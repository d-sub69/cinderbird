using System;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>
    /// Thin adapter: the phoenix's silhouette in the nest fire. Flames are emitted from a bird-shaped mesh (the particle
    /// system's shape), and the stage the nest's Heat level has reached sets how big, bright and lively it is
    /// (<see cref="PhoenixLook"/>). A new stage grows in over a few seconds with a flare of the fire; loading a save shows
    /// the stage at once. It breathes, beats its wings, glances aside now and then, and always faces the player.
    /// </summary>
    public sealed class PhoenixView : MonoBehaviour
    {
        [SerializeField] private PhoenixSettings _settings;
        [SerializeField] private NestHeat _heat;
        [Tooltip("The flames shaped like the bird: its shape is the phoenix mesh; world-space simulation.")]
        [SerializeField] private ParticleSystem _flames;
        [Tooltip("The player's camera: the silhouette turns to face it.")]
        [SerializeField] private Transform _viewer;
        [Tooltip("Optional: the nest fire flares when a stage comes.")]
        [SerializeField] private NestFireView _fire;

        private int _stage = -1;
        private PhoenixLook _from;
        private PhoenixLook _to;
        private float _transition = 1f;
        private float _nextGlance;
        private float _glance;
        private float _glanceTarget;
        // The nest announced a level it reached in play (not a loaded save): the next stage grows in rather than appears.
        private bool _reachedInPlay;
        private Vector3 _restPosition;
        private ParticleSystem.MinMaxCurve _baseStartSize;

        /// <summary>A short line for the HUD when a stage comes.</summary>
        public event Action<string> Notice;

        /// <summary>The stage shown (or growing in): 0 for none.</summary>
        public int Stage => Mathf.Max(0, _stage);

        private void Awake()
        {
            if (_settings == null || _heat == null || _flames == null || _viewer == null)
            {
                Debug.LogError($"{nameof(PhoenixView)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
            else
            {
                _restPosition = transform.localPosition;
                _baseStartSize = _flames.main.startSize;
            }
        }

        private void OnEnable()
        {
            if (_heat != null)
            {
                _heat.LevelReached += OnLevelReached;
            }
        }

        private void OnDisable()
        {
            if (_heat != null)
            {
                _heat.LevelReached -= OnLevelReached;
            }
        }

        private void OnLevelReached(int level)
        {
            _reachedInPlay = true;
        }

        private void Update()
        {
            int stage = PhoenixLook.StageFor(_settings.Stages, _heat.Level);
            if (stage != _stage)
            {
                ChangeStage(stage);
            }

            float deltaTime = Time.deltaTime;
            _transition = Mathf.Min(1f, _transition + deltaTime / _settings.TransitionSeconds);
            PhoenixLook look = PhoenixLook.Lerp(_from, _to, Mathf.SmoothStep(0f, 1f, _transition));
            Apply(look);
        }

        // A stage reached in play grows in with a flare; one that is simply there (a fresh game, a loaded save) appears at once.
        private void ChangeStage(int stage)
        {
            bool risen = stage > _stage;
            bool first = _stage < 0 || !(risen && _reachedInPlay);
            _reachedInPlay = false;
            _from = first ? PhoenixLook.Of(_settings.Stages, stage) : PhoenixLook.Lerp(_from, _to, _transition);
            _to = PhoenixLook.Of(_settings.Stages, stage);
            _transition = first ? 1f : 0f;
            _stage = stage;
            if (first || !risen || stage <= 0)
            {
                return;
            }

            if (_fire != null)
            {
                _fire.Flare(_settings.Flare);
            }

            string message = _settings.Stages[stage - 1].Message;
            if (!string.IsNullOrEmpty(message))
            {
                Notice?.Invoke(message);
            }
        }

        private void Apply(PhoenixLook look)
        {
            ParticleSystem.EmissionModule emission = _flames.emission;
            emission.rateOverTime = look.IsVisible ? look.ParticlesPerSecond : 0f;
            if (!look.IsVisible)
            {
                return;
            }

            ParticleSystem.MainModule main = _flames.main;
            Color color = look.Color;
            color.a = look.Brightness;
            main.startColor = color;
            main.startSize = new ParticleSystem.MinMaxCurve(_baseStartSize.constantMin * look.FlameSize, _baseStartSize.constantMax * look.FlameSize);
            transform.localPosition = _restPosition + Vector3.up * look.Lift;

            float time = Time.time;
            float breath = 1f + Mathf.Sin(time * _settings.BreatheSpeed * Mathf.PI * 2f) * look.Breathe;
            float beat = 1f + Mathf.Sin(time * _settings.WingBeatSpeed * Mathf.PI * 2f) * look.WingBeat;
            transform.localScale = new Vector3(look.Scale * breath * beat, look.Scale * breath, look.Scale);

            FaceViewer();
        }

        // Turned toward the player around the vertical axis, with a glance aside now and then.
        private void FaceViewer()
        {
            if (Time.time >= _nextGlance)
            {
                _nextGlance = Time.time + _settings.GlanceInterval * UnityEngine.Random.Range(0.7f, 1.3f);
                _glanceTarget = Mathf.Approximately(_glanceTarget, 0f) ? UnityEngine.Random.Range(-1f, 1f) * _settings.GlanceAngle : 0f;
            }

            _glance = Mathf.MoveTowards(_glance, _glanceTarget, 30f * Time.deltaTime);
            Vector3 toViewer = _viewer.position - transform.position;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(toViewer) * Quaternion.Euler(0f, _glance, 0f);
            }
        }
    }
}
