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
