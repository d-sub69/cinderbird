using System;
using UnityEngine;

namespace Cinderbird.Save
{
    /// <summary>
    /// The helper (Sally): her mode, the spot she was sent to, where she stands and what is in her sieve. Whether she is
    /// hired lives in the upgrades.
    /// </summary>
    [Serializable]
    public sealed class HelperSave
    {
        [SerializeField] private int _mode;
        [Tooltip("Where she was sent (world), its layer (-1: the outermost open one), and whether she was still on her way.")]
        [SerializeField] private bool _hasSpot;
        [SerializeField] private Vector3 _spot;
        [SerializeField] private int _spotLayer = -1;
        [SerializeField] private bool _spotPending;
        [SerializeField] private bool _hasPose;
        [SerializeField] private Vector3 _position;
        [SerializeField] private float _yaw;
        [SerializeField] private SieveSave _sieve = new SieveSave();

        public int Mode { get => _mode; set => _mode = value; }
        public bool HasSpot { get => _hasSpot; set => _hasSpot = value; }
        public Vector3 Spot { get => _spot; set => _spot = value; }
        public int SpotLayer { get => _spotLayer; set => _spotLayer = value; }
        public bool SpotPending { get => _spotPending; set => _spotPending = value; }
        public bool HasPose { get => _hasPose; set => _hasPose = value; }
        public Vector3 Position { get => _position; set => _position = value; }
        public float Yaw { get => _yaw; set => _yaw = value; }
        public SieveSave Sieve => _sieve;
    }
}
