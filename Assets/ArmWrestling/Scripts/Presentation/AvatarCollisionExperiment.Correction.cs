using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    public sealed partial class AvatarCollisionExperiment
    {
        private const float ContactAllowance = 0.002f;
        private const float MaxDigitDegrees = 14f;
        public float MaxFingerCorrection { get; private set; }
        public float ArmCorrection { get; private set; }

        partial void CorrectPose()
        {
            NudgeArm();
            NudgeFingers(playerHand, opponentHand);
            NudgeFingers(opponentHand, playerHand);
        }

        private void NudgeFingers(List<Proxy> moving, List<Proxy> other)
        {
            for (int first = 1; first < moving.Count; first += 3)
            {
                Transform joint = moving[first].joint;
                float before = FingerEnergy(moving, first, other, joint.position, out Vector3 axis);
                if (axis.sqrMagnitude < 1e-8f || before <= 0f) continue;

                Quaternion initial = joint.rotation;
                float angle = Mathf.Min(MaxDigitDegrees, axis.magnitude * Mathf.Rad2Deg);
                // The MTD gives a linearized proposal. Accept it only after
                // the moved shapes improve on the actual overlap query.
                for (int attempt = 0; attempt < 3; attempt++, angle *= 0.5f)
                {
                    joint.rotation = Quaternion.AngleAxis(angle, axis) * initial;
                    RefreshDigit(moving, first);
                    if (FingerEnergy(moving, first, other, joint.position, out _) < before - 1e-8f)
                    {
                        MaxFingerCorrection = Mathf.Max(MaxFingerCorrection, angle);
                        break;
                    }
                    if (attempt == 2)
                    {
                        joint.rotation = initial;
                        RefreshDigit(moving, first);
                    }
                }
            }
        }

        private static float FingerEnergy(List<Proxy> moving, int first, List<Proxy> other,
            Vector3 pivot, out Vector3 rotation)
        {
            float score = 0f;
            float weight = 0f;
            rotation = Vector3.zero;
            for (int i = first; i < first + 3; i++)
            {
                Proxy a = moving[i];
                Vector3 lever = a.pose.position - pivot;
                foreach (var b in other)
                {
                    if (!Overlap(a, b, out Vector3 direction, out float depth) || depth <= ContactAllowance) continue;
                    float excess = depth - ContactAllowance;
                    score += excess * excess;
                    weight += excess;
                    rotation += Vector3.Cross(lever, direction) * (excess * excess / (lever.sqrMagnitude + 0.0001f));
                }
            }
            if (weight > 0f) rotation /= weight;
            return score;
        }

        private static void RefreshDigit(List<Proxy> hand, int first)
        {
            for (int i = first; i < first + 3; i++) hand[i].pose = hand[i].sample();
        }

        private const float MaxArmDegrees = 12f;
        private Transform deviceFrame, opponentShoulder, opponentWrist;
        private Proxy opponentUpperShape, opponentForeShape;

        private void NudgeArm()
        {
            Vector3 shoulder = opponentShoulder.position;
            Vector3 elbow = opponentElbow.joint.position;
            Vector3 axis = (opponentWrist.position - shoulder).normalized;
            float before = ArmEnergy();
            if (before <= 0f) return;

            // The shortest axis of separation can switch between box faces.
            // Prefer an anatomical outward swivel, and scale it by the query.
            Vector3 tangent = Vector3.Cross(axis, elbow - shoulder);
            float outward = Mathf.Sign(Vector3.Dot(tangent, -deviceFrame.right));
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.003f, 0.046f, Mathf.Sqrt(before)));
            float angle = outward * MaxArmDegrees * strength;
            // Keep most of the original elbow support when the reach circle
            // rises away from the pad. Bound the swivel on the continuous arc.
            float low = 0f, high = 1f;
            for (int i = 0; i < 9; i++)
            {
                float fraction = (low + high) * 0.5f;
                Vector3 candidate = shoulder + Quaternion.AngleAxis(angle * fraction, axis) * (elbow - shoulder);
                if (WithinElbowBudget(elbow, candidate)) low = fraction;
                else high = fraction;
            }
            angle *= low;

            Quaternion upper = opponentShoulder.rotation;
            Quaternion wrist = opponentWrist.rotation;
            ApplySwivel(upper, wrist, axis, angle);
            float gain = Mathf.Sqrt(before) - Mathf.Sqrt(ArmEnergy());
            // Fade on the measured improvement instead of switching directions
            // when the collision normal jumps to another face.
            angle *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.006f, gain));
            ApplySwivel(upper, wrist, axis, angle);
            if (ArmEnergy() < before - 1e-8f) ArmCorrection = angle;
            else ApplySwivel(upper, wrist, axis, 0f);
        }

        private void ApplySwivel(Quaternion upper, Quaternion wrist, Vector3 axis, float angle)
        {
            opponentShoulder.rotation = Quaternion.AngleAxis(angle, axis) * upper;
            opponentWrist.rotation = wrist;
            RefreshArm();
        }

        private bool WithinElbowBudget(Vector3 before, Vector3 candidate)
        {
            Vector3 offset = deviceFrame.InverseTransformDirection(candidate - before);
            Vector3 local = deviceFrame.InverseTransformPoint(candidate);
            return offset.y >= -0.004f && offset.y <= 0.012f
                && offset.x * offset.x + offset.z * offset.z <= 0.030f * 0.030f
                && local.x >= -0.302f && local.x <= -0.008f
                && local.z >= 0.061f && local.z <= 0.239f;
        }

        private float ArmEnergy()
        {
            float score = 0f;
            foreach (var body in torso)
            {
                score += OverlapCost(opponentUpperShape, body);
                score += OverlapCost(opponentForeShape, body);
            }
            return score;
        }

        private static float OverlapCost(Proxy a, Proxy b)
        {
            if (!Overlap(a, b, out _, out float depth)) return 0f;
            float excess = Mathf.Max(0f, depth - ContactAllowance);
            return excess * excess;
        }

        private void RefreshArm()
        {
            opponentUpperShape.pose = opponentUpperShape.sample();
            opponentForeShape.pose = opponentForeShape.sample();
            opponentElbow.pose = opponentElbow.sample();
        }
    }
}
