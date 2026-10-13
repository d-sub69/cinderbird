using System.Collections.Generic;
using Cinderbird.Embers;
using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Shows the embers a scoop took flying into the sieve with the ash: one glowing dot per ember, in its tier's
    /// color, leaving from where it lay and landing in the sieve during the pour. Once landed they're under the
    /// ash, where the sieve's own glimmers take over.
    /// </summary>
    public sealed class EmberStreamView : MonoBehaviour
    {
        private const int MaxFlying = 24;
        // Same toss as the ash clumps: each ember flies for this share of the pour, starts spread over the rest.
        private const float FlightShare = 0.6f;
        // Embers leave from near the dig spot (they lay under it), not from deep inside the pile.
        private const float StartSpread = 0.25f;
        private const float LandingSpreadFraction = 0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [SerializeField] private SieveFiller _filler;
        [SerializeField] private SieveSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("The sieve floor (its local XZ plane); embers land relative to it so they follow the sieve.")]
        [SerializeField] private Transform _sieveFloor;
        [Tooltip("Small emissive sphere with no collider (the sieve's ember visual works).")]
        [SerializeField] private Renderer _emberPrefab;
        [Tooltip("The pile the embers come from; their positions are in its local space.")]
        [SerializeField] private VoxelTerrain _pile;

        private readonly System.Random _random = new System.Random();
        private Renderer[] _visuals;
        private Vector3[] _from;
        private Vector3[] _landing;
        private float[] _startTimes;
        private MaterialPropertyBlock _propertyBlock;
        private int _activeCount;
        private float _flightTime;

        private void Awake()
        {
            if (_filler == null || _settings == null || _tierCatalog == null || _sieveFloor == null || _emberPrefab == null || _pile == null)
            {
                Debug.LogError($"{nameof(EmberStreamView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreatePool();
        }

        private void OnEnable()
        {
            if (_visuals != null)
            {
                _filler.FillStarted += OnFillStarted;
            }
        }

        private void OnDisable()
        {
            if (_visuals == null)
            {
                return;
            }

            _filler.FillStarted -= OnFillStarted;
            // The tool was stowed mid-pour; the load lands at once, so the flight just stops.
            HideAll();
        }

        private void OnDestroy()
        {
            // The pool lives outside the sieve (embers fly in world space), so clean it up explicitly.
            if (_visuals != null && _visuals.Length > 0 && _visuals[0] != null)
            {
                Destroy(_visuals[0].transform.parent.gameObject);
            }
        }

        private void CreatePool()
        {
            // World-space container: embers must not move with the sieve once they've left the pile.
            Transform container = new GameObject("Flying Embers").transform;
            _visuals = new Renderer[MaxFlying];
            _from = new Vector3[MaxFlying];
            _landing = new Vector3[MaxFlying];
            _startTimes = new float[MaxFlying];
            for (int i = 0; i < MaxFlying; i++)
            {
                Renderer visual = Instantiate(_emberPrefab, container);
                visual.name = $"Flying Ember {i}";
                visual.gameObject.SetActive(false);
                _visuals[i] = visual;
            }
        }

        private void OnFillStarted(float volume, Vector3 digPoint)
        {
            HideAll();
            IReadOnlyList<Ember> embers = _filler.IncomingEmbers;
            _activeCount = Mathf.Min(embers.Count, MaxFlying);
            if (_activeCount == 0)
            {
                return;
            }

            _flightTime = _settings.FillDuration * FlightShare;
            float staggerWindow = Mathf.Max(0f, _settings.FillDuration - _flightTime);
            float now = Time.time;
            float landingRadius = _settings.Radius * LandingSpreadFraction;
            for (int i = 0; i < _activeCount; i++)
            {
                Ember ember = embers[i];
                // Leaves from where it lay, seen from above: its spot under the dig, lifted to the surface.
                Vector3 lay = _pile.transform.TransformPoint(ember.Position);
                Vector3 offset = Vector3.ClampMagnitude(new Vector3(lay.x - digPoint.x, 0f, lay.z - digPoint.z), StartSpread);
                _from[i] = digPoint + offset;
                Vector2 landing = RandomInDisc(landingRadius);
                _landing[i] = new Vector3(landing.x, 0f, landing.y);
                // Spread over the middle of the pour, so the embers come out among the ash, not first or last.
                _startTimes[i] = now + staggerWindow * (i + 0.5f) / _activeCount;
                Style(_visuals[i], ember.TierIndex);
            }
        }

        private void Style(Renderer visual, int tierIndex)
        {
            EmberTier tier = tierIndex >= 0 && tierIndex < _tierCatalog.Count ? _tierCatalog[tierIndex] : null;
            if (tier == null)
            {
                return;
            }

            visual.transform.localScale = Vector3.one * tier.VisualScale;
            _propertyBlock.SetColor(BaseColorId, tier.Color);
            _propertyBlock.SetColor(EmissionColorId, tier.EmissionColor);
            visual.SetPropertyBlock(_propertyBlock);
        }

        private void Update()
        {
            if (_activeCount == 0)
            {
                return;
            }

            float now = Time.time;
            bool anyFlying = false;
            for (int i = 0; i < _activeCount; i++)
            {
                anyFlying |= Advance(i, now - _startTimes[i]);
            }

            if (!anyFlying && now - _startTimes[_activeCount - 1] >= _flightTime)
            {
                HideAll();
            }
        }

        /// <returns>False once this ember has landed.</returns>
        private bool Advance(int index, float elapsed)
        {
            GameObject visual = _visuals[index].gameObject;
            float progress = elapsed / _flightTime;
            if (progress < 0f || progress >= 1f)
            {
                if (visual.activeSelf)
                {
                    visual.SetActive(false);
                }

                return progress < 0f;
            }

            if (!visual.activeSelf)
            {
                visual.SetActive(true);
            }

            // Recomputed every frame so embers land in the sieve even while the player walks or turns.
            Vector3 target = _sieveFloor.TransformPoint(_landing[index]);
            Vector3 position = Vector3.Lerp(_from[index], target, progress);
            position.y += _settings.ClumpArcHeight * 4f * progress * (1f - progress);
            _visuals[index].transform.position = position;
            return true;
        }

        private void HideAll()
        {
            for (int i = 0; i < _activeCount; i++)
            {
                _visuals[i].gameObject.SetActive(false);
            }

            _activeCount = 0;
        }

        private Vector2 RandomInDisc(float radius)
        {
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Sqrt((float)_random.NextDouble()) * radius;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
