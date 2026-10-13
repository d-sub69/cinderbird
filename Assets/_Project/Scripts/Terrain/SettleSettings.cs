using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>How loose ash settles after a dig.</summary>
    [CreateAssetMenu(fileName = "SettleSettings", menuName = "Cinderbird/Settle Settings")]
    public sealed class SettleSettings : ScriptableObject
    {
        [Tooltip("Steepest slope loose ash rests at, in degrees (real dry ash: 30-45; steeper keeps scoops visible). The mountain's own slopes stay as they are.")]
        [SerializeField, Range(10f, 80f)] private float _reposeAngle = 50f;
        [Tooltip("How far beyond a dig ash may slide, in meters.")]
        [SerializeField, Min(0f)] private float _reach = 2f;
        [Tooltip("Share of its excess a too-steep column sheds per step (0..1): lower is smoother but slower.")]
        [SerializeField, Range(0.05f, 1f)] private float _flowRate = 0.5f;
        [Tooltip("Most ash a column sheds per step, in samples' worth (a sample is 0.25 m tall). Sets how fast ash runs: " +
                 "lower is a slower, steadier trickle. 0 = no limit (slides snap).")]
        [SerializeField, Min(0f)] private float _maxFlowPerStep = 0.1f;

        [Header("Look")]
        [Tooltip("How often the sliding ash is shown moving. Each update re-meshes the changed chunks.")]
        [SerializeField, Range(2f, 60f)] private float _updatesPerSecond = 20f;
        [Tooltip("Settling steps per update. With the updates per second and the max flow per step, this sets how fast ash runs.")]
        [SerializeField, Range(1, 200)] private int _stepsPerUpdate = 4;
        [Tooltip("Dust particles per spot where ash runs, each update.")]
        [SerializeField, Range(0, 10)] private int _dustPerSpot = 2;

        public float UpdatesPerSecond => _updatesPerSecond;
        public int StepsPerUpdate => _stepsPerUpdate;
        public int DustPerSpot => _dustPerSpot;

        public SettleParameters ToParameters()
        {
            return new SettleParameters(_reposeAngle, _reach, _flowRate, _maxFlowPerStep);
        }
    }
}
