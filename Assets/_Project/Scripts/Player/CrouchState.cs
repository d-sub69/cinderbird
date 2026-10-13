using UnityEngine;

namespace Cinderbird.Player
{
    /// <summary>
    /// Smoothly moves the player's height between standing and crouching. Standing back up waits
    /// until there is headroom, so the player can't stand up into an overhang.
    /// </summary>
    public sealed class CrouchState
    {
        private const float Epsilon = 0.0001f;

        private readonly float _standHeight;
        private readonly float _crouchHeight;
        private readonly float _transitionSpeed;

        /// <param name="transitionSpeed">Meters of height change per second.</param>
        public CrouchState(float standHeight, float crouchHeight, float transitionSpeed)
        {
            _standHeight = standHeight;
            _crouchHeight = Mathf.Min(crouchHeight, standHeight);
            _transitionSpeed = Mathf.Max(0f, transitionSpeed);
            Height = standHeight;
        }

        public float Height { get; private set; }

        /// <summary>True whenever the player isn't fully upright, including mid-transition.</summary>
        public bool IsCrouching => Height < _standHeight - Epsilon;

        /// <summary>Jumps straight to fully crouched or standing, e.g. when loading a save.</summary>
        public void SnapTo(bool crouching)
        {
            Height = crouching ? _crouchHeight : _standHeight;
        }

        /// <param name="canStand">Whether there is room above to grow taller this frame.</param>
        public void Tick(bool wantsCrouch, bool canStand, float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            // Blocked from rising: hold the current height rather than sinking back down.
            if (!wantsCrouch && !canStand)
            {
                return;
            }

            float target = wantsCrouch ? _crouchHeight : _standHeight;
            Height = Mathf.MoveTowards(Height, target, _transitionSpeed * deltaTime);
        }
    }
}
