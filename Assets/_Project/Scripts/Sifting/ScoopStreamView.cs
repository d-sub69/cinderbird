using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// The visible part of a pour: a puff of dust at the dig spot and a stream of ash clumps arcing into the sieve,
    /// landing by the time the load arrives. Clumps follow the sieve as the player moves.
    /// </summary>
    public sealed class ScoopStreamView : MonoBehaviour
    {
        // Each clump flies for this share of the pour; the rest is used to stagger their starts.
        private const float FlightShare = 0.6f;
        private const int MinClumps = 6;
        // Clumps leave from around the dig spot and land around the sieve's middle, not all on one point.
        private const float DigSpread = 0.15f;
        private const float LandingSpreadFraction = 0.6f;

        [SerializeField] private SieveFiller _filler;
        [SerializeField] private SieveSettings _settings;
        [Tooltip("The sieve floor (its local XZ plane); clumps land relative to it so they follow the sieve.")]
        [SerializeField] private Transform _sieveFloor;
        [Tooltip("Small grey sphere with the ash material and no collider.")]
        [SerializeField] private Renderer _clumpPrefab;
        [Tooltip("Optional dust burst at the dig spot; emission is triggered from code.")]
        [SerializeField] private ParticleSystem _digPuff;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private Transform[] _clumps;
        private Renderer[] _clumpRenderers;
        private MaterialPropertyBlock _propertyBlock;
        private Vector3[] _fromPositions;
        private Vector3[] _landingOffsets;
        private float[] _startTimes;
        private int _activeCount;
        private float _flightTime;
        private readonly System.Random _random = new System.Random();

        private void Awake()
        {
            if (_filler == null || _settings == null || _sieveFloor == null || _clumpPrefab == null)
            {
                Debug.LogError($"{nameof(ScoopStreamView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreatePool(_settings.MaxClumps);
        }

        private void OnEnable()
        {
            if (_clumps != null)
            {
                _filler.FillStarted += OnFillStarted;
            }
        }

        private void OnDisable()
        {
            if (_clumps == null)
            {
                return;
            }

            _filler.FillStarted -= OnFillStarted;
            // The tool was stowed mid-pour; the load lands at once, so the stream just stops.
            HideAll();
        }

        private void OnDestroy()
        {
            // The pool lives outside the sieve (clumps fly in world space), so clean it up explicitly.
            if (_clumps != null && _clumps.Length > 0 && _clumps[0] != null)
            {
                Destroy(_clumps[0].parent.gameObject);
            }
        }

        private void Update()
        {
            if (_activeCount == 0)
            {
                return;
            }

            float now = Time.time;
            for (int i = 0; i < _activeCount; i++)
            {
                AdvanceClump(i, now - _startTimes[i]);
            }

            // All clumps start in order and fly equally long, so the last one landing ends the stream.
            if (now - _startTimes[_activeCount - 1] >= _flightTime)
            {
                HideAll();
            }
        }

        private void CreatePool(int count)
        {
            // World-space container: clumps must not move with the sieve once they've left the pile.
            Transform container = new GameObject("Ash Clumps").transform;
            _clumps = new Transform[count];
            _clumpRenderers = new Renderer[count];
            _fromPositions = new Vector3[count];
            _landingOffsets = new Vector3[count];
            _startTimes = new float[count];

            for (int i = 0; i < count; i++)
            {
                Renderer clump = Instantiate(_clumpPrefab, container);
                clump.name = $"Ash Clump {i}";
                clump.gameObject.SetActive(false);
                _clumps[i] = clump.transform;
                _clumpRenderers[i] = clump;
            }
        }

        private void OnFillStarted(float volume, Vector3 digPoint)
        {
            HideAll();

            float liters = volume * 1000f;
            _activeCount = Mathf.Clamp(Mathf.RoundToInt(liters * _settings.ClumpsPerLiter), MinClumps, _clumps.Length);
            _flightTime = _settings.FillDuration * FlightShare;
            float staggerWindow = Mathf.Max(0f, _settings.FillDuration - _flightTime);
            float now = Time.time;
            float landingRadius = _settings.Radius * LandingSpreadFraction;
            // Clumps are made of the ash being scooped, so they share the load's layer color.
            _propertyBlock.SetColor(BaseColorId, _filler.IncomingAshColor);

            for (int i = 0; i < _activeCount; i++)
            {
                _startTimes[i] = now + staggerWindow * i / Mathf.Max(1, _activeCount - 1);
                _fromPositions[i] = digPoint + RandomInSphere(DigSpread);
                Vector2 landing = RandomInDisc(landingRadius);
                _landingOffsets[i] = new Vector3(landing.x, 0f, landing.y);
                _clumps[i].localScale = Vector3.one * (_settings.ClumpSize * Mathf.Lerp(0.7f, 1.3f, (float)_random.NextDouble()));
                _clumpRenderers[i].SetPropertyBlock(_propertyBlock);
            }

            PuffDust(digPoint);
        }

        private void AdvanceClump(int index, float elapsed)
        {
            Transform clump = _clumps[index];
            float progress = elapsed / _flightTime;
            if (progress < 0f || progress >= 1f)
            {
                if (clump.gameObject.activeSelf)
                {
                    clump.gameObject.SetActive(false);
                }

                return;
            }

            if (!clump.gameObject.activeSelf)
            {
                clump.gameObject.SetActive(true);
            }

            // Recomputed every frame so the stream lands in the sieve even while the player walks or turns.
            Vector3 target = _sieveFloor.TransformPoint(_landingOffsets[index]);
            Vector3 position = Vector3.Lerp(_fromPositions[index], target, progress);
            // Straight line plus a parabola (4p(1-p) peaks at 1 halfway) gives a simple toss.
            position.y += _settings.ClumpArcHeight * 4f * progress * (1f - progress);
            clump.position = position;
        }

        private void PuffDust(Vector3 digPoint)
        {
            if (_digPuff == null || _settings.DigPuffParticles <= 0)
            {
                return;
            }

            _digPuff.transform.position = digPoint;
            // Only the color is overridden; position and everything else still come from the particle system.
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams { startColor = _filler.IncomingDustColor };
            _digPuff.Emit(emit, _settings.DigPuffParticles);
        }

        private void HideAll()
        {
            for (int i = 0; i < _activeCount; i++)
            {
                _clumps[i].gameObject.SetActive(false);
            }

            _activeCount = 0;
        }

        private Vector3 RandomInSphere(float radius)
        {
            return new Vector3(
                ((float)_random.NextDouble() * 2f - 1f) * radius,
                ((float)_random.NextDouble() * 2f - 1f) * radius,
                ((float)_random.NextDouble() * 2f - 1f) * radius);
        }

        private Vector2 RandomInDisc(float radius)
        {
            // Square root keeps points evenly spread instead of bunching at the center.
            float distance = radius * Mathf.Sqrt((float)_random.NextDouble());
            float angle = (float)_random.NextDouble() * Mathf.PI * 2f;
            return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
        }
    }
}
