using UnityEngine;

namespace Cinderbird.Terrain
{
    /// <summary>How loose ash settles after a dig.</summary>
    [CreateAssetMenu(fileName = "SettleSettings", menuName = "Cinderbird/Settle Settings")]
    public sealed class SettleSettings : ScriptableObject
    {
        [Tooltip("Steepest slope loose ash rests at, in degrees. The mountain's own generated slopes stay as they are.")]
        [SerializeField, Range(10f, 80f)] private float _reposeAngle = 35f;
        [Tooltip("How far beyond a dig ash may slide, in meters.")]
        [SerializeField, Min(0f)] private float _reach = 3f;
        [Tooltip("Share of its excess a too-steep column sheds per step (0..1): lower is smoother but slower.")]
        [SerializeField, Range(0.05f, 1f)] private float _flowRate = 0.5f;

        public SettleParameters ToParameters()
        {
            return new SettleParameters(_reposeAngle, _reach, _flowRate);
        }
    }
}
