using UnityEngine;

namespace Cinderbird.Embers
{
    [CreateAssetMenu(fileName = "EmberSpawnSettings", menuName = "Cinderbird/Ember Spawn Settings")]
    public sealed class EmberSpawnSettings : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _embersPerCubicMeter = 3f;
        [Tooltip("Minimum ash density at a spawn spot (-1..1). 0.99 keeps embers at least about one cell below the surface.")]
        [SerializeField, Range(0f, 1f)] private float _minDensity = 0.99f;
        [SerializeField] private int _seed = 1;

        public EmberSpawnParameters ToParameters()
        {
            return new EmberSpawnParameters(_embersPerCubicMeter, _minDensity, _seed);
        }
    }
}
