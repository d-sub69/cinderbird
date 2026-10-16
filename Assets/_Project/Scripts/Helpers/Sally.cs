using System;
using System.Collections.Generic;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Embers;
using Cinderbird.Nest;
using Cinderbird.Player;
using Cinderbird.Sifting;
using Cinderbird.Terrain;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Sally, the helper: hired in the shop, she appears by the shop and sifts the mountain like the player does: she walks
    /// to the nearest open ash of the layer she works, scoops it with a sieve of her own (the player's sieve, the same pour,
    /// shake and dust), shakes it out, and once her sieve holds all the embers it can she carries them to the nest and tips
    /// them in. Sent to a spot (the green circle), she walks to that exact spot first and works its layer from there; on
    /// her own, she works the outermost open layer. When her layer has nothing left she can reach, she takes the outermost
    /// open layer that has, then the ones under it; with nothing anywhere she waits by the shop, facing the mountain, and
    /// says why. Spoken to (E), she stops, turns to face the player and waits for the menu.
    /// What to do next is <see cref="HelperBrain"/>; this adapter does it.
    /// </summary>
    public sealed class Sally : MonoBehaviour, IInteractable
    {
        // Each failed try to reach the nest or her spot by the shop widens the ring she may stand in by this much.
        private const float RingWidening = 0.75f;
        private const int MaxRingWidenings = 4;
        // The spot she is sent to may be this much above or below where she ends up standing (meters).
        private const float SpotHeightReach = 1.5f;
        // Less ash than this on the whole mountain (cubic meters) counts as none: no search needed.
        private const float NoAshLeft = 0.01f;

        private static readonly ProfilerMarker SearchMarker = new ProfilerMarker("Cinderbird.Helper");

        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private NestController _nest;
        [SerializeField] private Upgrades _upgrades;
        [Tooltip("Hiring her: level value 1 means hired.")]
        [SerializeField] private TableUpgrade _hire;
        [Tooltip("Her pace: the level's value is the share of the base sieve time (and pour) she takes.")]
        [SerializeField] private TableUpgrade _pace;
        [Tooltip("Her sieve: the level's value is how many embers it holds.")]
        [SerializeField] private TableUpgrade _sieveSize;
        [Tooltip("The player's sieves: hers is the first one (scoop size and look).")]
        [SerializeField] private SieveUpgrade _sieveTiers;
        [SerializeField] private HelperSettings _settings;
        [SerializeField] private ScoopSettings _scoopSettings;
        [SerializeField] private SieveSettings _sieveSettings;
        [SerializeField] private HelperMover _mover;
        [SerializeField] private HelperAnimation _animation;
        [Tooltip("Her looks; hidden until she is hired.")]
        [SerializeField] private GameObject _body;
        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveFiller _filler;
        [SerializeField] private SieveShakeController _shaker;
        [SerializeField] private ToolPourMotion _pourMotion;
        [SerializeField] private SieveView _sieveView;
        [Tooltip("Her spot by the shop.")]
        [SerializeField] private Transform _home;
        [Tooltip("The player, to face while talking.")]
        [SerializeField] private Transform _player;

        private readonly List<Ember> _extracted = new List<Ember>();
        private readonly List<Ember> _moved = new List<Ember>();
        private readonly HashSet<int> _unreachable = new HashSet<int>();
        private readonly Dictionary<int, float> _unreachableUntil = new Dictionary<int, float>();
        private readonly List<int> _expiredKeys = new List<int>();
        private float[] _removedByLayer = Array.Empty<float>();
        private AshSurvey _survey;
        private CharacterController _collider;
        private float _nestRadius;
        private bool _hired;
        private bool _talking;
        private float _busySeconds;
        // The spot the player sent her to (local), its layer, and whether she still has to walk there.
        private bool _hasSpot;
        private Vector3 _spot;
        private int _spotLayer = AssignRules.OutermostLayer;
        private bool _spotPending;
        // The layer she works now; -1 until she has found one.
        private int _workLayer = -1;
        private bool _hasTarget;
        private Vector3 _target;
        private int _targetKey;
        private float _nextWorkSearch;
        private Destination _destination;
        private Vector3 _destinationPoint;
        private int _ringWidenings;
        private float _activitySeconds;
        // After a search found nothing: what the world looked like then. She searches again only once it has changed.
        private bool _noWork;
        private int _noWorkSignature;

        private enum Destination
        {
            None,
            Spot,
            Work,
            Nest,
            Home,
        }

        /// <summary>The player spoke to her (E): open her menu. She waits until <see cref="EndTalk"/>.</summary>
        public event Action<Sally> TalkRequested;

        /// <summary>A short message for the player (she can't work, and why).</summary>
        public event Action<string> Notice;

        /// <summary>Her mode, layer or what she is doing changed (for the HUD).</summary>
        public event Action Changed;

        public bool IsHired => _hired;

        public HelperMode Mode { get; private set; } = HelperMode.Auto;

        /// <summary>Whether the player sent her to a spot (in <see cref="HelperMode.Assigned"/>).</summary>
        public bool HasSpot => _hasSpot;

        /// <summary>The spot the player sent her to (world).</summary>
        public Vector3 Spot => _pile.transform.TransformPoint(_spot);

        /// <summary>The layer she was sent to work there; <see cref="AssignRules.OutermostLayer"/> for the outermost open one.</summary>
        public int SpotLayer => _spotLayer;

        /// <summary>She was sent to a spot and hasn't reached it yet.</summary>
        public bool SpotPending => _spotPending;

        /// <summary>The layer she works now; -1 when none.</summary>
        public int WorkLayer => _workLayer;

        /// <summary>Why she isn't working, when she isn't (for the HUD and the menu); empty while she works.</summary>
        public string CantWorkReason { get; private set; } = string.Empty;

        public HelperActivity Activity { get; private set; } = HelperActivity.Idle;

        public Sieve Sieve => _sieve;

        public SieveFiller Filler => _filler;

        public VoxelTerrain Pile => _pile;

        public HelperSettings Settings => _settings;

        public HelperMover Mover => _mover;

        /// <summary>Whether she has a spot to dig right now (for the debug read-out).</summary>
        public bool HasTarget => _hasTarget;

        /// <summary>The last thing that went wrong for her (for the debug read-out); empty when nothing has.</summary>
        public string LastProblem { get; private set; } = string.Empty;

        public string Prompt => "talk to Sally";

        private bool IsBusy => _busySeconds > 0f || _filler.IsFilling || _pourMotion.IsPlaying;

        private void Awake()
        {
            if (_pile == null || _nest == null || _upgrades == null || _hire == null || _pace == null || _sieveSize == null ||
                _sieveTiers == null || _settings == null || _scoopSettings == null || _sieveSettings == null || _mover == null ||
                _animation == null || _body == null || _sieve == null || _filler == null || _shaker == null || _pourMotion == null ||
                _sieveView == null || _home == null || _player == null)
            {
                Debug.LogError($"{nameof(Sally)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _collider = _mover.GetComponent<CharacterController>();
            _survey = new AshSurvey(_settings.SurveyStride, _settings.MinAshDepth, _settings.NearBand);
            _nestRadius = FootprintRadius(_nest.transform);
        }

        private void OnEnable()
        {
            if (_survey == null)
            {
                return;
            }

            _upgrades.Levels.Changed += OnLevelChanged;
            _pile.LayerUnlocked += OnLayerUnlocked;
        }

        private void OnDisable()
        {
            if (_survey == null)
            {
                return;
            }

            _upgrades.Levels.Changed -= OnLevelChanged;
            _pile.LayerUnlocked -= OnLayerUnlocked;
        }

        private void Start()
        {
            ApplyUpgrades();
        }

        public void Interact()
        {
            if (!_hired || _talking)
            {
                return;
            }

            // Nobody to open her menu (it isn't set up): she would stand waiting for it forever.
            if (TalkRequested == null)
            {
                Log("was spoken to, but no menu listens for her");
                return;
            }

            _talking = true;
            _mover.Stop();
            _destination = Destination.None;
            TalkRequested?.Invoke(this);
        }

        /// <summary>The player is done talking (the menu closed, or a spot was chosen): she goes back to work.</summary>
        public void EndTalk()
        {
            _talking = false;
        }

        /// <summary>Auto (work the outermost open layer, from where she is) or Off (deliver, then wait by the shop).</summary>
        public void SetMode(HelperMode mode)
        {
            Mode = mode == HelperMode.Assigned ? HelperMode.Auto : mode;
            _hasSpot = false;
            _spotPending = false;
            _workLayer = -1;
            ForgetWork();
            Changed?.Invoke();
        }

        /// <summary>Whether she could walk to a spot from where she is now (for the green circle).</summary>
        public bool CanReach(Vector3 spot)
        {
            return _mover.CanReach(spot, new Vector2(0f, _settings.SpotRadius), SpotHeightReach, SpotHeightReach);
        }

        /// <summary>
        /// Sends her to a spot (the player checked she may, <see cref="AssignRules"/>): she walks there first, then works the
        /// nearest ash of <paramref name="layer"/> from there.
        /// </summary>
        /// <param name="spot">Where the player pointed (world).</param>
        /// <param name="layer">The layer under the spot, or <see cref="AssignRules.OutermostLayer"/> for a spot on the ground.</param>
        public void Assign(Vector3 spot, int layer)
        {
            Mode = HelperMode.Assigned;
            _hasSpot = true;
            _spot = _pile.transform.InverseTransformPoint(spot);
            _spotLayer = layer;
            _spotPending = true;
            _workLayer = layer;
            ForgetWork();
            Changed?.Invoke();
        }

        /// <summary>Puts her back as a save had her: mode, spot, where she stood (same mountain only).</summary>
        public void Restore(HelperMode mode, bool hasSpot, Vector3 spot, int spotLayer, bool spotPending, bool hasPose, Vector3 position,
            float yaw)
        {
            Mode = mode == HelperMode.Assigned && !hasSpot ? HelperMode.Auto : mode;
            _hasSpot = Mode == HelperMode.Assigned;
            _spot = _pile.transform.InverseTransformPoint(spot);
            _spotLayer = spotLayer;
            _spotPending = _hasSpot && spotPending;
            _workLayer = _hasSpot ? spotLayer : -1;
            ForgetWork();
            if (_hired && hasPose)
            {
                _mover.Restore(position, yaw);
            }

            Changed?.Invoke();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            if (!_hired || deltaTime <= 0f || _pile.Terrain == null)
            {
                return;
            }

            _busySeconds = Mathf.Max(0f, _busySeconds - deltaTime);
            if (_spotPending && IsAt(Destination.Spot))
            {
                ReachSpot(null);
            }

            bool busy = IsBusy;
            bool deciding = !_talking && !busy && !_sieve.Contents.HasAsh;
            if (deciding && !_spotPending && !_hasTarget && Mode != HelperMode.Off && Time.time >= _nextWorkSearch)
            {
                using (SearchMarker.Auto())
                {
                    LookForWork();
                }
            }

            SieveContents contents = _sieve.Contents;
            HelperFacts facts = new HelperFacts(_talking, busy, contents.HasAsh, contents.IsFullOfEmbers, contents.EmberCount > 0,
                _hasTarget, IsAt(Destination.Work), IsAt(Destination.Nest), IsAt(Destination.Home), _spotPending);
            HelperActivity activity = HelperBrain.Decide(Mode, facts);
            if (activity != Activity)
            {
                Activity = activity;
                _activitySeconds = 0f;
                Changed?.Invoke();
            }

            _activitySeconds += deltaTime;
            Watch(activity);
            if (activity != HelperActivity.Shake)
            {
                _shaker.Drive(false, 0f);
            }

            Act(activity);
            _animation.SetWalkSpeed(_mover.Speed);
        }

        // Things that must never last: a shake that doesn't empty the sieve, a scoop or pour that never ends, a walk that
        // never arrives. Logged with what she knows, and undone, so a fault shows in the log instead of a helper standing still.
        private void Watch(HelperActivity activity)
        {
            bool walking = activity == HelperActivity.GoToSpot || activity == HelperActivity.GoToWork ||
                           activity == HelperActivity.GoToNest || activity == HelperActivity.GoHome;
            if (walking)
            {
                WatchWalk(activity);
            }
            else if (activity == HelperActivity.Shake && _activitySeconds > _settings.SieveSeconds * 3f + 2f)
            {
                Log($"shook {_activitySeconds:0} s and {_sieve.Contents.AshVolume:0.0000} m3 is still in the sieve " +
                    $"(shake energy {_shaker.Energy:0.00}); she tips the rest out");
                _sieve.Contents.RemoveAsh(_sieve.Contents.AshVolume);
                _activitySeconds = 0f;
            }
            else if (activity == HelperActivity.Wait && !_talking && _activitySeconds > 15f)
            {
                Log($"busy {_activitySeconds:0} s: filling {_filler.IsFilling}, tipping {_pourMotion.IsPlaying}, timer {_busySeconds:0.0} s");
                _busySeconds = 0f;
                _activitySeconds = 0f;
            }
        }

        private void WatchWalk(HelperActivity activity)
        {
            // Hired means able to walk: the mover and her body must be on.
            if (!_mover.enabled || !_collider.enabled)
            {
                Log("her mover or body was switched off while hired; switching them back on");
                _mover.enabled = true;
                _collider.enabled = true;
            }

            if (_activitySeconds <= 45f)
            {
                return;
            }

            Log($"{activity} for {_activitySeconds:0} s without arriving ({_mover.Describe()}); starting over");
            if (activity == HelperActivity.GoToWork)
            {
                GiveUpTarget();
            }
            else if (activity == HelperActivity.GoToSpot)
            {
                ReachSpot("couldn't get all the way to the spot; starts from where she got to");
            }

            _destination = Destination.None;
            _mover.Stop();
            _activitySeconds = 0f;
        }

        private void Act(HelperActivity activity)
        {
            switch (activity)
            {
                case HelperActivity.Wait:
                    if (_talking)
                    {
                        _mover.Face(_player.position);
                        _animation.SetPose(HelperPose.Talk);
                    }

                    break;
                case HelperActivity.Idle:
                    // Waiting by the shop, she keeps an eye on the mountain.
                    _mover.Face(_pile.transform.position);
                    _animation.SetPose(HelperPose.Idle);
                    break;
                case HelperActivity.GoHome:
                    WalkToRing(Destination.Home, _home.position, 0f, _settings.HomeRadius);
                    break;
                case HelperActivity.GoToSpot:
                    // As close as she can get counts: the walk failing must not leave her standing there for good.
                    if (_destination == Destination.Spot && _mover.HasFailed)
                    {
                        ReachSpot("can't get all the way to the spot; starts from where she got to");
                        break;
                    }

                    Walk(Destination.Spot, Spot, new Vector2(0f, _settings.SpotRadius), SpotHeightReach, SpotHeightReach);
                    break;
                case HelperActivity.GoToWork:
                    // Checked before walking again: walking would start the same failed walk over and hide the failure.
                    if (_destination == Destination.Work && _mover.HasFailed)
                    {
                        Log($"can't reach the ash at {_target}; trying another spot");
                        GiveUpTarget();
                        break;
                    }

                    Walk(Destination.Work, _target, _settings.ScoopDistance, _settings.ReachUp, _settings.ReachDown);
                    break;
                case HelperActivity.Scoop:
                    Scoop();
                    break;
                case HelperActivity.Shake:
                    // A steady, brisk shake: the same simulation as the player's, run at the speed TimeShake set.
                    _shaker.Drive(true, _shaker.FullWiggleSpeed * 1.25f);
                    _animation.SetPose(HelperPose.Shake);
                    break;
                case HelperActivity.GoToNest:
                    WalkToRing(Destination.Nest, _nest.transform.position, _nestRadius + _settings.NestDistance.x, _nestRadius + _settings.NestDistance.y);
                    break;
                case HelperActivity.Pour:
                    Pour();
                    break;
            }
        }

        // <param name="problem">Why she stopped short of it; null when she got there.</param>
        private void ReachSpot(string problem)
        {
            _spotPending = false;
            if (problem != null)
            {
                Log(problem);
            }

            Changed?.Invoke();
        }

        private bool IsAt(Destination destination)
        {
            if (_destination != destination || !_mover.HasArrived)
            {
                return false;
            }

            // Arrived at work means in reach of this spot, not the one before it.
            return destination != Destination.Work || (_destinationPoint - _target).sqrMagnitude < 0.0001f;
        }

        private void Walk(Destination destination, Vector3 point, Vector2 distance, float reachUp, float reachDown)
        {
            _animation.SetPose(_mover.IsWalking ? HelperPose.Walk : HelperPose.Idle);
            if (_destination == destination && (_destinationPoint - point).sqrMagnitude < 0.0001f && !_mover.HasFailed)
            {
                return;
            }

            if (_destination != destination)
            {
                _ringWidenings = 0;
            }

            _destination = destination;
            _destinationPoint = point;
            _mover.GoTo(point, distance, reachUp, reachDown);
        }

        // The nest and her spot by the shop: if nowhere in the ring can be reached (something stands there), the ring
        // grows a little at each try, so she never freezes in place waiting for a spot that stays blocked.
        private void WalkToRing(Destination destination, Vector3 center, float inner, float outer)
        {
            if (_destination == destination && _mover.HasFailed)
            {
                if (_ringWidenings < MaxRingWidenings)
                {
                    _ringWidenings++;
                }
                else
                {
                    // As wide as it gets: wait a moment for whatever is in the way to move.
                    _busySeconds = 1f;
                }

                Log($"can't reach {destination}; widening to {outer + _ringWidenings * RingWidening:0.0} m");
                _destinationPoint = new Vector3(float.NaN, 0f, 0f);
            }

            float widening = _destination == destination ? _ringWidenings * RingWidening : 0f;
            Walk(destination, center, new Vector2(inner, outer + widening), 2f, 2f);
        }

        private void Scoop()
        {
            _animation.SetPose(HelperPose.Scoop);
            if (!_mover.Face(_target))
            {
                return;
            }

            // Like the player's scoop: the sphere pushed into the surface along the line from her hands takes a real bite.
            Vector3 hands = transform.position + Vector3.up * _settings.HandsHeight + transform.forward * 0.3f;
            Vector3 direction = (_target - hands).normalized;
            float radius = _sieveTiers.TierAt(0).ScoopRadius;
            Vector3 center = _target + direction * (radius * _scoopSettings.Penetration);
            float[] removedByLayer = LayerBuffer(_pile.LayerCount);
            _extracted.Clear();
            float pourSeconds = _filler.PourDuration;
            DigResult result = _pile.Dig(center, radius, _extracted, removedByLayer, _scoopSettings.EmberExposeMargin, pourSeconds);
            Vector3 spot = _target;
            int key = _targetKey;
            _hasTarget = false;
            if (!result.RemovedAnyAsh && result.EmberCount == 0)
            {
                // Nothing there after all (sealed, or the ash slid away): skip the spot for a while.
                Log($"the ash at {spot} held nothing to scoop");
                MarkUnreachable(key);
                return;
            }

            _pile.MixLayerColors(removedByLayer, out Color ashColor, out Color dustColor);
            _filler.TryBegin(result.RemovedVolume, _extracted, spot, ashColor, dustColor);
            _animation.Scoop(spot, pourSeconds);
            TimeShake(result.RemovedVolume, pourSeconds);
        }

        // One sieve (the pour in plus the shake) takes the set time at her pace: the same shake simulation, run at the
        // speed that clears this load in the time left after the pour. The ramp-up of the shake is allowed for.
        private void TimeShake(float volume, float pourSeconds)
        {
            ShakeParameters shake = _shaker.Parameters;
            if (volume <= 0f || shake.MaxRemovalRate <= 0f)
            {
                return;
            }

            float pace = Mathf.Max(0.05f, _pace.ValueAt(_upgrades.LevelOf(_pace)));
            float shakeSeconds = Mathf.Max(0.5f, _settings.SieveSeconds * pace - pourSeconds);
            float simulatedSeconds = volume / shake.MaxRemovalRate + shake.RiseTime * 0.5f;
            _shaker.SetTimeScale(simulatedSeconds / shakeSeconds);
        }

        private void Pour()
        {
            _animation.SetPose(HelperPose.Pour);
            if (!_mover.Face(_nest.transform.position))
            {
                return;
            }

            PourOutcome outcome = EmberTransfer.Pour(_sieve.Contents, _nest.Sink, _moved);
            if (outcome == PourOutcome.Poured)
            {
                _nest.OnPoured(_moved, _sieve.transform.position);
            }

            _pourMotion.Play(outcome != PourOutcome.Poured);
            _busySeconds = _pourMotion.TipDuration;
            _destination = Destination.None;
        }

        // A search costs milliseconds when there is nothing to find (every column of the mountain is looked at). After one
        // found nothing, the next waits until something that could change the answer changed: the ash (chunk versions), the
        // open layers, or the spots she skips. Standing idle on an unchanged mountain then costs nothing.
        private void LookForWork()
        {
            ForgetExpired();
            int signature = WorkSignature();
            if (!_noWork || signature != _noWorkSignature)
            {
                _noWork = !FindWork();
                _noWorkSignature = signature;
            }

            // Only a search that found nothing waits before the next: after a scoop she looks again at once.
            if (_noWork)
            {
                _nextWorkSearch = Time.time + _settings.IdleRecheckSeconds;
            }
        }

        private int WorkSignature()
        {
            VoxelWorld world = _pile.Terrain.World;
            int signature = _pile.DeepestUnlockedLayer * 7919 + _unreachable.Count * 104729;
            for (int i = 0; i < world.ChunkCount; i++)
            {
                signature = signature * 31 + world.VersionOf(i);
            }

            return signature;
        }

        // The next spot to dig, from where she stands: in the layer she works; when that has nothing she can reach, in the
        // outermost open layer that has, then the ones under it. With nothing anywhere she says why and waits.
        private bool FindWork()
        {
            int deepest = _pile.DeepestUnlockedLayer;
            if (_pile.TotalAshVolume < NoAshLeft)
            {
                _workLayer = -1;
                SetCantWork("there is no ash left for her to sift");
                return false;
            }

            Vector3 from = _pile.transform.InverseTransformPoint(transform.position);
            if (_workLayer >= 0 && _workLayer <= deepest && TryWorkIn(_workLayer, from))
            {
                return true;
            }

            for (int layer = 0; layer <= deepest; layer++)
            {
                if (layer != _workLayer && TryWorkIn(layer, from))
                {
                    _workLayer = layer;
                    Changed?.Invoke();
                    return true;
                }
            }

            _workLayer = -1;
            SetCantWork(WhyNoWork(from, deepest));
            return false;
        }

        private bool TryWorkIn(int layer, Vector3 from)
        {
            AshTerrain terrain = _pile.Terrain;
            if (!_survey.TryFindNearest(terrain.World, terrain.Locks, from, layer, _unreachable, out Vector3 surface, out int key))
            {
                return false;
            }

            _hasTarget = true;
            _target = _pile.transform.TransformPoint(surface);
            _targetKey = key;
            SetCantWork(string.Empty);
            return true;
        }

        // Ash she can't reach in an open layer, or all open layers sifted and the next one sealed, or the mountain done. The
        // search just failed with her skipped spots left out; only if she skips some can open ash be left, so only then is
        // there a second search.
        private string WhyNoWork(Vector3 from, int deepest)
        {
            AshTerrain terrain = _pile.Terrain;
            for (int layer = 0; layer <= deepest && _unreachable.Count > 0; layer++)
            {
                if (_survey.HasOpenAsh(terrain.World, terrain.Locks, from, layer, null))
                {
                    return $"she can't reach the ash left in the {_pile.LayerName(layer)}";
                }
            }

            return deepest + 1 < _pile.LayerCount
                ? $"the {_pile.LayerName(deepest + 1)} is still sealed"
                : "there is no ash left for her to sift";
        }

        private void SetCantWork(string reason)
        {
            if (reason == CantWorkReason)
            {
                return;
            }

            CantWorkReason = reason;
            Changed?.Invoke();
            if (reason.Length > 0)
            {
                Log($"can't work: {reason}");
                Notice?.Invoke($"Sally can't work: {reason}. She waits by the shop.");
            }
        }

        private void GiveUpTarget()
        {
            MarkUnreachable(_targetKey);
            _hasTarget = false;
            _destination = Destination.None;
            _mover.Stop();
        }

        // A spot she couldn't reach (or that held nothing) is skipped for a while: slides and digging change the way there.
        private void MarkUnreachable(int key)
        {
            _unreachable.Add(key);
            _unreachableUntil[key] = Time.time + _settings.RetryUnreachableSeconds;
        }

        private void ForgetExpired()
        {
            float now = Time.time;
            _expiredKeys.Clear();
            foreach (KeyValuePair<int, float> entry in _unreachableUntil)
            {
                if (entry.Value <= now)
                {
                    _expiredKeys.Add(entry.Key);
                }
            }

            foreach (int key in _expiredKeys)
            {
                _unreachableUntil.Remove(key);
                _unreachable.Remove(key);
            }
        }

        private void ForgetWork()
        {
            _noWork = false;
            _hasTarget = false;
            _nextWorkSearch = 0f;
            _destination = Destination.None;
            _mover.Stop();
            SetCantWork(string.Empty);
        }

        // A new layer opened: she may have work again, and she looks at once.
        private void OnLayerUnlocked(int layer)
        {
            _noWork = false;
            _unreachable.Clear();
            _unreachableUntil.Clear();
            _hasTarget = false;
            _nextWorkSearch = 0f;
        }

        private void OnLevelChanged(string id)
        {
            if (id == _hire.Id || id == _pace.Id || id == _sieveSize.Id)
            {
                ApplyUpgrades();
            }
        }

        private void ApplyUpgrades()
        {
            bool hired = _hire.ValueAt(_upgrades.LevelOf(_hire)) >= 1f;
            float pace = Mathf.Max(0.05f, _pace.ValueAt(_upgrades.LevelOf(_pace)));
            _filler.SetDurationScale(pace);
            _pourMotion.SetTimeScale(1f / pace);

            int capacity = Mathf.Max(1, Mathf.RoundToInt(_sieveSize.ValueAt(_upgrades.LevelOf(_sieveSize))));
            _sieve.SetCapacity(capacity);
            _sieveView.SetSize(_sieveTiers.TierAt(0).VisualScale, Mathf.Max(capacity, _sieveSettings.MaxVisibleEmbers));

            if (hired == _hired && _body.activeSelf == hired)
            {
                return;
            }

            bool arriving = hired && !_hired;
            _hired = hired;
            _body.SetActive(hired);
            _collider.enabled = hired;
            _mover.enabled = hired;
            if (arriving)
            {
                // She arrives at her spot by the shop.
                _mover.Restore(_home.position, Quaternion.LookRotation(Flat(_pile.transform.position - _home.position)).eulerAngles.y);
                SetMode(HelperMode.Auto);
            }

            Changed?.Invoke();
        }

        // How far the nest reaches out from its center (its solid parts), so she stands just outside it to pour.
        private static float FootprintRadius(Transform root)
        {
            float radius = 0f;
            Vector3 center = root.position;
            foreach (Collider part in root.GetComponentsInChildren<Collider>())
            {
                if (part.isTrigger)
                {
                    continue;
                }

                Bounds bounds = part.bounds;
                Vector3 far = new Vector3(Mathf.Max(Mathf.Abs(bounds.min.x - center.x), Mathf.Abs(bounds.max.x - center.x)), 0f,
                    Mathf.Max(Mathf.Abs(bounds.min.z - center.z), Mathf.Abs(bounds.max.z - center.z)));
                radius = Mathf.Max(radius, far.magnitude);
            }

            return radius;
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > 0.0001f ? direction : Vector3.forward;
        }

        private void Log(string message)
        {
            LastProblem = message;
            Changed?.Invoke();
            if (DevTools.Enabled)
            {
                Debug.Log($"[Sally] {message}", this);
            }
        }

        // Reused between digs; only reallocated if the terrain's layer count changes.
        private float[] LayerBuffer(int layerCount)
        {
            if (_removedByLayer.Length != layerCount)
            {
                _removedByLayer = new float[layerCount];
            }

            Array.Clear(_removedByLayer, 0, _removedByLayer.Length);
            return _removedByLayer;
        }
    }
}
