using UnityEngine;

namespace ArmWrestlingVR
{
    // A view-only right arm skinned to the original local Ch03 skeleton.
    public sealed class PlayerArmVisual : MonoBehaviour
    {
        private const string MeshResource = "Characters/P1DLocalPlayerArm";
        private static readonly Vector3 ContactPalmOffset = new Vector3(0.032f, 0f, 0.008f);
        private Transform upper, lower, hand, middle;
        private float upperLength, foreLength, setback;
        private Vector3 upperAxis, lowerAxis;
        private Quaternion handBasis, lowerRest, upperRest;
        private Material material, sleeveMaterial;
        private Mesh sleeveMesh;
        private readonly Transform[] fingers = new Transform[15];
        public SkinnedMeshRenderer Skin { get; private set; }
        public Vector3 PalmPosition => Vector3.Lerp(hand.position, middle.position, 0.55f);
        public Vector3 DesiredPalm { get; private set; }
        public bool UsesHandContact { get; private set; }
        public bool HasSleeve => sleeveMesh != null;

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

        internal void EnableHandContact()
        {
            UsesHandContact = true;
            var sleeve = new GameObject("Player short sleeve");
            sleeve.transform.SetParent(upper, false);
            sleeve.transform.localRotation = Quaternion.FromToRotation(Vector3.up, upperAxis);
            sleeve.transform.localScale = Vector3.one / upper.lossyScale.x;
            sleeveMesh = CreateSleeveMesh();
            sleeve.AddComponent<MeshFilter>().sharedMesh = sleeveMesh;
            sleeveMaterial = new Material(material) { color = new Color(0.10f, 0.19f, 0.23f) };
            sleeveMaterial.SetFloat("_Glossiness", 0.12f);
            sleeve.AddComponent<MeshRenderer>().sharedMaterial = sleeveMaterial;
        }

        // Profile in metres along the upper arm. The last ring folds in at
        // the cuff. The closed shoulder covers the crop from external views.
        private static Mesh CreateSleeveMesh()
        {
            const int sides = 20;
            Vector2[] profile =
            {
                new Vector2(-0.043f, 0.012f), new Vector2(-0.030f, 0.035f),
                new Vector2(0f, 0.048f), new Vector2(0.067f, 0.047f),
                new Vector2(0.086f, 0.044f), new Vector2(0.092f, 0.041f),
                new Vector2(0.089f, 0.033f)
            };
            var vertices = new Vector3[profile.Length * sides + 1];
            int cap = vertices.Length - 1;
            vertices[cap] = new Vector3(0f, -0.045f, 0f);
            var triangles = new System.Collections.Generic.List<int>();
            for (int ring = 0; ring < profile.Length; ring++)
            {
                for (int side = 0; side < sides; side++)
                {
                    float angle = side * (2f * Mathf.PI / sides);
                    int a = ring * sides + side;
                    int b = ring * sides + (side + 1) % sides;
                    float radius = profile[ring].y;
                    vertices[a] = new Vector3(radius * Mathf.Cos(angle), profile[ring].x, radius * Mathf.Sin(angle));
                    if (ring == 0) triangles.AddRange(new[] { cap, a, b });
                    if (ring < profile.Length - 1)
                        triangles.AddRange(new[] { a, a + sides, b, b, a + sides, b + sides });
                }
            }
            var mesh = new Mesh { name = "Player short sleeve", vertices = vertices };
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        private static Vector3 Pad(Transform frame) =>
            frame.TransformPoint(new Vector3(ArenaFactory.ElbowPadX, 0.08f, ArenaFactory.ElbowPadZ));

        public void ApplyGrip(Transform frame, Vector3 grip) =>
            PoseArm(frame, grip + frame.right * 0.028f, frame.right);

        // Both palms use this side vector. The cross product keeps it nearly
        // perpendicular to both hands while preserving the world-space wrist goals.
        internal Vector3 ApplyContactGrip(Transform frame, Vector3 grip, Vector3 opponentForward)
        {
            Vector3 palm = grip + frame.TransformVector(ContactPalmOffset);
            Vector3 playerForward = (palm - Pad(frame)).normalized;
            Vector3 across = Vector3.Cross(opponentForward, playerForward);
            if (Vector3.Dot(across, frame.right) < 0f) across = -across;
            Vector3 side = across.sqrMagnitude > 0.000001f
                ? Vector3.Slerp(frame.right, across.normalized, 0.85f).normalized : frame.right;
            PoseArm(frame, palm, side);
            return side;
        }

        private void PoseArm(Transform frame, Vector3 palm, Vector3 side)
        {
            Vector3 shoulder = frame.TransformPoint(new Vector3(0.19f, 0.19f, -0.34f));
            Vector3 pad = Pad(frame);
            DesiredPalm = palm;
            Vector3 direction = (DesiredPalm - pad).normalized;
            Vector3 targetWrist = DesiredPalm - direction * setback;
            ArmChain.SolveJoints(shoulder, targetWrist, pad, frame.up, upperLength, foreLength, out var elbow, out var wrist);
            upper.position = shoulder;
            Quaternion start = transform.rotation * upperRest;
            upper.rotation = Quaternion.FromToRotation(start * upperAxis, elbow - shoulder) * start;
            start = upper.rotation * lowerRest;
            lower.rotation = Quaternion.FromToRotation(start * lowerAxis, wrist - elbow) * start;
            hand.rotation = Quaternion.LookRotation(direction, side) * Quaternion.Inverse(handBasis);
        }

        private void OnDestroy()
        {
            if (material != null) Destroy(material);
            if (sleeveMaterial != null) Destroy(sleeveMaterial);
            if (sleeveMesh != null) Destroy(sleeveMesh);
        }
    }
}
