using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Offerings
{
    /// <summary>
    /// Thin adapter between the offerings and the mountain:
    /// <list type="bullet">
    /// <item>buries every offering not yet uncovered at its spot when the mountain is built;</item>
    /// <item>when a dig reaches one, shows it lying in the hole (its model), where it stays until the player picks it up:
    /// it rides the ash under it like a loose ember, never buried again, and falls if its support is dug away;</item>
    /// <item>picking it up (E) hands it to the inspect view (<see cref="PickUpRequested"/>); only "Place in the shrine" there
    /// takes it off the pile.</item>
    /// </list>
    /// </summary>
    public sealed class OfferingFinder : MonoBehaviour
    {
        // How far up through ash that came over it an offering is looked for its surface.
        private const float MaxCoverSearch = 3f;
        // Seconds between checks of every shown offering against the ash (also checked whenever the ash changes).
        private const float CheckInterval = 0.5f;

        [SerializeField] private Shrine _shrine;
        [SerializeField] private VoxelTerrain _pile;
        [Tooltip("Falling and following, as for loose embers.")]
        [SerializeField] private LooseEmberSettings _fallSettings;
        [Tooltip("What a falling offering can land on besides ash (the ground, rocks).")]
        [SerializeField] private LayerMask _groundMask = ~0;

        private sealed class Shown
        {
            public GameObject Model;
            public float Rest = LooseEmber.UnknownRest;
            public bool Falling;
            public float Speed;
        }

        /// <summary>
        /// Fired when the player picks up an offering lying on the pile (its index, and where and how it lay in the world): the
        /// inspect view takes over, then calls <see cref="Shrine.PickUp"/> to keep it or <see cref="PutBack"/> to leave it.
        /// </summary>
        public event System.Action<int, Vector3, Quaternion> PickUpRequested;

        private readonly Dictionary<int, Shown> _shown = new Dictionary<int, Shown>();
        private readonly List<int> _scratch = new List<int>();
        private AshFallSurface _surface;
        private Transform _container;
        private bool _checkPending = true;
        private int _pickingUp = -1;
        private float _nextCheckTime;

        private void Awake()
        {
            if (_shrine == null || _pile == null || _fallSettings == null)
            {
                Debug.LogError($"{nameof(OfferingFinder)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _surface = new AshFallSurface(_pile, _groundMask);
            _container = new GameObject("Offerings on the pile").transform;
        }

        private void OnEnable()
        {
            _pile.Built += BuryAll;
            _pile.FindsUncovered += OnFindsUncovered;
            _pile.Settled += OnSettled;
            _pile.Dug += OnDug;
            _shrine.Collection.Changed += OnCollectionChanged;
            if (_pile.IsBuilt)
            {
                BuryAll();
            }
        }

        private void OnDisable()
        {
            _pile.Built -= BuryAll;
            _pile.FindsUncovered -= OnFindsUncovered;
            _pile.Settled -= OnSettled;
            _pile.Dug -= OnDug;
            _shrine.Collection.Changed -= OnCollectionChanged;
        }

        private void OnDestroy()
        {
            if (_container != null)
            {
                Destroy(_container.gameObject);
            }
        }

        /// <summary>The player pressed E on an offering lying on the pile: it leaves the pile to be looked at closely.</summary>
        public void PickUp(int index)
        {
            if (_pickingUp >= 0 || !_shown.TryGetValue(index, out Shown shown))
            {
                return;
            }

            _pickingUp = index;
            shown.Model.SetActive(false);
            PickUpRequested?.Invoke(index, shown.Model.transform.position, shown.Model.transform.rotation);
        }

        /// <summary>The player didn't keep it: it lies where it was again.</summary>
        public void PutBack(int index)
        {
            if (_pickingUp == index)
            {
                _pickingUp = -1;
            }

            if (_shown.TryGetValue(index, out Shown shown))
            {
                shown.Model.SetActive(true);
            }
        }

        private void Update()
        {
            AshTerrain terrain = _pile.Terrain;
            if (terrain == null || _shown.Count == 0)
            {
                return;
            }

            if (_checkPending || Time.time >= _nextCheckTime)
            {
                _checkPending = false;
                _nextCheckTime = Time.time + CheckInterval;
                CheckAgainstAsh(terrain);
            }

            AdvanceFalls(Time.deltaTime);
            SyncModels();
        }

        private void OnSettled(Bounds changed)
        {
            _checkPending = true;
        }

        private void OnDug(DigResult result, Vector3 center, float radius)
        {
            _checkPending = true;
        }

        private void OnFindsUncovered(IReadOnlyList<BuriedFind> finds)
        {
            for (int i = 0; i < finds.Count; i++)
            {
                if (_shrine.Uncover(finds[i].Id, finds[i].Position))
                {
                    Show(finds[i].Id);
                }
            }

            _checkPending = true;
        }

        // A save was loaded, or an offering was picked up: the pile shows exactly the offerings lying on it.
        private void OnCollectionChanged(int index)
        {
            AshTerrain terrain = _pile.Terrain;
            OfferingCollection collection = _shrine.Collection;
            for (int i = 0; i < collection.Count; i++)
            {
                if (index >= 0 && i != index)
                {
                    continue;
                }

                OfferingState state = collection.StateOf(i);
                if (state != OfferingState.Buried && terrain != null)
                {
                    terrain.RemoveFind(i);
                }

                if (state == OfferingState.OnPile)
                {
                    Show(i);
                }
                else
                {
                    Hide(i);
                }
            }

            _checkPending = true;
        }

        private void Show(int index)
        {
            if (_shown.ContainsKey(index))
            {
                return;
            }

            OfferingDefinition offering = _shrine.Catalog[index];
            GameObject model = offering.Model != null
                ? Instantiate(offering.Model, _container)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);
            model.transform.SetParent(_container, false);
            model.name = offering.DisplayName;
            if (offering.Model == null)
            {
                model.transform.localScale = Vector3.one * offering.Size;
            }

            model.transform.position = _pile.transform.TransformPoint(_shrine.PilePositionOf(index));
            model.AddComponent<PileOffering>().Initialize(this, index, offering.DisplayName);
            _shown[index] = new Shown { Model = model };
        }

        private void Hide(int index)
        {
            if (_pickingUp == index)
            {
                _pickingUp = -1;
            }

            if (!_shown.TryGetValue(index, out Shown shown))
            {
                return;
            }

            Destroy(shown.Model);
            _shown.Remove(index);
        }

        // Keeps every offering at its rest above the ash (lifted by ash that came over it, let down where it ran away),
        // and starts a fall for any with neither ash nor ground under it. Never buries one.
        private void CheckAgainstAsh(AshTerrain terrain)
        {
            foreach (KeyValuePair<int, Shown> entry in _shown)
            {
                Shown shown = entry.Value;
                if (shown.Falling)
                {
                    continue;
                }

                Vector3 center = _shrine.PilePositionOf(entry.Key);
                float radius = _shrine.Catalog[entry.Key].Size * 0.5f;
                if (!LooseEmberRules.TryFindSurface(terrain.World, center, radius, MaxCoverSearch, out float surface))
                {
                    if (!_surface.IsOnGround(center, radius))
                    {
                        shown.Falling = true;
                        shown.Speed = 0f;
                        shown.Rest = LooseEmber.UnknownRest;
                    }

                    continue;
                }

                float y = LooseEmberRules.Settle(center.y, surface, shown.Rest, radius, out float rest);
                shown.Rest = rest;
                _shrine.SetPilePosition(entry.Key, new Vector3(center.x, y, center.z));
            }
        }

        private void AdvanceFalls(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            FallParameters fall = _fallSettings.Fall;
            _scratch.Clear();
            _scratch.AddRange(_shown.Keys);
            foreach (int index in _scratch)
            {
                Shown shown = _shown[index];
                if (!shown.Falling)
                {
                    continue;
                }

                float radius = _shrine.Catalog[index].Size * 0.5f;
                FallStep step = LooseEmberFall.Advance(_shrine.PilePositionOf(index), shown.Speed, radius, deltaTime, fall, _surface);
                _shrine.SetPilePosition(index, step.Position);
                shown.Speed = step.Speed;
                if (step.Resting)
                {
                    shown.Falling = false;
                    _checkPending = true;
                }
            }
        }

        private void SyncModels()
        {
            float follow = 1f - Mathf.Exp(-_fallSettings.FollowRate * Time.deltaTime);
            foreach (KeyValuePair<int, Shown> entry in _shown)
            {
                Transform model = entry.Value.Model.transform;
                Vector3 target = _pile.transform.TransformPoint(_shrine.PilePositionOf(entry.Key));
                model.position = Vector3.Lerp(model.position, target, follow);
            }
        }

        private void BuryAll()
        {
            AshTerrain terrain = _pile.Terrain;
            OfferingCatalog catalog = _shrine.Catalog;
            if (terrain == null || catalog == null)
            {
                return;
            }

            OfferingCollection collection = _shrine.Collection;
            for (int i = 0; i < catalog.Count; i++)
            {
                if (catalog[i] == null || collection.StateOf(i) != OfferingState.Buried)
                {
                    continue;
                }

                OfferingPlacement placement = catalog[i].Placement;
                Vector3 spot = placement.HorizontalPosition(_pile.PileRadius);
                spot.y = terrain.NaturalSurfaceHeightAt(spot) - placement.Depth;
                terrain.PlaceFind(i, spot);
            }
        }
    }
}
