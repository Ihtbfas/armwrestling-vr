using UnityEngine;

namespace ArmWrestlingVR
{
    // Local Ch03 grip trial. Cache absolute finger rotations so the pose cannot
    // accumulate when the Animator evaluates more than once in a frame.
    internal sealed class OpponentGripPose
    {
        private const int First = (int)HumanBodyBones.RightThumbProximal;
        private const int Count = 15;
        private readonly Quaternion[] rotations = new Quaternion[Count];
        private readonly bool[] mapped = new bool[Count];

        public OpponentGripPose(Animator animator)
        {
            for (int finger = 0; finger < Count; finger += 3)
            {
                var baseJoint = animator.GetBoneTransform((HumanBodyBones)(First + finger));
                var middleJoint = animator.GetBoneTransform((HumanBodyBones)(First + finger + 1));
                var tipJoint = animator.GetBoneTransform((HumanBodyBones)(First + finger + 2));
                if (baseJoint == null || middleJoint == null || tipJoint == null) continue;
                bool thumb = finger == 0;
                // Infer the flexion axis from the evaluated Idle pose. The imported
                // bind rotations differ from the rotations written by Humanoid.
                Vector3 proximal = (middleJoint.position - baseJoint.position).normalized;
                Vector3 intermediate = (tipJoint.position - middleJoint.position).normalized;
                Vector3 flexion = Vector3.Cross(proximal, intermediate);
                if (flexion.sqrMagnitude < 0.0001f) continue;
                Save(finger, baseJoint, flexion, thumb ? 3f : 5f);
                Save(finger + 1, middleJoint, flexion, thumb ? 6f : 9f);
                Save(finger + 2, tipJoint, flexion, thumb ? 2f : 4f);
            }
        }

        private void Save(int index, Transform joint, Vector3 flexion, float degrees)
        {
            Vector3 axis = joint.InverseTransformDirection(flexion);
            rotations[index] = joint.localRotation * Quaternion.AngleAxis(degrees, axis.normalized);
            mapped[index] = true;
        }

        public void Apply(Animator animator)
        {
            for (int i = 0; i < Count; i++)
                if (mapped[i]) animator.SetBoneLocalRotation((HumanBodyBones)(First + i), rotations[i]);
        }

        public void Apply(Transform[] joints)
        {
            for (int i = 0; i < Count; i++)
                if (mapped[i] && joints[i] != null) joints[i].localRotation = rotations[i];
        }
    }
}
