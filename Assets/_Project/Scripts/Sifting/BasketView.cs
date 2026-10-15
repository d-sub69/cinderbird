using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Draws the embers in the basket, dropping newly tipped ones in from above, and scales an optional
    /// warm point light with how many embers are carried.
    /// </summary>
    public sealed class BasketView : MonoBehaviour
    {
        [SerializeField] private Basket _basket;
        [SerializeField] private BasketSettings _settings;
        [SerializeField] private TipSettings _tipSettings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Parent for ember visuals; its local XZ plane is the basket floor.")]
        [SerializeField] private Transform _emberRoot;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider.")]
        [SerializeField] private Renderer _emberPrefab;
        [Tooltip("Optional point light that brightens with the number of embers carried.")]
        [SerializeField] private Light _glowLight;
        [Tooltip("Optional: the basket's body, scaled up for bigger baskets (see SetSize).")]
        [SerializeField] private Transform _body;

        private EmberPoolDisplay _embers;
        // Per shown ember: time its drop started, or -1 when it is resting.
        private float[] _dropStartTimes;
        private float[] _restHeights;
        private bool _anyDropping;
        private Vector3 _bodyBaseScale;
        private float _sizeScale = 1f;

        private void Awake()
        {
            if (_basket == null || _settings == null || _tipSettings == null || _tierCatalog == null || _emberRoot == null || _emberPrefab == null)
            {
                Debug.LogError($"{nameof(BasketView)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _bodyBaseScale = _body != null ? _body.localScale : Vector3.one;
            if (_body != null)
            {
                _body.localScale = _bodyBaseScale * _sizeScale;
            }

            CreateDisplay(_basket.Contents.Capacity);
        }

        /// <summary>Sizes the basket for an upgrade: its body scales up and embers get spots across the bigger floor.</summary>
        /// <param name="scale">Size relative to the first basket.</param>
        public void SetSize(float scale, int capacity)
        {
            capacity = Mathf.Max(1, capacity);
            scale = Mathf.Max(0.1f, scale);
            if (_embers == null)
            {
                // Not started yet (inactive): Awake builds it at this size, with the basket's capacity.
                _sizeScale = scale;
                return;
            }

            if (Mathf.Approximately(scale, _sizeScale) && capacity == _embers.Capacity)
            {
                return;
            }

            _sizeScale = scale;
            if (_body != null)
            {
                _body.localScale = _bodyBaseScale * _sizeScale;
            }

            _embers.Dispose();
            CreateDisplay(capacity);
            Refresh();
        }

        private void CreateDisplay(int capacity)
        {
            _embers = new EmberPoolDisplay(_emberPrefab, _emberRoot, capacity, _settings.Radius * _sizeScale, _settings.SpotJitter, _tierCatalog, this);
            _dropStartTimes = new float[capacity];
            _restHeights = new float[capacity];
            for (int i = 0; i < capacity; i++)
            {
                _dropStartTimes[i] = -1f;
            }

            _anyDropping = false;
        }

        private void OnEnable()
        {
            if (_embers == null)
            {
                return;
            }

            _basket.Contents.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_embers == null)
            {
                return;
            }

            _basket.Contents.Changed -= Refresh;
        }

        private void Update()
        {
            if (_anyDropping)
            {
                AnimateDrops();
            }
        }

        private void AnimateDrops()
        {
            _anyDropping = false;
            float now = Time.time;
            for (int i = 0; i < _embers.ShownCount; i++)
            {
                if (_dropStartTimes[i] >= 0f)
                {
                    AnimateDrop(i, now - _dropStartTimes[i]);
                }
            }
        }

        private void Refresh()
        {
            BasketContents contents = _basket.Contents;
            int previouslyShown = _embers.Sync(contents.Embers, 0f);
            int shown = _embers.ShownCount;

            for (int i = previouslyShown; i < shown; i++)
            {
                _restHeights[i] = _embers.VisualTransform(i).localPosition.y;
                _dropStartTimes[i] = Time.time;
                _anyDropping = true;
            }

            // Embers that left (poured into the nest) stop animating.
            for (int i = shown; i < previouslyShown; i++)
            {
                _dropStartTimes[i] = -1f;
            }

            // Sync just snapped every ember to rest; put the ones mid-drop back on their curve this frame.
            if (_anyDropping)
            {
                AnimateDrops();
            }

            UpdateLight(contents.Embers.Count);
        }

        private void AnimateDrop(int index, float elapsed)
        {
            Transform visual = _embers.VisualTransform(index);
            float fallTime = elapsed - _tipSettings.DropDelay;

            // Hidden until the sieve has tipped far enough for them to "leave" it.
            if (fallTime < 0f)
            {
                visual.gameObject.SetActive(false);
                _anyDropping = true;
                return;
            }

            visual.gameObject.SetActive(true);
            float progress = fallTime / _tipSettings.DropDuration;
            Vector3 position = visual.localPosition;
            if (progress >= 1f)
            {
                visual.localPosition = new Vector3(position.x, _restHeights[index], position.z);
                _dropStartTimes[index] = -1f;
                return;
            }

            // 1 - p² starts slow and speeds up, like falling under gravity.
            float height = _tipSettings.DropHeight * (1f - progress * progress);
            visual.localPosition = new Vector3(position.x, _restHeights[index] + height, position.z);
            _anyDropping = true;
        }

        private void UpdateLight(int emberCount)
        {
            if (_glowLight == null)
            {
                return;
            }

            _glowLight.enabled = emberCount > 0;
            _glowLight.intensity = Mathf.Min(_settings.MaxLightIntensity, emberCount * _settings.LightIntensityPerEmber);
        }
    }
}
