using UnityEngine;

namespace Cinderbird.Embers
{
    /// <summary>
    /// How ember clusters glow through the ash: brighter the more they're worth, a small bright spot when deep that spreads
    /// as the player digs toward them, gone once emptied.
    /// </summary>
    [CreateAssetMenu(fileName = "ClusterGlowSettings", menuName = "Cinderbird/Cluster Glow Settings")]
    public sealed class ClusterGlowSettings : ScriptableObject
    {
        [Tooltip("Clusters buried deeper than this (meters of ash) don't show.")]
        [SerializeField, Min(0.5f)] private float _reach = 2.5f;
        [Tooltip("Glow strength per unit of cluster worth (Glow values of its embers), before the curve below.")]
        [SerializeField, Min(0f)] private float _intensityPerWorth = 0.35f;
        [Tooltip("How the glow grows with worth: 1 = in proportion, lower = big clusters only a little brighter.")]
        [SerializeField, Range(0.1f, 1f)] private float _worthExponent = 0.5f;
        [Tooltip("Brightest a cluster gets (HDR, so bloom picks it up).")]
        [SerializeField, Min(0f)] private float _maxIntensity = 6f;
        [Tooltip("A deep cluster's glowing patch, as a share of the cluster's size; it grows to the full size as you dig near.")]
        [SerializeField, Range(0.05f, 1f)] private float _deepPatchScale = 0.3f;
        [Tooltip("Smallest glowing patch, in meters.")]
        [SerializeField, Min(0.05f)] private float _minPatchRadius = 0.3f;
        [Tooltip("How much the glow breathes (0 = steady).")]
        [SerializeField, Range(0f, 0.5f)] private float _pulse = 0.15f;
        [Tooltip("Seconds between glow updates; the clusters themselves are counted again about 4 times a second.")]
        [SerializeField, Min(0.05f)] private float _refreshSeconds = 0.25f;

        public float Reach => _reach;
        public float IntensityPerWorth => _intensityPerWorth;
        public float WorthExponent => _worthExponent;
        public float MaxIntensity => _maxIntensity;
        public float DeepPatchScale => _deepPatchScale;
        public float MinPatchRadius => _minPatchRadius;
        public float Pulse => _pulse;
        public float RefreshSeconds => _refreshSeconds;
    }
}
