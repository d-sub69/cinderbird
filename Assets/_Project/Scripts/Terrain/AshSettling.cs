using System;
using Cinderbird.Core;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter that runs <see cref="AshSettler"/>: steps on a worker thread and, many times a second, writes
    /// the result into the terrain on the main thread, so ash is seen to move. Driven by <see cref="VoxelTerrain"/>:
    /// <list type="bullet">
    /// <item>A scoop (<see cref="StartScoop"/>) is one simulation with its slide: the scooped ash drains out of the
    /// pile over the pour time while the surrounding ash sinks into the hole as it forms.</item>
    /// <item>Any other dig settles afterwards (<see cref="SettleAfterDig"/>).</item>
    /// </list>
    /// A new dig first writes out what is running (<see cref="WriteOutNow"/>), scoop ash fully taken (its own
    /// settling picks the slide up), so a late write can never overwrite a fresh hole. Freezes while the game is
    /// paused (scaled time).
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class AshSettling : MonoBehaviour
    {
        private static readonly ProfilerMarker SettleMarker = new ProfilerMarker("Cinderbird.Settle");
        private static readonly ProfilerMarker WaitMarker = new ProfilerMarker("Cinderbird.SettleWait");

        [SerializeField] private SettleSettings _settings;
        [Tooltip("Optional: puffs of dust where ash runs. World-space simulation.")]
        [SerializeField] private ParticleSystem _dust;

        private VoxelTerrain _terrain;
        private AshSettler _settler;
        private WorkerThread _worker;
        private bool _working;
        private float _nextUpdateTime;

        // A plain dig waiting to be settled from the next Update.
        private bool _beginPending;
        private Vector3 _pendingCenter;
        private float _pendingRadius;

        // The scoop being drained, until its ash is all out and its Dug event has fired.
        private bool _scoopActive;
        private DigResult _scoopResult;
        private float _scoopStartTime;
        private float _scoopSeconds;

        // Everything the current slide changed so far, for one glimmer refresh when it ends.
        private Bounds _slideBounds;
        private bool _hasSlideBounds;

        // What the next batch of work does; set on the main thread before it starts (starting the worker is a
        // memory barrier).
        private AshTerrain _workTerrain;
        private bool _workBegins;
        private bool _workBeginsScoop;
        private int _workSteps;
        // Written by the worker thread, read after waiting for it.
        private float _workMilliseconds;

        /// <summary>Whether ash is moving right now (a scoop draining or a slide running).</summary>
        public bool IsSettling => _beginPending || _settler != null && _settler.IsActive;

        /// <summary>Main-thread milliseconds of the last update (writing the slide and re-meshing), for the budget.</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Worker-thread milliseconds of the last batch of steps (off the main thread).</summary>
        public float LastWorkMilliseconds { get; private set; }

        /// <summary>Part of <see cref="LastUpdateMilliseconds"/> spent writing the slide into the terrain.</summary>
        public float LastCommitMilliseconds { get; private set; }

        /// <summary>Part of <see cref="LastUpdateMilliseconds"/> spent re-meshing the changed chunks.</summary>
        public float LastMeshMilliseconds { get; private set; }

        /// <summary>Counts updates, so the performance overlay can tell a new one happened.</summary>
        public int UpdateCount { get; private set; }

        private void Awake()
        {
            _terrain = GetComponent<VoxelTerrain>();
            if (_settings == null)
            {
                Debug.LogError($"{nameof(AshSettling)} on '{name}' has no settings.", this);
                enabled = false;
                return;
            }

            _settler = new AshSettler(_settings.ToParameters());
            _worker = new WorkerThread("Cinderbird ash settling", DoWork);
        }

        private void OnDisable()
        {
            WriteOutNow();
        }

        private void OnDestroy()
        {
            _worker?.Dispose();
        }

        /// <summary>
        /// Takes a counted but uncarved dig (<see cref="AshTerrain.StartDig"/>) out of the pile over
        /// <paramref name="seconds"/>, with the slide running at the same time. When the ash is all out,
        /// <see cref="VoxelTerrain.CompleteScoop"/> is called with <paramref name="result"/>.
        /// </summary>
        public void StartScoop(DigResult result, Vector3 localCenter, float radius, float seconds)
        {
            WriteOutNow();
            _scoopActive = true;
            _scoopResult = result;
            _scoopStartTime = Time.time;
            _scoopSeconds = Mathf.Max(0.01f, seconds);
            _pendingCenter = localCenter;
            _pendingRadius = radius;
            // Loading the area runs off the main thread; draining starts with the first update.
            BeginLoad(true);
        }

        /// <summary>Settles the walls of a hole carved at once, from the next Update.</summary>
        public void SettleAfterDig(Vector3 localCenter, float radius)
        {
            if (_settler == null)
            {
                return;
            }

            _beginPending = true;
            _pendingCenter = localCenter;
            _pendingRadius = radius;
        }

        /// <summary>
        /// Takes out the rest of a scoop's ash at once (its ash is already in the sieve), e.g. before saving.
        /// The slide around it carries on.
        /// </summary>
        public void CompleteScoopNow()
        {
            if (!_scoopActive)
            {
                return;
            }

            if (_working)
            {
                FinishWork();
            }

            if (_settler.IsActive)
            {
                _settler.Drain(1f);
                WriteOut();
            }

            FireScoopDug();
            if (_settler.IsActive)
            {
                // The ash just taken out leaves walls that still need to slide.
                StartWork();
            }
        }

        /// <summary>
        /// Writes everything as it stands into the terrain (any scoop ash fully taken) and stops; ash still too
        /// steep is picked up by the next dig's settling. Waits for steps running on the worker thread first.
        /// </summary>
        public void WriteOutNow()
        {
            // A plain dig that hasn't started settling: with nothing loaded there's nothing to write.
            _beginPending = false;
            if (_working)
            {
                FinishWork();
            }

            if (_settler == null || !_settler.IsActive)
            {
                FireScoopDug();
                return;
            }

            _settler.Drain(1f);
            WriteOut();
            FireScoopDug();
            EndSlide();
        }

        private void Update()
        {
            if (_beginPending)
            {
                _beginPending = false;
                if (_terrain.Terrain != null)
                {
                    BeginLoad(false);
                }

                return;
            }

            if (!_working || !_worker.IsCompleted || Time.time < _nextUpdateTime)
            {
                return;
            }

            bool moving = FinishWork();
            bool draining = _settler.IsDraining;
            if (draining)
            {
                _settler.Drain((Time.time - _scoopStartTime) / _scoopSeconds);
            }

            WriteOut();
            if (_scoopActive && !_settler.IsDraining)
            {
                FireScoopDug();
            }

            // Ash just taken out needs more steps even if the last batch found everything at rest.
            if (!_settler.IsActive || !moving && !draining)
            {
                EndSlide();
                return;
            }

            StartWork();
        }

        private void BeginLoad(bool scoop)
        {
            if (_working)
            {
                FinishWork();
            }

            _workTerrain = _terrain.Terrain;
            _workBegins = true;
            _workBeginsScoop = scoop;
            // Copied here, on the main thread: loose embers can move while the area loads on the worker.
            _settler.UseLooseEmbers(_workTerrain.LooseEmbers);
            StartWork();
        }

        private void StartWork()
        {
            _workSteps = _settings.StepsPerUpdate;
            _working = true;
            _worker.Start();
            _nextUpdateTime = Time.time + 1f / _settings.UpdatesPerSecond;
        }

        // Runs on the worker thread.
        private bool DoWork()
        {
            long started = TimeMeasure.Now();
            if (_workBegins)
            {
                _workBegins = false;
                if (_workBeginsScoop)
                {
                    _settler.BeginScoop(_workTerrain, _pendingCenter, _pendingRadius);
                }
                else
                {
                    _settler.Begin(_workTerrain, _pendingCenter, _pendingRadius);
                }
            }

            bool moving = _settler.Step(_workSteps);
            // Timed here: the result is only collected at the next update.
            _workMilliseconds = TimeMeasure.MillisecondsSince(started);
            return moving;
        }

        private bool FinishWork()
        {
            _working = false;
            bool moving;
            Exception error;
            using (WaitMarker.Auto())
            {
                moving = _worker.Wait(out error);
            }

            if (error == null)
            {
                LastWorkMilliseconds = _workMilliseconds;
                return moving;
            }

            Debug.LogError($"Ash settling failed: {error}", this);
            _settler.End();
            _hasSlideBounds = false;
            if (_scoopActive)
            {
                // The scooped ash is already in the sieve: make sure it's out of the pile, whatever the slide did.
                _terrain.CarveNow(_pendingCenter, _pendingRadius);
            }

            return false;
        }

        private void FireScoopDug()
        {
            if (!_scoopActive)
            {
                return;
            }

            _scoopActive = false;
            _terrain.CompleteScoop(_scoopResult, _pendingCenter, _pendingRadius);
        }

        private void WriteOut()
        {
            if (!_settler.IsActive)
            {
                return;
            }

            using (SettleMarker.Auto())
            {
                long started = TimeMeasure.Now();
                _settler.Commit();
                LastCommitMilliseconds = TimeMeasure.MillisecondsSince(started);
                LastMeshMilliseconds = 0f;
                if (_settler.LastCommitChangedAnything)
                {
                    Bounds changed = _settler.LastCommitBounds;
                    if (_hasSlideBounds)
                    {
                        _slideBounds.Encapsulate(changed);
                    }
                    else
                    {
                        _slideBounds = changed;
                        _hasSlideBounds = true;
                    }

                    _terrain.ApplySettledEdits(changed);
                    LastMeshMilliseconds = _terrain.LastMeshMilliseconds;
                    PuffDust();
                }

                LastUpdateMilliseconds = TimeMeasure.MillisecondsSince(started);
                UpdateCount++;
            }
        }

        private void EndSlide()
        {
            _settler.End();
            if (_hasSlideBounds)
            {
                _hasSlideBounds = false;
                _terrain.FinishSettle(_slideBounds);
            }
        }

        private void PuffDust()
        {
            int perSpot = _settings.DustPerSpot;
            if (_dust == null || perSpot <= 0)
            {
                return;
            }

            for (int i = 0; i < _settler.LastSlidePoints.Count; i++)
            {
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(_settler.LastSlidePoints[i]),
                    applyShapeToPosition = true,
                    startColor = _terrain.DustColorOfLayer(_settler.LastSlideLayers[i]),
                };
                _dust.Emit(emit, perSpot);
            }
        }
    }
}
