using Cinderbird.Terrain;
using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// When settling ash slides into where the player stands, lifts them on top of it rather than leaving them
    /// stuck inside. Reads the ash density directly: colliders catch up with settling a frame or two later.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class KeepAboveAsh : MonoBehaviour
    {
        // Density above this at a body point counts as being inside ash; the surface itself is 0.
        private const float BuriedDensity = 0.2f;
        private const float LiftStep = 0.05f;
        private const float MaxLift = 3f;

        [SerializeField] private VoxelTerrain _terrain;
        [SerializeField] private FirstPersonController _player;

        private CharacterController _body;

        private void Awake()
        {
            _body = GetComponent<CharacterController>();
            if (_terrain == null || _player == null)
            {
                Debug.LogError($"{nameof(KeepAboveAsh)} on '{name}' has unassigned references.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (_terrain != null)
            {
                _terrain.Settled += OnSettled;
            }
        }

        private void OnDisable()
        {
            if (_terrain != null)
            {
                _terrain.Settled -= OnSettled;
            }
        }

        private void OnSettled(Bounds changed)
        {
            float lift = 0f;
            while (lift <= MaxLift && IsBuried(lift))
            {
                lift += LiftStep;
            }

            if (lift > 0f)
            {
                _player.MoveBy(Vector3.up * lift);
            }
        }

        // Checks a few points along the capsule, raised by `lift`: just above the feet, the middle and the head.
        private bool IsBuried(float lift)
        {
            Vector3 center = transform.TransformPoint(_body.center) + Vector3.up * lift;
            float half = _body.height * 0.5f - _body.radius;
            return _terrain.SampleDensityAt(center + Vector3.down * half) > BuriedDensity
                || _terrain.SampleDensityAt(center) > BuriedDensity
                || _terrain.SampleDensityAt(center + Vector3.up * half) > BuriedDensity;
        }
    }
}
