using UnityEngine;

namespace ArmWrestlingVR
{
    [CreateAssetMenu(menuName = "Arm Wrestling/Grip Avatar Profile")]
    public sealed class GripAvatarProfile : ScriptableObject
    {
        public GameObject prefab;
        [Min(0.01f)] public float modelScale = 1f;
        public Vector3 originFromSeat = new Vector3(0f,-0.09f,-0.1f);
        public float footLift = 0.08f;
        public Vector3 playerShoulder = new Vector3(0.19f,0.19f,-0.34f);
        public float playerSupportHeight = 0.073f;
        [Tooltip("Offsets in anatomical hand space, multiplied by measured palm length.")]
        public Vector3 playerWebCorrection, opponentWebCorrection;
        public float playerRoll, opponentRoll;
        public Vector3 thumbOpposition;
        [Tooltip("Optional normalized surface marker offsets: heel, web, thumb, index, middle, ring, little.")]
        public Vector3[] contactOffsets = new Vector3[7];
        public Vector4[] muscleOffsets = new Vector4[5];
        [System.Serializable]
        public struct FingerAdjustment
        {
            public GripAvatarProfile player;
            public HumanBodyBones joint;
            [Tooltip("Degrees relative to this Avatar's retargeted joint rotation.")]
            public Vector3 localEuler;
        }
        [Tooltip("Optional authored pose for which the retargeted opponent needs local finger adjustments.")]
        public HumanoidGripDefinition opponentCalibrationGrip;
        public FingerAdjustment[] opponentFingerAdjustments;
        [Min(0.5f)] public float contactRadiusScale = 1f;
        public float elbowSupportHeight = 0.135f;
        public Vector2 bodySideAtLimits = new Vector2(0.16f,0.02f);
        public Vector2 uprightAtLimits = new Vector2(13f,7f);

        internal void ApplyOpponentAdjustments(HumanoidGripDefinition definition, GripAvatarProfile player, PairedGripPose.Hand hand)
        {
            if (definition != opponentCalibrationGrip || opponentFingerAdjustments == null) return;
            foreach (var edit in opponentFingerAdjustments)
            {
                if (edit.player != player) continue;
                int i = (int)edit.joint - (int)HumanBodyBones.RightThumbProximal;
                if (i < 0 || i >= hand.fingerRotations.Length) continue;
                Quaternion delta = Quaternion.Euler(edit.localEuler);
                hand.openFingerRotations[i] *= delta;
                hand.fingerRotations[i] *= delta;
            }
        }

        public bool IsUsable
        {
            get
            {
                var animator = prefab != null ? prefab.GetComponent<Animator>() : null;
                return animator != null && animator.isHuman && animator.avatar != null && animator.avatar.isValid && modelScale > 0f;
            }
        }
    }
}
