using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Drives the nest's flame particles and light from a <see cref="FireFlare"/>: the fire grows with
    /// the Heat that has landed so far and flares briefly as each ember arrives.
    /// </summary>
    public sealed class NestFireView : MonoBehaviour
    {
        [SerializeField] private NestController _nest;
        [SerializeField] private NestPourEffect _pourEffect;
        [SerializeField] private NestSettings _settings;
        [SerializeField] private ParticleSystem _flames;
        [SerializeField] private Light _light;

        private FireFlare _fire;
        private ParticleSystem.MinMaxCurve _baseStartSize;
        // Counted from landings rather than read from the wallet, so the fire grows as embers arrive, not on the key press.
        private int _landedHeat;

        private void Awake()
        {
            if (_nest == null || _pourEffect == null || _settings == null || _flames == null || _light == null)
            {
                Debug.LogError($"{nameof(NestFireView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _fire = new FireFlare(_settings.FlareHalfLife, _settings.MaxFlare);
            _baseStartSize = _flames.main.startSize;
        }

        private void OnEnable()
        {
            if (_fire != null)
            {
                _pourEffect.EmberLanded += OnEmberLanded;
                _nest.HeatRestored += OnHeatRestored;
            }
        }

        private void OnDisable()
        {
            if (_fire != null)
            {
                _pourEffect.EmberLanded -= OnEmberLanded;
                _nest.HeatRestored -= OnHeatRestored;
            }
        }

        /// <summary>Makes the fire flare up, as a big moment (the phoenix taking shape) would.</summary>
        public void Flare(float amount)
        {
            _fire?.AddFlare(amount);
        }

        private void OnHeatRestored(int heat)
        {
            _landedHeat = heat;
        }

        private void Update()
        {
            _fire.Tick(Time.deltaTime);
            _fire.SetProgress(_nest.HeatGoal > 0 ? (float)_landedHeat / _nest.HeatGoal : 0f);
            Apply();
        }

        private void OnEmberLanded(int tierIndex)
        {
            int heat = _nest.HeatOf(tierIndex);
            _landedHeat += heat;
            _fire.AddFlare(heat * _settings.FlarePerHeat);
        }

        private void Apply()
        {
            float level = _fire.BaseLevel;
            float flare = _fire.Flare;

            ParticleSystem.EmissionModule emission = _flames.emission;
            emission.rateOverTime = Mathf.Lerp(_settings.MinEmissionRate, _settings.MaxEmissionRate, level)
                                    + flare * _settings.FlareEmissionBoost;

            float sizeMultiplier = Mathf.Lerp(_settings.MinSizeMultiplier, _settings.MaxSizeMultiplier, level)
                                   + flare * _settings.FlareSizeBoost;
            ParticleSystem.MainModule main = _flames.main;
            main.startSize = Scale(_baseStartSize, sizeMultiplier);

            _light.intensity = Mathf.Lerp(_settings.MinLightIntensity, _settings.MaxLightIntensity, level)
                               + flare * _settings.FlareLightBoost;
        }

        // Scales whatever Start Size mode was chosen in the Inspector instead of overwriting it with a constant.
        private static ParticleSystem.MinMaxCurve Scale(ParticleSystem.MinMaxCurve curve, float factor)
        {
            switch (curve.mode)
            {
                case ParticleSystemCurveMode.Constant:
                    return new ParticleSystem.MinMaxCurve(curve.constant * factor);
                case ParticleSystemCurveMode.TwoConstants:
                    return new ParticleSystem.MinMaxCurve(curve.constantMin * factor, curve.constantMax * factor);
                default:
                    curve.curveMultiplier *= factor;
                    return curve;
            }
        }
    }
}
