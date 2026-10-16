using UnityEngine;

namespace Cinderbird.Helpers
{
    /// <summary>
    /// Procedural animation for the placeholder body (head, body, arms, legs from primitives): legs and arms swing with
    /// her walk, she leans and reaches the sieve to the ash for a scoop, holds it out to shake, tips forward to pour.
    /// The arms always point at her hands, which carry the sieve. Replaced by an Animator-driven version with the model.
    /// </summary>
    public sealed class HelperPrimitiveAnimation : HelperAnimation
    {
        [Tooltip("Bends forward for scoops and pours; pivot at the hips.")]
        [SerializeField] private Transform _torso;
        [SerializeField] private Transform _leftLeg;
        [SerializeField] private Transform _rightLeg;
        [Tooltip("Shoulder pivots; each arm's length runs along its local forward.")]
        [SerializeField] private Transform _leftArm;
        [SerializeField] private Transform _rightArm;
        [Tooltip("Carries the sieve; its rest pose is where she holds it.")]
        [SerializeField] private Transform _hands;
        [SerializeField, Min(0.1f)] private float _armLength = 0.55f;
        [Tooltip("Steps per meter walked.")]
        [SerializeField, Min(0.1f)] private float _stepsPerMeter = 1.6f;
        [SerializeField, Range(0f, 60f)] private float _legSwing = 28f;
        [SerializeField, Range(0f, 45f)] private float _scoopLean = 28f;
        [SerializeField, Range(0f, 45f)] private float _pourLean = 12f;
        [SerializeField, Min(0.01f)] private float _blendTime = 0.12f;

        private HelperPose _pose;
        private float _speed;
        private float _stepPhase;
        private float _lean;
        private float _leanVelocity;
        private Vector3 _handsRest;
        private Quaternion _torsoRest;
        private Quaternion _leftLegRest;
        private Quaternion _rightLegRest;
        private Vector3 _scoopPoint;
        private float _scoopStart = -1f;
        private float _scoopSeconds;
        private float _swing;

        private void Awake()
        {
            if (_torso == null || _leftLeg == null || _rightLeg == null || _leftArm == null || _rightArm == null || _hands == null)
            {
                Debug.LogError($"{nameof(HelperPrimitiveAnimation)} on '{name}' has unassigned references.", this);
                enabled = false;
                return;
            }

            _handsRest = _hands.localPosition;
            _torsoRest = _torso.localRotation;
            _leftLegRest = _leftLeg.localRotation;
            _rightLegRest = _rightLeg.localRotation;
        }

        public override void SetPose(HelperPose pose)
        {
            _pose = pose;
        }

        public override void SetWalkSpeed(float speed)
        {
            _speed = speed;
        }

        public override void Scoop(Vector3 worldPoint, float seconds)
        {
            _scoopPoint = worldPoint;
            _scoopStart = Time.time;
            _scoopSeconds = Mathf.Max(0.05f, seconds);
        }

        private void LateUpdate()
        {
            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            AnimateLegs(deltaTime);
            float reach = ScoopReach();
            float targetLean = _pose == HelperPose.Pour ? _pourLean : reach * _scoopLean;
            _lean = Mathf.SmoothDamp(_lean, targetLean, ref _leanVelocity, _blendTime, Mathf.Infinity, deltaTime);
            _torso.localRotation = _torsoRest * Quaternion.Euler(_lean, 0f, 0f);
            PlaceHands(reach);
            AimArm(_leftArm);
            AimArm(_rightArm);
        }

        private void AnimateLegs(float deltaTime)
        {
            float walking = _pose == HelperPose.Walk ? Mathf.Clamp01(_speed / 2f) : 0f;
            _stepPhase += _speed * _stepsPerMeter * Mathf.PI * deltaTime;
            _swing = Mathf.MoveTowards(_swing, walking, deltaTime * 6f);
            float angle = Mathf.Sin(_stepPhase) * _legSwing * _swing;
            _leftLeg.localRotation = _leftLegRest * Quaternion.Euler(angle, 0f, 0f);
            _rightLeg.localRotation = _rightLegRest * Quaternion.Euler(-angle, 0f, 0f);
        }

        // 0 at rest, 1 with the sieve down at the ash: out over the first part of the pour, a moment there, then back.
        private float ScoopReach()
        {
            if (_scoopStart < 0f)
            {
                return 0f;
            }

            float t = (Time.time - _scoopStart) / _scoopSeconds;
            if (t >= 1f)
            {
                _scoopStart = -1f;
                return 0f;
            }

            float reach = t < 0.4f ? t / 0.4f : t < 0.6f ? 1f : (1f - t) / 0.4f;
            return Mathf.SmoothStep(0f, 1f, reach);
        }

        private void PlaceHands(float reach)
        {
            Transform parent = _hands.parent;
            Vector3 rest = _handsRest;
            if (_pose == HelperPose.Walk)
            {
                rest += new Vector3(0f, Mathf.Abs(Mathf.Sin(_stepPhase)) * 0.02f * _swing, 0f);
            }

            if (reach <= 0f)
            {
                _hands.localPosition = rest;
                return;
            }

            // Toward the ash, but only as far as her arms reach from her chest.
            Vector3 target = parent.InverseTransformPoint(_scoopPoint);
            Vector3 chest = (_leftArm.localPosition + _rightArm.localPosition) * 0.5f;
            Vector3 fromChest = Vector3.ClampMagnitude(target - chest, _armLength + 0.15f);
            _hands.localPosition = Vector3.Lerp(rest, chest + fromChest, reach);
        }

        private void AimArm(Transform arm)
        {
            Vector3 toHands = _hands.position - arm.position;
            if (toHands.sqrMagnitude > 0.0001f)
            {
                arm.rotation = Quaternion.LookRotation(toHands, _torso.up);
            }
        }
    }
}
