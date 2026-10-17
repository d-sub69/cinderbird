using System;
using UnityEngine;

namespace Cinderbird.Valley
{
    /// <summary>One stage of the valley's thaw: from which Heat level, where the snow melts, which stretch of the river runs, which windows light up.</summary>
    [Serializable]
    public struct ValleyThawStage
    {
        [Tooltip("The nest's Heat level this stage comes with.")]
        [SerializeField, Min(0)] private int _heatLevel;
        [Tooltip("Middle of the patch that thaws (world x/z).")]
        [SerializeField] private Vector3 _center;
        [Tooltip("Radius of the patch (meters).")]
        [SerializeField, Min(0f)] private float _radius;
        [Tooltip("The stretch of river that thaws, from and to (0 = its start, 1 = its end).")]
        [SerializeField] private Vector2 _river;
        [Tooltip("The windows that light up (their order in the village).")]
        [SerializeField] private int[] _windows;
        [Tooltip("Shown on the HUD when the stage comes.")]
        [SerializeField] private string _message;

        public ValleyThawStage(int heatLevel, Vector3 center, float radius, Vector2 river, int[] windows, string message)
        {
            _heatLevel = heatLevel;
            _center = center;
            _radius = radius;
            _river = river;
            _windows = windows;
            _message = message;
        }

        public int HeatLevel => _heatLevel;
        public Vector3 Center => _center;
        public float Radius => _radius;
        public Vector2 River => _river;
        public int[] Windows => _windows ?? Array.Empty<int>();
        public string Message => _message;
    }
}
