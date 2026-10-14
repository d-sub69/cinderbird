using System.Collections.Generic;
using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Draws the sieve's contents: embers resting on the sieve floor, the ash disc covering them, and a faint
    /// glimmer on the ash surface above each buried ember that brightens as shaking thins the ash.
    /// Contents are redrawn only when they change; the per-frame work is just the glimmer twinkle.
    /// </summary>
    public sealed class SieveView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        // Spots stay inside this share of the sieve radius so embers don't clip the rim.
        private const float UsableRadiusFraction = 0.85f;
        // Embers rest a little sunk into the floor (see EmberPoolDisplay), so their top is at 0.8 × size.
        private const float EmberTopPerSize = 0.8f;
        // A fresh load always covers the biggest ember by this margin, so nothing shows before shaking.
        private const float CoverMargin = 1.25f;

        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Cylinder representing the ash. Its local y = 0 should be the sieve floor.")]
        [SerializeField] private Transform _ashDisc;
        [Tooltip("Parent for ember visuals; its local XZ plane is the sieve floor.")]
        [SerializeField] private Transform _emberRoot;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider.")]
        [SerializeField] private Renderer _emberPrefab;

        [Tooltip("Optional: draws the ash rising while a scooped load is still pouring in.")]
        [SerializeField] private SieveFiller _filler;

        [Header("Buried glimmer")]
        [SerializeField] private GlimmerSettings _glimmerSettings;
        [Tooltip("Dot shown on the ash surface above a buried ember (Ember_Visual works).")]
        [SerializeField] private Renderer _glimmerPrefab;

        private EmberPoolDisplay _embers;
        private Renderer[] _glimmers;
        private Color[] _glimmerEmission;
        private bool _anyGlimmer;
        private float _minCoverHeight;
        private MaterialPropertyBlock _propertyBlock;
        private MaterialPropertyBlock _ashBlock;
        private Renderer _ashRenderer;

        private void Awake()
        {
            if (_sieve == null || _settings == null || _tierCatalog == null || _ashDisc == null || _emberRoot == null ||
                _emberPrefab == null || _glimmerSettings == null || _glimmerPrefab == null)
            {
                Debug.LogError($"{nameof(SieveView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _embers = new EmberPoolDisplay(
                _emberPrefab,
                _emberRoot,
                _settings.MaxVisibleEmbers,
                _settings.Radius * UsableRadiusFraction,
                _settings.SpotJitter,
                _tierCatalog,
                this);
            _minCoverHeight = LargestEmberSize() * EmberTopPerSize * CoverMargin;
            _propertyBlock = new MaterialPropertyBlock();
            _ashBlock = new MaterialPropertyBlock();
            _ashRenderer = _ashDisc.GetComponent<Renderer>();
            CreateGlimmerPool();
        }

        private void OnEnable()
        {
            if (_embers == null)
            {
                return;
            }

            _sieve.Contents.Changed += Refresh;
            if (_filler != null)
            {
                _filler.IncomingChanged += Refresh;
            }

            Refresh();
        }

        private void OnDisable()
        {
            if (_embers == null)
            {
                return;
            }

            _sieve.Contents.Changed -= Refresh;
            if (_filler != null)
            {
                _filler.IncomingChanged -= Refresh;
            }
        }

        private void Update()
        {
            if (!_anyGlimmer || _glimmerSettings.TwinkleSpeed <= 0f)
            {
                return;
            }

            float time = Time.time;
            for (int i = 0; i < _glimmers.Length; i++)
            {
                if (_glimmers[i].gameObject.activeSelf)
                {
                    float dim = GlimmerTwinkle.Dim(time, GlimmerTwinkle.PhaseFor(i), _glimmerSettings.TwinkleSpeed, _glimmerSettings.TwinkleAmount);
                    ApplyEmission(_glimmers[i], _glimmerEmission[i] * dim);
                }
            }
        }

        private void CreateGlimmerPool()
        {
            int count = _settings.MaxVisibleEmbers;
            _glimmers = new Renderer[count];
            _glimmerEmission = new Color[count];
            for (int i = 0; i < count; i++)
            {
                Renderer glimmer = Instantiate(_glimmerPrefab, _emberRoot);
                glimmer.name = $"Sieve Glimmer {i}";
                // Same layer as the sieve, so the held-items camera draws it and it never clips.
                glimmer.gameObject.layer = _emberRoot.gameObject.layer;
                glimmer.gameObject.SetActive(false);
                _glimmers[i] = glimmer;
            }
        }

        private void Refresh()
        {
            SieveContents contents = _sieve.Contents;
            float ashHeight = AshHeight(contents);

            UpdateAshDisc(ashHeight);
            UpdateAshColor(contents);
            // Embers lie on the floor; the ash disc hides them until it drains below their tops.
            _embers.Sync(contents.Embers, 0f);
            UpdateGlimmers(contents.Embers, ashHeight);
        }

        // Each load fills up to a height deep enough to bury every ember, then drains in step with the ash left.
        // While a load is still pouring in, it rises toward that same height, so the hand-over is seamless.
        private float AshHeight(SieveContents contents)
        {
            if (contents.AshVolume > 0f)
            {
                return LoadHeight(contents.LoadVolume) * contents.AshFraction;
            }

            // A load without ash (a loose ember picked up on its own, or test mode) brings no ash to show.
            if (_filler != null && _filler.IsFilling && _filler.IncomingVolume > 0f)
            {
                return LoadHeight(_filler.IncomingVolume) * _filler.IncomingProgress;
            }

            return 0f;
        }

        // The ash takes the color of the layers it was dug from; while pouring, the incoming load's color.
        private void UpdateAshColor(SieveContents contents)
        {
            if (_ashRenderer == null)
            {
                return;
            }

            bool pouring = !contents.HasAsh && _filler != null && _filler.IsFilling;
            _ashBlock.SetColor(BaseColorId, pouring ? _filler.IncomingAshColor : contents.AshColor);
            _ashRenderer.SetPropertyBlock(_ashBlock);
        }

        private float LoadHeight(float loadVolume)
        {
            float height = _settings.FullAshHeight * Mathf.Clamp01(loadVolume / _settings.ReferenceAshVolume);
            return Mathf.Max(height, _minCoverHeight);
        }

        private void UpdateGlimmers(IReadOnlyList<Ember> embers, float ashHeight)
        {
            _anyGlimmer = false;
            int shown = _embers.ShownCount;
            float maxDepth = Mathf.Max(_settings.FullAshHeight, _minCoverHeight);

            for (int i = 0; i < _glimmers.Length; i++)
            {
                Renderer glimmer = _glimmers[i];
                EmberTier tier = i < shown ? TierOf(embers[i]) : null;
                if (tier == null)
                {
                    glimmer.gameObject.SetActive(false);
                    continue;
                }

                Vector3 emberPosition = _embers.VisualTransform(i).localPosition;
                float emberTop = emberPosition.y + tier.VisualScale * 0.5f;
                float brightness = SieveGlimmer.Brightness(ashHeight, emberTop, maxDepth, _glimmerSettings.DeepBrightness);
                if (brightness <= 0f)
                {
                    glimmer.gameObject.SetActive(false);
                    continue;
                }

                glimmer.gameObject.SetActive(true);
                Transform glimmerTransform = glimmer.transform;
                // Centered on the ash surface, so about half the dot pokes out.
                glimmerTransform.localPosition = new Vector3(emberPosition.x, ashHeight, emberPosition.z);
                glimmerTransform.localScale = Vector3.one * (tier.VisualScale * _glimmerSettings.SizeScale);

                _glimmerEmission[i] = tier.EmissionColor * (_glimmerSettings.Brightness * brightness);
                ApplyEmission(glimmer, _glimmerEmission[i]);
                _anyGlimmer = true;
            }
        }

        private void ApplyEmission(Renderer glimmer, Color emission)
        {
            // Black base so only the emission shows: it reads as light through the ash, not a bead on it.
            _propertyBlock.SetColor(BaseColorId, Color.black);
            _propertyBlock.SetColor(EmissionColorId, emission);
            glimmer.SetPropertyBlock(_propertyBlock);
        }

        private EmberTier TierOf(Ember ember)
        {
            int index = ember.TierIndex;
            return index >= 0 && index < _tierCatalog.Count ? _tierCatalog[index] : null;
        }

        private float LargestEmberSize()
        {
            float largest = 0f;
            for (int i = 0; i < _tierCatalog.Count; i++)
            {
                if (_tierCatalog[i] != null)
                {
                    largest = Mathf.Max(largest, _tierCatalog[i].VisualScale);
                }
            }

            return largest;
        }

        private void UpdateAshDisc(float height)
        {
            bool visible = height > 0f;
            _ashDisc.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            // Unity's cylinder primitive is 2 units tall at scale 1, so scale.y is half the height.
            // Raising it by the same amount keeps the bottom on the sieve floor.
            float halfHeight = height * 0.5f;
            Vector3 scale = _ashDisc.localScale;
            _ashDisc.localScale = new Vector3(scale.x, halfHeight, scale.z);
            Vector3 position = _ashDisc.localPosition;
            _ashDisc.localPosition = new Vector3(position.x, halfHeight, position.z);
        }
    }
}
