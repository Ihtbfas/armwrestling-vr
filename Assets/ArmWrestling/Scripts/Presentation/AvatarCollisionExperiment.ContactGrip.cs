using System.Collections.Generic;
using UnityEngine;

namespace ArmWrestlingVR
{
    public sealed partial class AvatarCollisionExperiment
    {
        public int StoppedJoints { get; private set; }
        public float ChestTurn { get; private set; }
        public float BlockedClosureExcess { get; private set; }
        private Transform chestJoint;
        private Animator bodyAnimator;

        private void SolveContactGrip()
        {
            StoppedJoints = 0;
            ChestTurn = 0f;
            BlockedClosureExcess = 0f;
            CloseHand(playerHand, opponentHand);
            CloseHand(opponentHand, playerHand);
            TurnChest();
        }

        private void CloseHand(List<Proxy> hand, List<Proxy> obstacle)
        {
            var closed = new Quaternion[15];
            var opened = new Quaternion[15];
            Vector3 forward = (hand[7].joint.position - hand[1].joint.parent.position).normalized;
            for (int i = 0; i < 15; i++)
            {
                Transform joint = hand[i + 1].joint;
                closed[i] = joint.localRotation;
                Quaternion delta = Quaternion.FromToRotation(hand[i + 1].pose.rotation * Vector3.up, forward);
                if (i < 3) delta = Quaternion.Slerp(Quaternion.identity, delta, 0.45f);
                else delta = Quaternion.RotateTowards(Quaternion.identity, delta, 48f);
                Quaternion worldOpen = delta * joint.rotation;
                opened[i] = Quaternion.Inverse(joint.parent.rotation) * worldOpen;
            }
            for (int i = 0; i < 15; i++) hand[i + 1].joint.localRotation = opened[i];
            RefreshHand(hand);
            for (int i = 0; i < 15; i++)
            {
                Transform joint = hand[i + 1].joint;
                int first = 1 + (i / 3) * 3;
                float initial = DigitPeak(hand, first, obstacle);
                float limit = Mathf.Max(0.002f, initial + 0.0001f);
                float accepted = 0f;
                for (int step = 1; step <= 24; step++)
                {
                    float t = step / 24f;
                    joint.localRotation = Quaternion.Slerp(opened[i], closed[i], t);
                    RefreshHand(hand);
                    float penetration = DigitPeak(hand, first, obstacle);
                    if (penetration > limit)
                    {
                        StoppedJoints++;
                        BlockedClosureExcess = Mathf.Max(BlockedClosureExcess, penetration - limit);
                        break;
                    }
                    accepted = t;
                }
                joint.localRotation = Quaternion.Slerp(opened[i], closed[i], accepted);
                MaxFingerCorrection = Mathf.Max(MaxFingerCorrection, Quaternion.Angle(joint.localRotation, closed[i]));
                RefreshHand(hand);
            }
        }

        private static float DigitPeak(List<Proxy> hand, int first, List<Proxy> other)
        {
            float peak = 0f;
            for (int i = first; i < first + 3; i++)
                foreach (var b in other)
                    if (Overlap(hand[i], b, out _, out float depth)) peak = Mathf.Max(peak, depth);
            return peak;
        }

        private static void RefreshHand(List<Proxy> hand)
        {
            foreach (var proxy in hand) proxy.pose = proxy.sample();
        }

        // Chest rotation is tested against the same arm volume while keeping
        // the elbow and wrist targets. Re-solve bone directions without scaling.
        private void TurnChest()
        {
            float before = ArmEnergy();
            if (before <= 0f) return;
            var right = new ArmSnapshot(bodyAnimator, true);
            var left = new ArmSnapshot(bodyAnimator, false);
            Quaternion start = chestJoint.rotation;
            float best = before, chosen = 0f;
            for (int step = -12; step <= 12; step++)
            {
                float angle = step * 0.25f;
                chestJoint.rotation = Quaternion.AngleAxis(angle, deviceFrame.up) * start;
                right.RestoreTargets();
                left.RestoreTargets();
                foreach (var proxy in proxies) proxy.pose = proxy.sample();
                float energy = ArmEnergy();
                if (right.WithinBudget() && left.WithinBudget() && energy < best)
                {
                    best = energy;
                    chosen = angle;
                }
            }
            chestJoint.rotation = Quaternion.AngleAxis(chosen, deviceFrame.up) * start;
            right.RestoreTargets();
            left.RestoreTargets();
            foreach (var proxy in proxies) proxy.pose = proxy.sample();
            ChestTurn = chosen;
        }

        private sealed class ArmSnapshot
        {
            private readonly Transform upper, lower, hand;
            private readonly Vector3 elbowGoal, wristGoal, upperAxis, lowerAxis;
            private readonly Quaternion upperRotation, lowerRotation, wristRotation;
            private readonly float upperLength, lowerLength;
            internal ArmSnapshot(Animator animator, bool right)
            {
                upper = Bone(animator, right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
                lower = Bone(animator, right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
                hand = Bone(animator, right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
                elbowGoal = lower.position; wristGoal = hand.position;
                upperRotation = upper.rotation; lowerRotation = lower.rotation; wristRotation = hand.rotation;
                upperLength = Vector3.Distance(upper.position, lower.position);
                lowerLength = Vector3.Distance(lower.position, hand.position);
                upperAxis = lower.position - upper.position;
                lowerAxis = hand.position - lower.position;
            }
            internal bool WithinBudget()
            {
                Vector3 shift = lower.position - elbowGoal;
                return shift.sqrMagnitude <= 0.008f * 0.008f
                    && shift.y >= -0.001f
                    && Vector3.Distance(hand.position, wristGoal) < 0.00004f;
            }
            internal void RestoreTargets()
            {
                ArmChain.SolveJoints(upper.position, wristGoal, elbowGoal, Vector3.up, upperLength, lowerLength,
                    out Vector3 elbow, out Vector3 wrist);
                upper.rotation = Quaternion.FromToRotation(upperAxis, elbow - upper.position) * upperRotation;
                lower.rotation = Quaternion.FromToRotation(lowerAxis, wrist - lower.position) * lowerRotation;
                hand.rotation = wristRotation;
            }
        }
    }
}
