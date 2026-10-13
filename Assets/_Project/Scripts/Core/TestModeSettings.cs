using UnityEngine;

namespace Cinderbird.Core
{
    /// <summary>
    /// One switch for fast playtesting: a much bigger scoop, scoops that land without ash (no shaking), and a sieve
    /// that holds any number of embers. Read live, so it can be ticked during Play. Only works in the Editor and in
    /// development builds, so a release can never ship with it on.
    /// </summary>
    [CreateAssetMenu(fileName = "TestMode", menuName = "Cinderbird/Test Mode")]
    public sealed class TestModeSettings : ScriptableObject
    {
        [Tooltip("The one switch. Everything below only applies while this is ticked.")]
        [SerializeField] private bool _enabled;
        [SerializeField, Min(1f)] private float _scoopRadiusMultiplier = 3f;
        [Tooltip("Scooped ash is dropped on the way: embers land in the sieve ready to pour.")]
        [SerializeField] private bool _skipShaking = true;
        [SerializeField] private bool _unlimitedSieveEmbers = true;

        public bool Active => _enabled && Debug.isDebugBuild;

        public float ScoopRadiusMultiplier => Active ? _scoopRadiusMultiplier : 1f;

        public bool SkipShaking => Active && _skipShaking;

        public bool UnlimitedSieveEmbers => Active && _unlimitedSieveEmbers;
    }
}
