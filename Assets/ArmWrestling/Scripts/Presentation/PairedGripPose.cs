using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    // A single neutral two-person pose. Positions and wrist rotations are stored
    // in Grip Anchor space; finger rotations belong to the selected Avatar.
    [CreateAssetMenu(menuName = "Arm Wrestling/Paired Grip Pose")]
    public sealed class PairedGripPose : ScriptableObject
    {
        public const string AvatarResource = "P2A/Male_Adult_01";
        public const string PoseResource = "P2A/NeutralPairedGrip";
        public const string ContactPoseResource = "P2A/ContactPairedGrip";
        public const int FingerCount = 15;
        public const float AvatarScale = 0.94f;

        [Serializable]
        public sealed class Hand
        {
            public Vector3 palmDorsal;
            public Vector3[] contactPoints = Array.Empty<Vector3>(), padPoints = Array.Empty<Vector3>(), padLocalPoints = Array.Empty<Vector3>(), contactNormals = Array.Empty<Vector3>();
            public float palmLength, palmWidth, palmThickness;
            public float collisionRadiusScale = 1f;
            public Vector3[] palmContactPoints = Array.Empty<Vector3>();
            public Vector3 thumbRootStart, thumbRootEnd;
            public float thumbRootRadius;
            public Vector3 wristPosition;
            public Quaternion wristRotation = Quaternion.identity;
            public Quaternion[] fingerRotations = new Quaternion[FingerCount];
            public Quaternion[] openFingerRotations = Array.Empty<Quaternion>();

            public Pose WorldWrist(Transform anchor) =>
                new Pose(anchor.TransformPoint(wristPosition), anchor.rotation * wristRotation);

            public void Apply(Transform[] fingers)
            {
                for (int i = 0; i < FingerCount; i++)
                    if (fingers[i] != null)
                        fingers[i].localRotation = fingerRotations[i];
            }
        }

        [Range(0f, 1f)] public float fingerStart;
        public bool usesSurfaceTargets;
        public Hand player = new Hand();
        public Hand opponent = new Hand();

        public bool IsComplete => IsAuthored(player) && IsAuthored(opponent);
        public bool HasContactEndpoints => IsComplete && IsFinite(fingerStart) && fingerStart >= 0f && fingerStart <= 1f
            && IsOpenAuthored(player) && IsOpenAuthored(opponent);
        private static bool IsOpenAuthored(Hand hand) => hand.openFingerRotations != null
            && hand.openFingerRotations.Length == FingerCount && Array.TrueForAll(hand.openFingerRotations, IsRotation);
        private static bool IsAuthored(Hand hand) => hand != null && hand.fingerRotations != null
            && hand.fingerRotations.Length == FingerCount && Array.TrueForAll(hand.fingerRotations, IsRotation)
            && IsRotation(hand.wristRotation) && IsFinite(hand.wristPosition.sqrMagnitude);

        private static bool IsRotation(Quaternion q)
        {
            float norm = Quaternion.Dot(q, q);
            return IsFinite(norm) && Mathf.Abs(norm - 1f) < 0.01f;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        public static HumanBodyBones Finger(int index) =>
            (HumanBodyBones)((int)HumanBodyBones.RightThumbProximal + index);

        public static Transform[] MapFingers(Animator animator)
        {
            var fingers = new Transform[FingerCount];
            for (int i = 0; i < fingers.Length; i++) fingers[i] = animator.GetBoneTransform(Finger(i));
            return fingers;
        }
    }
}
