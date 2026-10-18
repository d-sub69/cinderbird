using System;
using Cinderbird.Core;
using Cinderbird.Economy;
using Cinderbird.Nest;
using Cinderbird.Player;
using Cinderbird.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.Powers
{
    /// <summary>
    /// The Warm Wind: hold the power key (F) and the wind gathers, the camera trembling a little; held long enough, let go
    /// and a gust flies to the ash under the crosshair and blows its top off (<see cref="VoxelTerrain.Blow"/>): the ash is gone, the
    /// embers it covered lie bare on the surface. Nothing shows beforehand where it will strike. Let go too soon and only a
    /// puff of wind comes: it looks the same but blows nothing away and costs nothing. Comes with a Heat level,
    /// then rests for a while after each gust.
    /// </summary>
    public sealed class WarmWind : MonoBehaviour
    {
        [SerializeField] private WarmWindSettings _settings;
        [Tooltip("The power key (F): hold to gather, release to blow.")]
        [SerializeField] private InputActionReference _powerAction;
        [Tooltip("Where the player looks from: the gust goes where the crosshair points.")]
        [SerializeField] private Transform _camera;
        [SerializeField] private VoxelTerrain _pile;
        [SerializeField] private NestHeat _heat;
        [SerializeField] private CameraShake _shake;
        [Tooltip("Optional: while something else owns the buttons, the key does nothing.")]
        [SerializeField] private InputFocus _focus;
        [Tooltip("Streaks of wind flying to the spot. World-space simulation.")]
        [SerializeField] private ParticleSystem _gust;
        [Tooltip("Ash lifted off the spot. World-space simulation.")]
        [SerializeField] private ParticleSystem _dust;
        [Tooltip("Optional: the upgrades bought, for the two below.")]
        [SerializeField] private Upgrades _upgrades;
        [Tooltip("Optional: its cooldown upgrade; each level's value is the cooldown in seconds.")]
        [SerializeField] private TableUpgrade _cooldownUpgrade;
        [Tooltip("Optional: its radius upgrade; each level's value is the radius in meters.")]
        [SerializeField] private TableUpgrade _radiusUpgrade;

        private PowerCooldown _cooldown;
        private bool _gathering;
        private float _gatherTime;
        private bool _gustInFlight;
        private Vector3 _strikePoint;
        private Vector3 _gustDirection;
        private float _strikeTime;
        private int _shownSeconds = -1;

        /// <summary>A short message for the player (not ready yet, nothing to blow there).</summary>
        public event Action<string> Notice;

        /// <summary>Whether it is unlocked, ready or not changed (for the HUD).</summary>
        public event Action Changed;

        /// <summary>Fired when a gust struck the pile, with the ash it blew away (for the pacing log).</summary>
        public event Action<DigResult> Gusted;

        public bool IsUnlocked => _heat != null && _heat.Level >= _settings.RequiredHeatLevel;

        /// <summary>Seconds until it can be called again; 0 when ready.</summary>
        public float CooldownRemaining => _cooldown != null ? _cooldown.Remaining(Time.time) : 0f;

        private void Awake()
        {
            if (_settings == null || _powerAction == null || _camera == null || _pile == null || _heat == null || _shake == null ||
                _gust == null || _dust == null)
            {
                Debug.LogError($"{nameof(WarmWind)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _cooldown = new PowerCooldown();
        }

        private void OnEnable()
        {
            if (_cooldown == null)
            {
                return;
            }

            _powerAction.action.Enable();
            _heat.LevelReached += OnLevelReached;
            if (_upgrades != null)
            {
                _upgrades.Levels.Changed += OnUpgradeChanged;
            }
        }

        private void OnDisable()
        {
            if (_cooldown == null)
            {
                return;
            }

            _heat.LevelReached -= OnLevelReached;
            if (_upgrades != null)
            {
                _upgrades.Levels.Changed -= OnUpgradeChanged;
            }

            StopGathering();
        }

        private void Update()
        {
            if (Time.deltaTime <= 0f)
            {
                return;
            }

            if (_gustInFlight && Time.time >= _strikeTime)
            {
                Strike();
            }

            ReportCooldown();
            bool usable = Cursor.lockState == CursorLockMode.Locked && (_focus == null || !_focus.IsTaken);
            if (!usable)
            {
                StopGathering();
                return;
            }

            InputAction action = _powerAction.action;
            if (action.WasPressedThisFrame())
            {
                StartGathering();
            }

            if (_gathering)
            {
                _gatherTime += Time.deltaTime;
                _shake.SetHum(_settings.GatherShake * Mathf.Clamp01(_gatherTime / _settings.GatherSeconds));
                if (action.WasReleasedThisFrame() || !action.IsPressed())
                {
                    StopGathering();
                    Release();
                }
            }
        }

        private void StartGathering()
        {
            if (!IsUnlocked)
            {
                Notice?.Invoke($"The nest must reach Heat level {_settings.RequiredHeatLevel} to call the Warm Wind");
                return;
            }

            if (!_cooldown.IsReady(Time.time) || _gustInFlight)
            {
                Notice?.Invoke($"The Warm Wind gathers again in {Mathf.CeilToInt(_cooldown.Remaining(Time.time))} s");
                return;
            }

            _gathering = true;
            _gatherTime = 0f;
        }

        private void StopGathering()
        {
            if (!_gathering)
            {
                return;
            }

            _gathering = false;
            _shake.SetHum(0f);
        }

        // Held long enough, the gust leaves now and strikes when it arrives; it only blows ash, so it needs ash to aim at. Let
        // go sooner and a puff flies out the same way, but blows nothing away and costs nothing.
        private void Release()
        {
            Ray ray = new Ray(_camera.position, _camera.forward);
            if (_gatherTime < _settings.GatherSeconds)
            {
                Puff(ray);
                return;
            }

            if (!Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ||
                hit.collider.GetComponentInParent<VoxelTerrain>() != _pile)
            {
                Notice?.Invoke("The wind finds no ash there");
                return;
            }

            _cooldown.Use(Time.time, CurrentCooldown);
            _strikePoint = hit.point;
            _gustDirection = ray.direction;
            _strikeTime = Time.time + hit.distance / _settings.GustSpeed;
            _gustInFlight = true;
            EmitGust(ray.origin + ray.direction * 0.8f, hit.point, _settings.GustStreaks);
            Changed?.Invoke();
        }

        // A puff: the same streaks, fewer of them, fading out a few meters ahead.
        private void Puff(Ray ray)
        {
            float distance = Physics.Raycast(ray, out RaycastHit hit, _settings.Reach, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? Mathf.Min(hit.distance, 5f)
                : 5f;
            EmitGust(ray.origin + ray.direction * 0.8f, ray.origin + ray.direction * distance, _settings.GustStreaks / 3);
            _shake.Kick(_settings.GatherShake, 0.3f);
            Notice?.Invoke("Hold F a little longer to gather the wind");
        }

        private void Strike()
        {
            _gustInFlight = false;
            DigResult blown = _pile.Blow(_strikePoint, CurrentRadius, _settings.Depth);
            if (blown.RemovedAnyAsh)
            {
                Gusted?.Invoke(blown);
            }

            EmitDust(_strikePoint);
            _shake.Kick(_settings.GustShake, _settings.GustShakeSeconds);
        }

        // Streaks from just in front of the player to the spot, spread a little and arriving together.
        private void EmitGust(Vector3 from, Vector3 to, int streaks)
        {
            Vector3 path = to - from;
            float seconds = Mathf.Max(0.05f, path.magnitude / _settings.GustSpeed);
            for (int i = 0; i < streaks; i++)
            {
                Vector3 start = from + UnityEngine.Random.insideUnitSphere * 0.6f;
                Vector3 end = to + UnityEngine.Random.insideUnitSphere * (CurrentRadius * 0.6f);
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = start,
                    velocity = (end - start) / seconds,
                    startLifetime = seconds * UnityEngine.Random.Range(1f, 1.25f),
                };
                _gust.Emit(emit, 1);
            }
        }

        // Ash lifted off the whole area, carried on in the gust's direction and up.
        private void EmitDust(Vector3 center)
        {
            Vector3 along = new Vector3(_gustDirection.x, 0f, _gustDirection.z).normalized;
            for (int i = 0; i < _settings.DustPuffs; i++)
            {
                Vector2 disc = UnityEngine.Random.insideUnitCircle * CurrentRadius;
                Vector3 position = center + new Vector3(disc.x, 0.1f, disc.y);
                Vector3 outward = new Vector3(disc.x, 0f, disc.y) * 0.6f;
                ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams
                {
                    position = position,
                    velocity = along * UnityEngine.Random.Range(2f, 5f) + outward + Vector3.up * UnityEngine.Random.Range(0.8f, 2.2f),
                };
                _dust.Emit(emit, 1);
            }
        }

        // Upgrades replace the settings' values: each level's value is the cooldown (seconds) or the radius (meters).
        private float CurrentCooldown => _upgrades != null && _cooldownUpgrade != null
            ? _cooldownUpgrade.ValueAt(_upgrades.LevelOf(_cooldownUpgrade))
            : _settings.Cooldown;

        private float CurrentRadius => _upgrades != null && _radiusUpgrade != null
            ? _radiusUpgrade.ValueAt(_upgrades.LevelOf(_radiusUpgrade))
            : _settings.Radius;

        // The HUD shows the cooldown in whole seconds: tell it when the number changes.
        private void ReportCooldown()
        {
            int seconds = Mathf.CeilToInt(_cooldown.Remaining(Time.time));
            if (seconds != _shownSeconds)
            {
                _shownSeconds = seconds;
                Changed?.Invoke();
            }
        }

        // A shorter cooldown bought while the wind rests: it is ready again at once.
        private void OnUpgradeChanged(string id)
        {
            if (_cooldownUpgrade == null || id != _cooldownUpgrade.Id || _cooldown.IsReady(Time.time))
            {
                return;
            }

            _cooldown.Reset();
            Notice?.Invoke("The Warm Wind is ready again");
            Changed?.Invoke();
        }

        private void OnLevelReached(int level)
        {
            if (level == _settings.RequiredHeatLevel)
            {
                Notice?.Invoke("The nest is warm enough to call the Warm Wind: hold F, let go to blow the ash off");
            }

            Changed?.Invoke();
        }
    }
}
