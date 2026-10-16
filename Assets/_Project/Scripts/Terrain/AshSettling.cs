using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>
    /// Thin adapter that runs ash settling (<see cref="AshSettler"/>) in several areas at once: each dig gets its own
    /// <see cref="SettleJob"/>, all stepped on one worker thread and, many times a second, written into the terrain on the
    /// main thread, so ash is seen to move. Driven by <see cref="VoxelTerrain"/>:
    /// <list type="bullet">
    /// <item>A scoop (<see cref="StartScoop"/>) is one simulation with its slide: the scooped ash drains out of the
    /// pile over the pour time while the surrounding ash sinks into the hole as it forms.</item>
    /// <item>Any other dig settles afterwards (<see cref="SettleAfterDig"/>).</item>
    /// </list>
    /// Before a dig changes the terrain, <see cref="PrepareForDig"/> finishes only the areas it touches (scoop ash fully
    /// taken), so a late write can never overwrite a fresh hole, while slides elsewhere (a helper's patch) carry on.
    /// The main thread only touches the terrain while the worker is idle. Freezes while the game is paused.
    /// </summary>
    [RequireComponent(typeof(VoxelTerrain))]
    public sealed class AshSettling : MonoBehaviour
    {
        private static readonly ProfilerMarker SettleMarker = new ProfilerMarker("Cinderbird.Settle");
        private static readonly ProfilerMarker WaitMarker = new ProfilerMarker("Cinderbird.SettleWait");

        // Most areas settling at once; a new one beyond this finishes the oldest first.
        private const int MaxAreas = 4;

        [SerializeField] private SettleSettings _settings;
        [Tooltip("Optional: puffs of dust where ash runs. World-space simulation.")]
        [SerializeField] private ParticleSystem _dust;

        private readonly List<SettleJob> _jobs = new List<SettleJob>();
        private readonly Stack<SettleJob> _pool = new Stack<SettleJob>();
        private VoxelTerrain _terrain;
        private SettleParameters _parameters;
        private WorkerThread _worker;
        private bool _working;
        private float _nextUpdateTime;

        // What the worker runs; set on the main thread before it starts (starting it is a memory barrier).
        private AshTerrain _workTerrain;
        private int _workSteps;
        // Written by the worker thread, read after waiting for it.
        private float _workMilliseconds;

        /// <summary>Whether ash is moving right now anywhere (a scoop draining or a slide running).</summary>
        public bool IsSettling => _jobs.Count > 0;

        /// <summary>How many areas are settling right now.</summary>
        public int AreaCount => _jobs.Count;

        /// <summary>Main-thread milliseconds of the last update (writing every area and starting their re-mesh).</summary>
        public float LastUpdateMilliseconds { get; private set; }

        /// <summary>Worker-thread milliseconds of the last batch of steps, all areas (off the main thread).</summary>
        public float LastWorkMilliseconds { get; private set; }

        /// <summary>Part of <see cref="LastUpdateMilliseconds"/> spent writing the slides into the terrain.</summary>
        public float LastCommitMilliseconds { get; private set; }

        /// <summary>Part of <see cref="LastUpdateMilliseconds"/> spent starting the re-mesh of the changed chunks.</summary>
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

            _parameters = _settings.ToParameters();
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
        /// Before a dig: finishes every area the dig would touch (any scoop ash there fully taken, its Dug reported),
        /// so nothing still settling can write over the new hole. Areas elsewhere keep going.
        /// </summary>
        public void PrepareForDig(Vector3 localCenter, float radius)
        {
            if (_worker == null || _terrain.Terrain == null)
            {
                return;
            }

            WaitForWorker();
            SettleArea dig = AshSettler.AreaOf(_terrain.Terrain.World, localCenter, radius, _parameters.Reach);
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                if (_jobs[i].Area.Touches(dig))
                {
                    FinishNow(_jobs[i]);
                }
            }
        }

        /// <summary>
        /// Takes a counted but uncarved dig (<see cref="AshTerrain.StartDig"/>) out of the pile over
        /// <paramref name="seconds"/>, with the slide running at the same time. When the ash is all out,
        /// <see cref="VoxelTerrain.CompleteScoop"/> is called with <paramref name="result"/>.
        /// </summary>
        public void StartScoop(DigResult result, Vector3 localCenter, float radius, float seconds, DigExposure exposure)
        {
            SettleJob job = AddJob(localCenter, radius);
            job.StartScoop(job.Area, localCenter, radius, result, Time.time, seconds, exposure);
            StartWorkIfIdle();
        }

        /// <summary>Settles the walls of a hole carved at once.</summary>
        public void SettleAfterDig(Vector3 localCenter, float radius)
        {
            if (_worker == null)
            {
                return;
            }

            AddJob(localCenter, radius);
            StartWorkIfIdle();
        }

        /// <summary>
        /// Takes out the rest of every scoop's ash at once (it's already in the sieve), e.g. before saving. The slides
        /// around them carry on.
        /// </summary>
        public void CompleteScoopNow()
        {
            if (_worker == null)
            {
                return;
            }

            WaitForWorker();
            foreach (SettleJob job in _jobs)
            {
                if (!job.ScoopPending)
                {
                    continue;
                }

                job.Load(_terrain.Terrain);
                job.Settler.Drain(1f);
                WriteOut(job);
                FireScoopDug(job);
            }
        }

        /// <summary>
        /// Writes every area as it stands into the terrain (scoop ash fully taken) and stops; ash still too steep is
        /// picked up by the next dig's settling.
        /// </summary>
        public void WriteOutNow()
        {
            if (_worker == null)
            {
                return;
            }

            WaitForWorker();
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                FinishNow(_jobs[i]);
            }
        }

        private void Update()
        {
            if (_working)
            {
                if (!_worker.IsCompleted || Time.time < _nextUpdateTime)
                {
                    return;
                }

                FinishWork();
            }
            else if (_jobs.Count == 0)
            {
                return;
            }

            using (SettleMarker.Auto())
            {
                long started = TimeMeasure.Now();
                float commit = 0f;
                float mesh = 0f;
                for (int i = _jobs.Count - 1; i >= 0; i--)
                {
                    SettleJob job = _jobs[i];
                    if (job.NeedsLoading)
                    {
                        continue;
                    }

                    AshSettler settler = job.Settler;
                    bool draining = settler.IsDraining;
                    if (draining)
                    {
                        settler.Drain((Time.time - job.ScoopStartTime) / job.ScoopSeconds);
                    }

                    WriteOut(job);
                    commit += LastCommitMilliseconds;
                    mesh += LastMeshMilliseconds;
                    if (job.ScoopPending && !settler.IsDraining)
                    {
                        FireScoopDug(job);
                    }

                    // Ash just taken out needs more steps even if the last batch found everything at rest.
                    if (!settler.IsActive || (!job.Moving && !draining))
                    {
                        EndJob(job);
                    }
                }

                LastCommitMilliseconds = commit;
                LastMeshMilliseconds = mesh;
                LastUpdateMilliseconds = TimeMeasure.MillisecondsSince(started);
                UpdateCount++;
            }

            StartWorkIfIdle();
        }

        private SettleJob AddJob(Vector3 localCenter, float radius)
        {
            WaitForWorker();
            SettleArea area = AshSettler.AreaOf(_terrain.Terrain.World, localCenter, radius, _parameters.Reach);
            // Two simulations must never share a column (each would write its own copy back); the dig normally
            // finished these already, this keeps it true for any caller.
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                if (_jobs[i].Area.Touches(area))
                {
                    FinishNow(_jobs[i]);
                }
            }

            if (_jobs.Count >= MaxAreas)
            {
                FinishNow(_jobs[0]);
            }

            SettleJob job = _pool.Count > 0 ? _pool.Pop() : new SettleJob(_parameters);
            job.StartSlide(area, localCenter, radius);
            // Copied here, on the main thread: loose embers can move while the area loads on the worker.
            job.Settler.UseLooseEmbers(_terrain.Terrain.LooseEmbers);
            _jobs.Add(job);
            return job;
        }

        private void StartWorkIfIdle()
        {
            if (_working || _jobs.Count == 0)
            {
                return;
            }

            _workTerrain = _terrain.Terrain;
            _workSteps = _settings.StepsPerUpdate;
            _working = true;
            _worker.Start();
            _nextUpdateTime = Time.time + 1f / _settings.UpdatesPerSecond;
        }

        // Runs on the worker thread: loads new areas, then steps every area.
        private bool DoWork()
        {
            long started = TimeMeasure.Now();
            for (int i = 0; i < _jobs.Count; i++)
            {
                SettleJob job = _jobs[i];
                job.Load(_workTerrain);
                job.Moving = job.Settler.Step(_workSteps);
            }

            // Timed here: the result is only collected at the next update.
            _workMilliseconds = TimeMeasure.MillisecondsSince(started);
            return true;
        }

        private void WaitForWorker()
        {
            if (_working)
            {
                FinishWork();
            }
        }

        private void FinishWork()
        {
            _working = false;
            Exception error;
            using (WaitMarker.Auto())
            {
                _worker.Wait(out error);
            }

            if (error == null)
            {
                LastWorkMilliseconds = _workMilliseconds;
                return;
            }

            Debug.LogError($"Ash settling failed: {error}", this);
            for (int i = _jobs.Count - 1; i >= 0; i--)
            {
                SettleJob job = _jobs[i];
                if (job.ScoopPending)
                {
                    // The scooped ash is already in the sieve: make sure it's out of the pile, whatever the slide did.
                    _terrain.CarveNow(job.Center, job.Radius);
                    FireScoopDug(job);
                }

                job.Settler.End();
                _jobs.RemoveAt(i);
                _pool.Push(job);
            }
        }

        // Ends one area at once: loaded if it wasn't yet, scoop ash fully taken, written, reported.
        private void FinishNow(SettleJob job)
        {
            job.Load(_terrain.Terrain);
            if (job.Settler.IsActive)
            {
                job.Settler.Drain(1f);
                WriteOut(job);
            }

            FireScoopDug(job);
            EndJob(job);
        }

        private void FireScoopDug(SettleJob job)
        {
            if (!job.ScoopPending)
            {
                return;
            }

            job.ScoopPending = false;
            _terrain.CompleteScoop(job.ScoopResult, job.Center, job.Radius, job.ScoopExposure);
        }

        private void WriteOut(SettleJob job)
        {
            AshSettler settler = job.Settler;
            if (!settler.IsActive)
            {
                return;
            }

            long started = TimeMeasure.Now();
            settler.Commit();
            LastCommitMilliseconds = TimeMeasure.MillisecondsSince(started);
            LastMeshMilliseconds = 0f;
            if (!settler.LastCommitChangedAnything)
            {
                return;
            }

            Bounds changed = settler.LastCommitBounds;
            job.AddChanged(changed);
            _terrain.ApplySettledEdits(changed);
            LastMeshMilliseconds = _terrain.LastMeshMilliseconds;
            PuffDust(settler);
        }

        private void EndJob(SettleJob job)
        {
            job.Settler.End();
            _jobs.Remove(job);
            _pool.Push(job);
            if (job.HasSlideBounds)
            {
                _terrain.FinishSettle(job.SlideBounds);
            }
        }

        private void PuffDust(AshSettler settler)
        {
            int perSpot = _settings.DustPerSpot;
            if (_dust == null || perSpot <= 0)
            {
                return;
            }

            for (int i = 0; i < settler.LastSlidePoints.Count; i++)
            {
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = transform.TransformPoint(settler.LastSlidePoints[i]),
                    applyShapeToPosition = true,
                    startColor = _terrain.DustColorOfLayer(settler.LastSlideLayers[i]),
                };
                _dust.Emit(emit, perSpot);
            }
        }
    }
}
