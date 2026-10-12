using Cinderbird.Player;
using NUnit.Framework;
using UnityEngine;

namespace Cinderbird.Tests.EditMode
{
    public sealed class FirstPersonMotorTests
    {
        private const float Dt = 0.01f;
        private const float WalkSpeed = 4f;
        private const float Gravity = 20f;
        private const float JumpHeight = 1f;
        private const float CoyoteTime = 0.1f;
        private const float JumpBufferTime = 0.1f;

        private static readonly float JumpVelocity = Mathf.Sqrt(2f * Gravity * JumpHeight);

        private static FirstPersonMotor CreateMotor()
        {
            return new FirstPersonMotor(new MotorConfig(
                walkSpeed: WalkSpeed,
                groundAcceleration: 40f,
                airAcceleration: 10f,
                jumpHeight: JumpHeight,
                gravity: Gravity,
                coyoteTime: CoyoteTime,
                jumpBufferTime: JumpBufferTime,
                sprintMultiplier: SprintMultiplier,
                crouchMultiplier: CrouchMultiplier));
        }

        private const float SprintMultiplier = 1.6f;
        private const float CrouchMultiplier = 0.5f;

        private static float SettledHorizontalSpeed(FirstPersonMotor motor)
        {
            for (int i = 0; i < 200; i++)
            {
                motor.Tick(Vector3.forward, true, Dt);
            }

            return new Vector3(motor.Velocity.x, 0f, motor.Velocity.z).magnitude;
        }

        [Test]
        public void Sprinting_ReachesSprintSpeed()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.SetStance(sprinting: true, crouching: false);

            Assert.That(SettledHorizontalSpeed(motor), Is.EqualTo(WalkSpeed * SprintMultiplier).Within(1e-4f));
        }

        [Test]
        public void Crouching_ReachesCrouchSpeed()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.SetStance(sprinting: false, crouching: true);

            Assert.That(SettledHorizontalSpeed(motor), Is.EqualTo(WalkSpeed * CrouchMultiplier).Within(1e-4f));
        }

        [Test]
        public void CrouchingWhileSprinting_UsesCrouchSpeed()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.SetStance(sprinting: true, crouching: true);

            Assert.That(SettledHorizontalSpeed(motor), Is.EqualTo(WalkSpeed * CrouchMultiplier).Within(1e-4f));
        }

        [Test]
        public void Jump_WhileCrouching_DoesNothing()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.SetStance(sprinting: false, crouching: true);
            motor.RequestJump();

            Vector3 velocity = motor.Tick(Vector3.zero, true, Dt);

            Assert.That(velocity.y, Is.LessThanOrEqualTo(0f));
        }

        private static void TickAirborne(FirstPersonMotor motor, float seconds)
        {
            int steps = Mathf.RoundToInt(seconds / Dt);
            for (int i = 0; i < steps; i++)
            {
                motor.Tick(Vector3.zero, false, Dt);
            }
        }

        [Test]
        public void Jump_WhenGrounded_GivesExpectedUpwardVelocity()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.RequestJump();

            Vector3 velocity = motor.Tick(Vector3.zero, true, Dt);

            Assert.That(velocity.y, Is.EqualTo(JumpVelocity).Within(1e-4f));
        }

        [Test]
        public void Jump_WithinCoyoteTime_Works()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.Tick(Vector3.zero, true, Dt);
            TickAirborne(motor, CoyoteTime * 0.5f);

            motor.RequestJump();
            Vector3 velocity = motor.Tick(Vector3.zero, false, Dt);

            Assert.That(velocity.y, Is.GreaterThan(0f));
        }

        [Test]
        public void Jump_AfterCoyoteTime_DoesNothing()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.Tick(Vector3.zero, true, Dt);
            TickAirborne(motor, CoyoteTime * 3f);

            motor.RequestJump();
            Vector3 velocity = motor.Tick(Vector3.zero, false, Dt);

            Assert.That(velocity.y, Is.LessThan(0f));
        }

        [Test]
        public void Jump_SecondPressInAir_DoesNotDoubleJump()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.RequestJump();
            motor.Tick(Vector3.zero, true, Dt);

            motor.RequestJump();
            Vector3 velocity = motor.Tick(Vector3.zero, false, Dt);

            Assert.That(velocity.y, Is.LessThan(JumpVelocity));
        }

        [Test]
        public void Jump_PressedJustBeforeLanding_IsBuffered()
        {
            FirstPersonMotor motor = CreateMotor();
            TickAirborne(motor, 0.5f);

            motor.RequestJump();
            TickAirborne(motor, JumpBufferTime * 0.5f);
            Vector3 velocity = motor.Tick(Vector3.zero, true, Dt);

            Assert.That(velocity.y, Is.EqualTo(JumpVelocity).Within(1e-4f));
        }

        [Test]
        public void Jump_PressedLongBeforeLanding_IsIgnored()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.RequestJump();
            TickAirborne(motor, JumpBufferTime * 3f);

            Vector3 velocity = motor.Tick(Vector3.zero, true, Dt);

            Assert.That(velocity.y, Is.LessThanOrEqualTo(0f));
        }

        [Test]
        public void Gravity_AccumulatesWhileAirborne()
        {
            FirstPersonMotor motor = CreateMotor();

            TickAirborne(motor, 1f);

            Assert.That(motor.Velocity.y, Is.EqualTo(-Gravity).Within(0.01f));
        }

        [Test]
        public void Grounded_ResetsFallVelocity()
        {
            FirstPersonMotor motor = CreateMotor();
            TickAirborne(motor, 1f);

            Vector3 velocity = motor.Tick(Vector3.zero, true, Dt);

            Assert.That(velocity.y, Is.LessThanOrEqualTo(0f).And.GreaterThan(-5f));
        }

        [Test]
        public void HorizontalSpeed_DiagonalInput_DoesNotExceedWalkSpeed()
        {
            FirstPersonMotor motor = CreateMotor();
            Vector3 diagonal = new Vector3(1f, 0f, 1f);

            for (int i = 0; i < 200; i++)
            {
                motor.Tick(diagonal, true, Dt);
            }

            Vector3 horizontal = new Vector3(motor.Velocity.x, 0f, motor.Velocity.z);
            Assert.That(horizontal.magnitude, Is.EqualTo(WalkSpeed).Within(1e-4f));
        }

        [Test]
        public void HorizontalSpeed_Accelerates_RatherThanSnapping()
        {
            FirstPersonMotor motor = CreateMotor();

            motor.Tick(Vector3.forward, true, Dt);

            Assert.That(motor.Velocity.z, Is.GreaterThan(0f).And.LessThan(WalkSpeed));
        }

        [Test]
        public void HitCeiling_WhileRising_StopsUpwardVelocity()
        {
            FirstPersonMotor motor = CreateMotor();
            motor.RequestJump();
            motor.Tick(Vector3.zero, true, Dt);

            motor.NotifyHitCeiling();

            Assert.That(motor.Velocity.y, Is.EqualTo(0f));
        }
    }
}
