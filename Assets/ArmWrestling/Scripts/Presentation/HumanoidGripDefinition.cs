using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    // Semantic hand shape: Humanoid stretch/spread, never source-bone quaternions.
    [CreateAssetMenu(menuName = "Arm Wrestling/Humanoid Grip Definition")]
    public sealed class HumanoidGripDefinition : ScriptableObject
    {
        [Tooltip("Thumb/index/middle/ring/little. XYZW = proximal stretch, spread, middle stretch, distal stretch.")]
        public Vector4[] open = {
            new Vector4(0.1f, 0f, 0.1f, 0.1f), new Vector4(0.8f,0f,0.9f,0.9f),
            new Vector4(0.8f,0f,0.9f,0.9f), new Vector4(0.8f,0f,0.9f,0.9f), new Vector4(0.8f,0f,0.9f,0.9f) };
        public Vector4[] closed = {
            new Vector4(-0.3f,0f,-0.6f,-0.4f), new Vector4(-0.65f,0f,-0.15f,0.15f),
            new Vector4(-0.65f,0f,-0.15f,0.15f), new Vector4(-0.65f,0f,-0.15f,0.15f), new Vector4(-0.65f,0f,-0.15f,0.15f) };
        public GripContactTargets contactTargets;
        [Serializable]
        public sealed class AuthoredHand
        {
            public Avatar sourceAvatar;
            // Position correction in anchor axes, in units of palm length.
            public Vector3 wristOffset;
            public Quaternion wristDelta = Quaternion.identity;
            public Quaternion[] readyBones, contestBones;
            public Vector4[] readyMuscles, contestMuscles;
            public ThumbDirections readyThumb, contestThumb;

            public bool IsComplete => sourceAvatar != null && readyBones?.Length == 15
                && contestBones?.Length == 15 && readyMuscles?.Length == 5 && contestMuscles?.Length == 5;
        }

        // Two authored segment directions in the anatomical palm frame. Old keys leave them zero.
        [Serializable]
        public struct ThumbDirections
        {
            public Vector3 proximal, intermediate;

            private static Quaternion PalmFrame(Transform wrist, Transform[] fingers)
            {
                Vector3 forward = fingers[6].position - wrist.position;
                Vector3 normal = Vector3.Cross(fingers[3].position - fingers[12].position, forward);
                return Quaternion.LookRotation(forward, normal);
            }

            public static ThumbDirections Capture(Transform wrist, Transform[] fingers)
            {
                Quaternion inverse = Quaternion.Inverse(PalmFrame(wrist, fingers));
                return new ThumbDirections {
                    proximal = inverse * (fingers[1].position - fingers[0].position).normalized,
                    intermediate = inverse * (fingers[2].position - fingers[1].position).normalized
                };
            }

            public void Apply(Transform wrist, Transform[] fingers)
            {
                if (proximal.sqrMagnitude < 0.5f || intermediate.sqrMagnitude < 0.5f) return;
                Quaternion frame = PalmFrame(wrist, fingers);
                Aim(fingers[0], fingers[1], frame * proximal);
                Aim(fingers[1], fingers[2], frame * intermediate);
            }

            private static void Aim(Transform joint, Transform child, Vector3 direction)
            {
                joint.rotation = Quaternion.FromToRotation(child.position - joint.position, direction) * joint.rotation;
            }
        }

        public AuthoredHand authoredPlayer, authoredOpponent;
        public bool HasAuthoredPair => authoredPlayer?.IsComplete == true && authoredOpponent?.IsComplete == true;
        public Vector3 playerForward = new Vector3(-0.28f,0.87f,0.40f);
        public Vector3 opponentForward = new Vector3(0.25f,0.70f,-0.67f);
        public Vector2 palmRoll;
        public Vector3 palmNormal = new Vector3(0.956f,0.06f,0.288f);
        [Min(0f)] public float surfaceGap = 0.002f;
        public Vector3 palmContactCenter = Vector3.zero;
        public float thumbStagger = 0.006f;
        public float thumbOppositionTwist = 50f;
        [Range(0f,1f)] public float fingerStart = 0f;
    }
}
