using System;
using UnityEngine;

namespace ArmWrestlingVR
{
    [CreateAssetMenu(menuName = "Arm Wrestling/Paired Surface Contact Targets")]
    public sealed class GripContactTargets : ScriptableObject
    {
        [Serializable]
        public sealed class Hand
        {
            [Tooltip("Own palmar surface. X toward thumb / palm width; Y wrist to knuckles / palm length; Z surface offset / palm length.")]
            public Vector3 heel = new Vector3(0f,0.75f,0f);
            public Vector3 web = new Vector3(0.34f,0.78f,0f);
            [Tooltip("Thumb is relative to partner web XY. Other fingertips use offsets from their MCP projection onto partner dorsal surface.")]
            public Vector3[] pads = { new Vector3(-0.06f,0.02f,0.015f),
                new Vector3(0f,-0.05f,0.015f),new Vector3(0f,-0.05f,0.015f),
                new Vector3(0f,-0.05f,0.015f),new Vector3(0f,-0.05f,0.015f) };
        }
        [Tooltip("Controlled experiment: keep wrist axes and fit fingers only inside authored Humanoid flexion arcs.")]
        public bool jointLimited;
        public Hand player = new Hand(), opponent = new Hand();
        [Tooltip("Thumb distal direction in the common Grip Anchor; fingers instead turn down the partner back.")]
        public Vector3 thumbDirection = Vector3.up;
        [Min(0f)] public float palmClearance = 0.001f;
        [Range(0f,0.2f)] public float release = 0.035f;
        [Range(0.2f,1.2f)] public float knuckleLift = 0.65f;
    }
}
