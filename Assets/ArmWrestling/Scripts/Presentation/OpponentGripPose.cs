using UnityEngine;

namespace ArmWrestlingVR
{
    // Ch03 finger poses. Cache absolute rotations so the pose cannot
    // accumulate when the Animator evaluates more than once in a frame.
    internal sealed class OpponentGripPose
    {
        private const int First = (int)HumanBodyBones.RightThumbProximal;
        private const int Count = 15;
        private readonly Quaternion[] rotations = new Quaternion[Count];
        private readonly bool[] mapped = new bool[Count];

        public OpponentGripPose(Animator animator, bool handContact = false, bool player = false)
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
                Vector3 curl = thumb ? new Vector3(3f, 6f, 2f) : new Vector3(5f, 9f, 4f);
                if (handContact && !thumb)
                    curl = player ? new Vector3(2f, 11f, 6f) : new Vector3(10f, 19f, 6f);

                Save(finger, baseJoint, flexion, curl.x);
                Save(finger + 1, middleJoint, flexion, curl.y);
                Save(finger + 2, tipJoint, flexion, curl.z);
                if (thumb && handContact) OpposeThumb(animator, baseJoint, proximal, player);
            }
        }

        private void OpposeThumb(Animator animator, Transform thumb, Vector3 direction, bool player)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (wrist == null || index == null || middle == null || little == null) return;
            Vector3 forward = (middle.position - wrist.position).normalized;
            Vector3 dorsal = Vector3.Cross(index.position - little.position, forward).normalized;
            // Bring the thumb across the index web. Give the player's thumb
            // more room above the paired palms at neutral and negative angles.
            Vector3 target = index.position + 0.025f * forward + (player ? 0.027f : 0.004f) * dorsal;
            Vector3 from = thumb.parent.InverseTransformDirection(direction);
            Vector3 to = thumb.parent.InverseTransformDirection(target - thumb.position);
            Quaternion turn = Quaternion.RotateTowards(Quaternion.identity, Quaternion.FromToRotation(from, to), 32f);
            rotations[0] = turn * thumb.localRotation;
            mapped[0] = true;
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
