using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// The performance budget, measured on the development PC (Ryzen 7 5700X, RTX 3070 Ti) at 1080p, Medium,
    /// VSync off. Staying inside it there should leave the agreed minimum spec (GTX 1050, i5-6500, 8 GB) at 60 fps.
    /// The F3 overlay shows over-budget values in red.
    /// </summary>
    [CreateAssetMenu(fileName = "PerformanceBudget", menuName = "Cinderbird/Performance Budget")]
    public sealed class PerformanceBudget : ScriptableObject
    {
        [Header("Every frame (development PC)")]
        [Tooltip("Main-thread work per frame, without waiting for VSync or the GPU.")]
        [SerializeField, Min(0.1f)] private float _mainThreadMilliseconds = 6f;
        [SerializeField, Min(0.1f)] private float _gpuMilliseconds = 4f;
        [Tooltip("Managed memory allocated per frame in normal play; garbage now means collector stalls later.")]
        [SerializeField, Min(0)] private int _gcBytesPerFrame;

        [Header("Moments")]
        [Tooltip("One dig including re-meshing and collider baking.")]
        [SerializeField, Min(0.1f)] private float _digMilliseconds = 4f;
        [Tooltip("One update of settling ash on the main thread: writing the slide and re-meshing (the steps run in the background).")]
        [SerializeField, Min(0.1f)] private float _settleMilliseconds = 4f;
        [Tooltip("The main-thread part of an autosave (copying the state); packing and writing run in the background.")]
        [SerializeField, Min(0.1f)] private float _saveCaptureMilliseconds = 2f;
        [Tooltip("From the scene starting to the player having control, including building the mountain and loading the save.")]
        [SerializeField, Min(0.1f)] private float _startupSeconds = 3f;

        public float MainThreadMilliseconds => _mainThreadMilliseconds;
        public float GpuMilliseconds => _gpuMilliseconds;
        public int GcBytesPerFrame => _gcBytesPerFrame;
        public float DigMilliseconds => _digMilliseconds;
        public float SettleMilliseconds => _settleMilliseconds;
        public float SaveCaptureMilliseconds => _saveCaptureMilliseconds;
        public float StartupSeconds => _startupSeconds;
    }
}
