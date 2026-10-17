using System;
using UnityEngine;

namespace Cinderbird.Nest
{
    /// <summary>One stage of the phoenix's silhouette in the nest fire: from which Heat level, and how it looks.</summary>
    [Serializable]
    public struct PhoenixStage
    {
        [Tooltip("The nest's Heat level this stage comes with.")]
        [SerializeField, Min(0)] private int _heatLevel;
        [Tooltip("Size of the silhouette (1 = about 1.9 m across the wings).")]
        [SerializeField, Min(0f)] private float _scale;
        [Tooltip("Flame particles the shape gives off per second.")]
        [SerializeField, Min(0f)] private float _particlesPerSecond;
        [Tooltip("How bright and solid the flames are (0..1).")]
        [SerializeField, Range(0f, 1f)] private float _brightness;
        [Tooltip("The flames' color at this stage: deep red embers early, golden white when it is whole.")]
        [SerializeField] private Color _color;
        [Tooltip("How big each flame is (1 = as set on the particle system).")]
        [SerializeField, Min(0f)] private float _flameSize;
        [Tooltip("How far above its place in the fire it rises (meters).")]
        [SerializeField] private float _lift;
        [Tooltip("How much it swells and settles, as if breathing (share of its size).")]
        [SerializeField, Range(0f, 0.5f)] private float _breathe;
        [Tooltip("How far the wings beat (share of the wingspan).")]
        [SerializeField, Range(0f, 0.5f)] private float _wingBeat;
        [Tooltip("Shown on the HUD when the stage comes.")]
        [SerializeField] private string _message;

        public PhoenixStage(int heatLevel, float scale, float particlesPerSecond, float brightness, Color color, float flameSize, float lift,
            float breathe, float wingBeat, string message)
        {
            _heatLevel = heatLevel;
            _scale = scale;
            _particlesPerSecond = particlesPerSecond;
            _brightness = brightness;
            _color = color;
            _flameSize = flameSize;
            _lift = lift;
            _breathe = breathe;
            _wingBeat = wingBeat;
            _message = message;
        }

        public int HeatLevel => _heatLevel;
        public PhoenixLook Look => new PhoenixLook(_scale, _particlesPerSecond, _brightness, _color, _flameSize, _lift, _breathe, _wingBeat);
        public string Message => _message;
    }
}
