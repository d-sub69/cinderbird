using System.Collections.Generic;
using Cinderbird.Terrain;
using Unity.Profiling;
using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// Shows and moves the embers lying loose on the pile or the ground (<see cref="AshTerrain.LooseEmbers"/>):
    /// glowing spheres that drop onto whatever is below with a small bounce and ride the ash under them, lifted by ash
    /// that comes over them and let down where it runs away, never buried again (<see cref="LooseEmberRules"/>). Also
    /// finds the one the player is aiming at and hands it over when picked up with the sieve.
    /// </summary>
    public sealed class LooseEmberView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly ProfilerMarker UpdateMarker = new ProfilerMarker("Cinderbird.LooseEmbers");

        // How far up through ash that came over it an ember is looked for its surface; ash this deep in one go is rare
        // (sliding ash lifts it as it comes).
        private const float MaxCoverSearch = 3f;

        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [SerializeField] private LooseEmberSettings _settings;
        [Tooltip("Small emissive sphere with no collider (the sieve's ember visual works).")]
        [SerializeField] private Renderer _emberPrefab;
        [Tooltip("What a falling ember can land on besides ash (the ground, rocks).")]
        [SerializeField] private LayerMask _groundMask = ~0;

        // Per loose ember id: its sphere, and its fall speed while falling (negative = bouncing up).
        private readonly Dictionary<int, Renderer> _visuals = new Dictionary<int, Renderer>();
        private readonly Dictionary<int, float> _falling = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _seenFrame = new Dictionary<int, int>();
        private readonly Stack<Renderer> _pool = new Stack<Renderer>();
        private readonly List<int> _scratchIds = new List<int>();
        private MaterialPropertyBlock _propertyBlock;
        private AshFallSurface _surface;
        private Transform _container;
        private bool _checkPending = true;
        private float _nextCheckTime;

        public int Count => _pile != null && _pile.Terrain != null ? _pile.Terrain.LooseEmbers.Count : 0;

        private void Awake()
        {
            if (_pile == null || _tierCatalog == null || _settings == null || _emberPrefab == null)
            {
                Debug.LogError($"{nameof(LooseEmberView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            _surface = new AshFallSurface(_pile, _groundMask);
            _container = new GameObject("Loose Embers").transform;
        }

        private void OnEnable()
        {
            if (_propertyBlock == null)
            {
                return;
            }

            _pile.Built += MarkForCheck;
            _pile.Restored += MarkForCheck;
            _pile.LooseEmbersChanged += MarkForCheck;
            _pile.Settled += OnSettled;
            _pile.Dug += OnDug;
        }

        private void OnDisable()
        {
            if (_propertyBlock == null)
            {
                return;
            }

            _pile.Built -= MarkForCheck;
            _pile.Restored -= MarkForCheck;
            _pile.LooseEmbersChanged -= MarkForCheck;
            _pile.Settled -= OnSettled;
            _pile.Dug -= OnDug;
        }

        private void OnDestroy()
        {
            if (_container != null)
            {
                Destroy(_container.gameObject);
            }
        }

        private void MarkForCheck()
        {
            _checkPending = true;
        }

        private void OnSettled(Bounds changed)
        {
            _checkPending = true;
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            _checkPending = true;
        }

        /// <summary>The loose ember closest along the aim line, within reach and the pick tolerance of it.</summary>
        public bool TryFindAimed(Ray worldRay, float reach, out int id, out float distance)
        {
            id = -1;
            distance = float.MaxValue;
            AshTerrain terrain = _pile.Terrain;
            if (terrain == null)
            {
                return false;
            }

            float tolerance = _settings.PickTolerance;
            IReadOnlyList<LooseEmber> loose = terrain.LooseEmbers;
            for (int i = 0; i < loose.Count; i++)
            {
                Vector3 position = _pile.transform.TransformPoint(loose[i].Ember.Position);
                float along = Vector3.Dot(position - worldRay.origin, worldRay.direction);
                if (along < 0f || along > reach || along >= distance)
                {
                    continue;
                }

                Vector3 closest = worldRay.origin + worldRay.direction * along;
                if ((position - closest).sqrMagnitude <= tolerance * tolerance)
                {
                    id = loose[i].Id;
                    distance = along;
                }
            }

            return id >= 0;
        }

        /// <summary>Takes a loose ember out of the world, e.g. into the sieve.</summary>
        public bool TryTake(int id, out Ember ember, out Vector3 worldPosition)
        {
            if (!_pile.TryTakeLoose(id, out ember, out worldPosition))
            {
                return false;
            }

            ReleaseVisual(id);
            return true;
        }

        private void Update()
        {
            AshTerrain terrain = _pile.Terrain;
            if (terrain == null)
            {
                return;
            }

            using (UpdateMarker.Auto())
            {
                if (_checkPending || Time.time >= _nextCheckTime)
                {
                    _checkPending = false;
                    _nextCheckTime = Time.time + _settings.CheckInterval;
                    CheckAgainstAsh(terrain);
                }

                AdvanceFalls(terrain, Time.deltaTime);
                SyncVisuals(terrain);
            }
        }

        // Keeps every loose ember at its rest above the ash (lifted by ash that came over it, let down where ash ran away)
        // and starts a fall for any with neither ash nor ground under it. Never buries one.
        private void CheckAgainstAsh(AshTerrain terrain)
        {
            IReadOnlyList<LooseEmber> loose = terrain.LooseEmbers;
            for (int i = 0; i < loose.Count; i++)
            {
                LooseEmber item = loose[i];
                if (_falling.ContainsKey(item.Id))
                {
                    continue;
                }

                Vector3 center = item.Ember.Position;
                float radius = RadiusOf(item.Ember.TierIndex);
                if (!LooseEmberRules.TryFindSurface(terrain.World, center, radius, MaxCoverSearch, out float surface))
                {
                    if (!_surface.IsOnGround(center, radius))
                    {
                        _falling[item.Id] = 0f;
                        terrain.SetLooseRest(i, LooseEmber.UnknownRest);
                    }

                    continue;
                }

                float y = LooseEmberRules.Settle(center.y, surface, item.Rest, radius, out float rest);
                if (!Mathf.Approximately(y, center.y))
                {
                    terrain.MoveLoose(i, new Vector3(center.x, y, center.z));
                }

                if (!rest.Equals(item.Rest))
                {
                    terrain.SetLooseRest(i, rest);
                }
            }
        }

        private void AdvanceFalls(AshTerrain terrain, float deltaTime)
        {
            if (_falling.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            _scratchIds.Clear();
            FallParameters fall = _settings.Fall;
            _scratchIds.AddRange(_falling.Keys);
            foreach (int id in _scratchIds)
            {
                int index = terrain.IndexOfLoose(id);
                if (index < 0)
                {
                    _falling.Remove(id);
                    continue;
                }

                Ember ember = terrain.LooseEmbers[index].Ember;
                FallStep step = LooseEmberFall.Advance(ember.Position, _falling[id], RadiusOf(ember.TierIndex), deltaTime, fall, _surface);
                terrain.MoveLoose(index, step.Position);
                if (step.Resting)
                {
                    _falling.Remove(id);
                    // Landed: measure how it sits on the ash right away, so sliding ash can carry it.
                    _checkPending = true;
                }
                else
                {
                    _falling[id] = step.Speed;
                }
            }
        }

        private void SyncVisuals(AshTerrain terrain)
        {
            int frame = Time.frameCount;
            // Exponential approach: frame-rate independent, and fast enough to keep up with a falling ember.
            float follow = 1f - Mathf.Exp(-_settings.FollowRate * Time.deltaTime);
            IReadOnlyList<LooseEmber> loose = terrain.LooseEmbers;
            for (int i = 0; i < loose.Count; i++)
            {
                LooseEmber item = loose[i];
                Vector3 target = _pile.transform.TransformPoint(item.Ember.Position);
                if (!_visuals.TryGetValue(item.Id, out Renderer visual))
                {
                    visual = TakeVisual(item.Ember.TierIndex);
                    _visuals[item.Id] = visual;
                    // Starts where it lay, so an ember uncovered by a dig is seen coming out of the ash.
                    visual.transform.position = _pile.transform.TransformPoint(item.CameFrom);
                }

                visual.transform.position = Vector3.Lerp(visual.transform.position, target, follow);
                _seenFrame[item.Id] = frame;
            }

            if (_visuals.Count == loose.Count)
            {
                return;
            }

            // Some ember left without going through this view (e.g. a scoop took it): drop its sphere.
            _scratchIds.Clear();
            foreach (KeyValuePair<int, int> seen in _seenFrame)
            {
                if (seen.Value != frame)
                {
                    _scratchIds.Add(seen.Key);
                }
            }

            foreach (int id in _scratchIds)
            {
                ReleaseVisual(id);
                _falling.Remove(id);
            }
        }

        private Renderer TakeVisual(int tierIndex)
        {
            Renderer visual = _pool.Count > 0 ? _pool.Pop() : Instantiate(_emberPrefab, _container);
            visual.name = "Loose Ember";
            visual.gameObject.SetActive(true);
            EmberTier tier = tierIndex >= 0 && tierIndex < _tierCatalog.Count ? _tierCatalog[tierIndex] : null;
            if (tier != null)
            {
                visual.transform.localScale = Vector3.one * tier.VisualScale;
                _propertyBlock.SetColor(BaseColorId, tier.Color);
                _propertyBlock.SetColor(EmissionColorId, tier.EmissionColor);
                visual.SetPropertyBlock(_propertyBlock);
            }

            return visual;
        }

        private void ReleaseVisual(int id)
        {
            _seenFrame.Remove(id);
            if (!_visuals.TryGetValue(id, out Renderer visual))
            {
                return;
            }

            _visuals.Remove(id);
            visual.gameObject.SetActive(false);
            _pool.Push(visual);
        }

        private float RadiusOf(int tierIndex)
        {
            EmberTier tier = tierIndex >= 0 && tierIndex < _tierCatalog.Count ? _tierCatalog[tierIndex] : null;
            return tier != null ? tier.VisualScale * 0.5f : 0.05f;
        }
    }
}
