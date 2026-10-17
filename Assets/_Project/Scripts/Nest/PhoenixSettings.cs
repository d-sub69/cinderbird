using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>The phoenix's silhouette in the nest fire: its stages by Heat level, and how it moves and changes.</summary>
    [CreateAssetMenu(fileName = "PhoenixSettings", menuName = "Cinderbird/Phoenix Settings")]
    public sealed class PhoenixSettings : ScriptableObject
    {
        [Tooltip("In order; each comes with its Heat level. The silhouette grows clearer from one to the next.")]
        [SerializeField] private PhoenixStage[] _stages = DefaultStages();
        [Tooltip("Seconds a new stage takes to grow in.")]
        [SerializeField, Min(0.1f)] private float _transitionSeconds = 4f;
        [Tooltip("How much the fire flares when a stage comes (as feeding a big ember would).")]
        [SerializeField, Min(0f)] private float _flare = 1.5f;
        [Tooltip("Breaths per second.")]
        [SerializeField, Min(0f)] private float _breatheSpeed = 0.35f;
        [Tooltip("Wing beats per second.")]
        [SerializeField, Min(0f)] private float _wingBeatSpeed = 0.5f;
        [Tooltip("Now and then it turns its head: degrees, and seconds between turns.")]
        [SerializeField, Min(0f)] private float _glanceAngle = 18f;
        [SerializeField, Min(1f)] private float _glanceInterval = 9f;

        public PhoenixStage[] Stages => _stages;

        /// <summary>
        /// The slice's three stages, clearly apart: a few deep-red embers low in the fire; an orange bird rising above it,
        /// breathing; a big golden-white phoenix well above the flames, wings beating.
        /// </summary>
        public static PhoenixStage[] DefaultStages()
        {
            return new[]
            {
                new PhoenixStage(1, 0.55f, 30f, 0.45f, new Color(0.85f, 0.18f, 0.06f), 0.8f, 0f, 0.03f, 0f,
                    "In the nest fire, a flicker like folded wings"),
                new PhoenixStage(2, 0.9f, 130f, 0.75f, new Color(1f, 0.5f, 0.12f), 1f, 0.35f, 0.07f, 0.06f,
                    "The flames take the shape of a bird"),
                new PhoenixStage(3, 1.3f, 340f, 1f, new Color(1f, 0.86f, 0.5f), 1.35f, 0.8f, 0.05f, 0.18f,
                    "Aurel's silhouette burns clear in the nest, wings slowly beating"),
            };
        }

        public float TransitionSeconds => _transitionSeconds;
        public float Flare => _flare;
        public float BreatheSpeed => _breatheSpeed;
        public float WingBeatSpeed => _wingBeatSpeed;
        public float GlanceAngle => _glanceAngle;
        public float GlanceInterval => _glanceInterval;
    }
}
