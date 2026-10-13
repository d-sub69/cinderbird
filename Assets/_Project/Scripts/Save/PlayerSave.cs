using System;
using Cinderbird.Player;
using UnityEngine;

namespace Cinderbird.Save
{
    [Serializable]
    public sealed class PlayerSave
    {
        [SerializeField] private Vector3 _position;
        [SerializeField] private float _yaw;
        [SerializeField] private float _pitch;
        [SerializeField] private bool _crouching;

        public PlayerPose Pose
        {
            get => new PlayerPose(_position, _yaw, _pitch, _crouching);
            set
            {
                _position = value.Position;
                _yaw = value.Yaw;
                _pitch = value.Pitch;
                _crouching = value.Crouching;
            }
        }
    }
}
