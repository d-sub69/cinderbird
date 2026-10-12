using UnityEngine;

namespace Cinderbird.Sifting
{
    [CreateAssetMenu(fileName = "BasketSettings", menuName = "Cinderbird/Basket Settings")]
    public sealed class BasketSettings : ScriptableObject
    {
        [Tooltip("Embers the basket holds before you must empty it at the nest.")]
        [SerializeField, Min(1)] private int _capacity = 25;

        [Header("Visuals")]
        [Tooltip("Usable radius of the basket floor in meters; embers are spread within it.")]
        [SerializeField, Min(0.01f)] private float _radius = 0.17f;
        [Tooltip("How far ember spots stray from an even spiral. Automatically reduced if the largest embers could touch.")]
        [SerializeField, Range(0f, 0.5f)] private float _spotJitter = 0.25f;

        [Header("Glow light")]
        [Tooltip("Point light intensity added per ember carried.")]
        [SerializeField, Min(0f)] private float _lightIntensityPerEmber = 0.08f;
        [SerializeField, Min(0f)] private float _maxLightIntensity = 1.5f;

        public int Capacity => _capacity;
        public float Radius => _radius;
        public float SpotJitter => _spotJitter;
        public float LightIntensityPerEmber => _lightIntensityPerEmber;
        public float MaxLightIntensity => _maxLightIntensity;
    }
}
