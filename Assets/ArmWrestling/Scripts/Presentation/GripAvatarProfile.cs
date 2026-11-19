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
        public Vector4[] muscleOffsets = new Vector4[5];
        [Min(0.5f)] public float contactRadiusScale = 1f;
        public float elbowSupportHeight = 0.135f;
        public Vector2 bodySideAtLimits = new Vector2(0.16f,0.02f);
        public Vector2 uprightAtLimits = new Vector2(13f,7f);

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
