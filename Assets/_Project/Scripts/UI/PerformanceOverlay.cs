using Cinderbird.Terrain;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cinderbird.UI
{
    /// <summary>
    /// Debug read-out of frame rate and terrain timings, toggled with a key (F3). Text is rebuilt a few times per
    /// second with TextMeshPro's allocation-free SetText.
    /// </summary>
    public sealed class PerformanceOverlay : MonoBehaviour
    {
        // Averaging window: long enough to be readable, short enough to show spikes.
        private const float RefreshInterval = 0.25f;

        [SerializeField] private TMP_Text _text;
        [SerializeField] private VoxelTerrain _terrain;
        [SerializeField] private InputActionReference _toggleAction;
        [SerializeField] private bool _visibleOnStart = true;

        private bool _visible;
        private float _accumulatedSeconds;
        private float _worstFrameSeconds;
        private int _frames;
        private float _nextRefreshTime;

        private void Awake()
        {
            if (_text == null || _terrain == null || _toggleAction == null)
            {
                Debug.LogError($"{nameof(PerformanceOverlay)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            SetVisible(_visibleOnStart);
        }

        private void OnEnable()
        {
            if (_toggleAction != null)
            {
                _toggleAction.action.Enable();
            }
        }

        private void Update()
        {
            if (_toggleAction.action.WasPressedThisFrame())
            {
                SetVisible(!_visible);
            }

            float frame = Time.unscaledDeltaTime;
            _accumulatedSeconds += frame;
            _worstFrameSeconds = Mathf.Max(_worstFrameSeconds, frame);
            _frames++;
            if (!_visible || Time.unscaledTime < _nextRefreshTime || _frames == 0)
            {
                return;
            }

            float average = _accumulatedSeconds / _frames;
            // TextMeshPro's {index:decimals} format, not .NET's; avoids allocating a string every refresh.
            _text.SetText(
                "{0:0} FPS   {1:1} ms (worst {2:1})\nDig {3:1} ms   Mesh {4:1} ms / {5:0} chunks",
                1f / average,
                average * 1000f,
                _worstFrameSeconds * 1000f,
                _terrain.LastDigMilliseconds,
                _terrain.LastMeshMilliseconds,
                _terrain.LastMeshedChunkCount);

            _accumulatedSeconds = 0f;
            _worstFrameSeconds = 0f;
            _frames = 0;
            _nextRefreshTime = Time.unscaledTime + RefreshInterval;
        }

        private void SetVisible(bool visible)
        {
            _visible = visible;
            _text.enabled = visible;
        }
    }
}
