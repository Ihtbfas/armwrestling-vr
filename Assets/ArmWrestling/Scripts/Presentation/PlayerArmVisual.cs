using UnityEngine;

namespace ArmWrestlingVR
{
    // A view-only right arm skinned to the original local Ch03 skeleton.
    public sealed class PlayerArmVisual : MonoBehaviour
    {
        private const string MeshResource = "Characters/P1DLocalPlayerArm";
        private Transform upper, lower, hand, middle;
        private float upperLength, foreLength, setback;
        private Vector3 upperAxis, lowerAxis;
        private Quaternion handBasis, lowerRest, upperRest;
        private Material material;
        private readonly Transform[] fingers = new Transform[15];
        public SkinnedMeshRenderer Skin { get; private set; }
        public Vector3 PalmPosition => Vector3.Lerp(hand.position, middle.position, 0.55f);
        public Vector3 DesiredPalm { get; private set; }

        public static PlayerArmVisual TryCreate(VirtualArmRig rig, GameObject prefab)
        {
            Mesh mesh = Resources.Load<Mesh>(MeshResource);
            if (rig == null || rig.playerArm == null || prefab == null || mesh == null)
            {
                Debug.LogWarning("[P1-D] Local player arm unavailable. Use the editor Prepare Local Player Arm command.");
                return null;
            }
            GameObject model = Instantiate(prefab, rig.transform, false);
            model.name = "Player right arm (local Ch03)";
            model.transform.localScale = 1.12f * Vector3.one;
            var visual = model.AddComponent<PlayerArmVisual>();
            if (!visual.Bind(mesh))
            {
                model.SetActive(false);
                Destroy(model);
                Debug.LogWarning("[P1-D] Local mesh and Ch03 did not bind. Using the capsule.");
                return null;
            }
            foreach (Transform segment in new[] { rig.playerArm.upperArm, rig.playerArm.forearm, rig.playerArm.hand })
            {
                segment.GetComponent<Renderer>().enabled = false;
                segment.GetComponent<Collider>().enabled = false;
            }
            return visual;
        }

        private bool Bind(Mesh mesh)
        {
            var animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman || !animator.avatar.isValid) return false;
            upper = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            lower = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            if (upper == null || lower == null || hand == null || middle == null || index == null || little == null) return false;

            animator.runtimeAnimatorController = Resources.Load<RuntimeAnimatorController>("Characters/OpponentIK");
            animator.applyRootMotion = false;
            animator.Update(0f);
            for (int i = 0; i < fingers.Length; i++)
                fingers[i] = animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.RightThumbProximal + i));
            animator.enabled = false;
            upperLength = Vector3.Distance(upper.position, lower.position);
            foreLength = Vector3.Distance(lower.position, hand.position);
            upperAxis = upper.InverseTransformDirection((lower.position - upper.position).normalized);
            lowerAxis = lower.InverseTransformDirection((hand.position - lower.position).normalized);
            upperRest = Quaternion.Inverse(transform.rotation) * upper.rotation;
            lowerRest = Quaternion.Inverse(upper.rotation) * lower.rotation;
            setback = Vector3.Distance(hand.position, middle.position) * 0.55f;
            Vector3 forward = (middle.position - hand.position).normalized;
            Vector3 dorsal = Vector3.Cross(index.position - little.position, forward).normalized;
            handBasis = Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forward, dorsal);

            foreach (var renderer in GetComponentsInChildren<Renderer>())
            {
                renderer.enabled = false;
                if (Skin == null && renderer is SkinnedMeshRenderer candidate &&
                    candidate.sharedMesh != null && mesh.vertexCount == candidate.sharedMesh.vertexCount &&
                    System.Array.IndexOf(candidate.bones, upper) >= 0)
                    Skin = candidate;
            }
            if (Skin == null) return false;
            Skin.sharedMesh = mesh;
            Skin.enabled = true;
            Skin.updateWhenOffscreen = true;
            material = new Material(Shader.Find("Standard")) { color = new Color(0.70f, 0.47f, 0.34f) };
            material.SetFloat("_Glossiness", 0.22f);
            Skin.sharedMaterial = material;
            return true;
        }

        internal void SetGripPose(OpponentGripPose pose) => pose.Apply(fingers);

        public void ApplyGrip(Transform frame, Vector3 grip)
        {
            // The player sits on -Z. Keep the real one-arm bone lengths while
            // offsetting the two palm centres across the shared grip reference.
            Vector3 shoulder = frame.TransformPoint(new Vector3(0.19f, 0.19f, -0.34f));
            Vector3 pad = frame.TransformPoint(new Vector3(ArenaFactory.ElbowPadX, 0.08f, ArenaFactory.ElbowPadZ));
            DesiredPalm = grip + frame.right * 0.028f;
            Vector3 direction = (DesiredPalm - pad).normalized;
            Vector3 targetWrist = DesiredPalm - direction * setback;
            ArmChain.SolveJoints(shoulder, targetWrist, pad, frame.up, upperLength, foreLength, out var elbow, out var wrist);
            upper.position = shoulder;
            Quaternion start = transform.rotation * upperRest;
            upper.rotation = Quaternion.FromToRotation(start * upperAxis, elbow - shoulder) * start;
            start = upper.rotation * lowerRest;
            lower.rotation = Quaternion.FromToRotation(start * lowerAxis, wrist - elbow) * start;
            hand.rotation = Quaternion.LookRotation(direction, frame.right) * Quaternion.Inverse(handBasis);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
        }
    }
}
