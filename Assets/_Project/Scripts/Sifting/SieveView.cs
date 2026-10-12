using Cinderbird.Embers;
using UnityEngine;

namespace Cinderbird.Sifting
{
    /// <summary>
    /// Draws the sieve's contents: an ash disc whose height follows the ash volume, and the embers
    /// resting on top of it. Redraws only when the contents change.
    /// </summary>
    public sealed class SieveView : MonoBehaviour
    {
        // Spots stay inside this share of the sieve radius so embers don't clip the rim.
        private const float UsableRadiusFraction = 0.85f;

        [SerializeField] private Sieve _sieve;
        [SerializeField] private SieveSettings _settings;
        [SerializeField] private EmberTierCatalog _tierCatalog;
        [Tooltip("Cylinder representing the ash. Its local y = 0 should be the sieve floor.")]
        [SerializeField] private Transform _ashDisc;
        [Tooltip("Parent for ember visuals; its local XZ plane is the sieve floor.")]
        [SerializeField] private Transform _emberRoot;
        [Tooltip("Small sphere with an emissive URP Lit material and no collider.")]
        [SerializeField] private Renderer _emberPrefab;

        private EmberPoolDisplay _embers;

        private void Awake()
        {
            if (_sieve == null || _settings == null || _tierCatalog == null || _ashDisc == null || _emberRoot == null || _emberPrefab == null)
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
        }

        private void OnEnable()
        {
            if (_embers == null)
            {
                return;
            }

            _sieve.Contents.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (_embers == null)
            {
                return;
            }

            _sieve.Contents.Changed -= Refresh;
        }

        private void Refresh()
        {
            SieveContents contents = _sieve.Contents;
            float fill = Mathf.Clamp01(contents.AshVolume / _settings.ReferenceAshVolume);
            float ashHeight = fill * _settings.FullAshHeight;

            UpdateAshDisc(ashHeight);
            _embers.Sync(contents.Embers, ashHeight);
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
