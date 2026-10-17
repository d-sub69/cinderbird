using Cinderbird.Economy;
using UnityEngine;

namespace Cinderbird.Powers
{
    /// <summary>The Warm Wind power: what it blows away, how often, from when, and how it looks and feels.</summary>
    [CreateAssetMenu(fileName = "WarmWindSettings", menuName = "Cinderbird/Warm Wind Settings")]
    public sealed class WarmWindSettings : HeatGate
    {
        [Header("Effect")]
        [Tooltip("Radius of the area the wind blows the ash off (meters), until its radius upgrade says otherwise.")]
        [SerializeField, Min(0.25f)] private float _radius = 2.5f;
        [Tooltip("How much ash it blows off the surface, in the middle of the area (meters).")]
        [SerializeField, Min(0.05f)] private float _depth = 0.3f;
        [Tooltip("Seconds before it can be called again, until its cooldown upgrade says otherwise.")]
        [SerializeField, Min(0f)] private float _cooldown = 60f;
        [Tooltip("How far the player can send it (meters).")]
        [SerializeField, Min(1f)] private float _reach = 15f;
        [Tooltip("The nest's Heat level the power comes with.")]
        [SerializeField, Min(0)] private int _requiredHeatLevel = 1;

        [Header("Feel")]
        [Tooltip("Speed of the gust on its way to the spot (m/s); the ash blows away when it arrives.")]
        [SerializeField, Min(1f)] private float _gustSpeed = 22f;
        [Tooltip("Streaks of wind in one gust.")]
        [SerializeField, Min(0)] private int _gustStreaks = 70;
        [Tooltip("Puffs of ash the wind lifts off the area.")]
        [SerializeField, Min(0)] private int _dustPuffs = 120;
        [Tooltip("A slight camera shake while the wind gathers (degrees).")]
        [SerializeField, Min(0f)] private float _gatherShake = 0.25f;
        [Tooltip("The shake when the gust strikes (degrees).")]
        [SerializeField, Min(0f)] private float _gustShake = 1.2f;
        [Tooltip("How long the gust's shake takes to die down (seconds).")]
        [SerializeField, Min(0.05f)] private float _gustShakeSeconds = 0.6f;
        [Tooltip("How long F must be held to gather the wind (seconds); let go sooner and only a puff comes. The shake builds up over the same time.")]
        [SerializeField, Min(0.05f)] private float _gatherSeconds = 0.8f;

        public float Radius => _radius;
        public float Depth => _depth;
        public float Cooldown => _cooldown;
        public float Reach => _reach;
        public override int RequiredHeatLevel => _requiredHeatLevel;
        public float GustSpeed => _gustSpeed;
        public int GustStreaks => _gustStreaks;
        public int DustPuffs => _dustPuffs;
        public float GatherShake => _gatherShake;
        public float GustShake => _gustShake;
        public float GustShakeSeconds => _gustShakeSeconds;
        public float GatherSeconds => _gatherSeconds;
    }
}
