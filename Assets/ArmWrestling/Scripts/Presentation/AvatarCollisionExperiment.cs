using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArmWrestlingVR
{
    public enum PairedCollisionMode { Off, Diagnose, Correct, ContactGrip }

    // The active query colliders belong to an isolated, unsimulated physics scene.
    // ComputePenetration takes the actual bone poses explicitly.
    public sealed partial class AvatarCollisionExperiment : MonoBehaviour
    {
        public enum Region { Hands, Torso, Pads }

        public readonly struct Contact
        {
            public readonly Region region;
            public readonly string first, second;
            public readonly Vector3 position, direction;
            public readonly float depth;

            internal Contact(Region region, string first, string second, Vector3 position, Vector3 direction, float depth)
            {
                this.region = region;
                this.first = first;
                this.second = second;
                this.position = position;
                this.direction = direction;
                this.depth = depth;
            }
        }

        private sealed class Proxy
        {
            internal Collider shape;
            internal Func<Pose> sample;
            internal Transform joint;
            internal string name;
            internal Pose pose;
            internal bool touching;
        }

        [SerializeField] private PairedCollisionMode mode;
        [Tooltip("在 Scene 视图中显示查询代理。红色表示重叠，黄色线段表示分离方向。")]
        public bool showGizmos;
        public PairedCollisionMode Mode { get => mode; set => mode = value; }
        public IReadOnlyList<Contact> Contacts => contacts;
        public int ProxyCount => proxies.Count;
        public int EvaluationCount { get; private set; }

        private readonly List<Proxy> proxies = new List<Proxy>();
        private readonly List<Contact> contacts = new List<Contact>();
        private readonly List<Proxy> playerHand = new List<Proxy>();
        private readonly List<Proxy> opponentHand = new List<Proxy>();
        private readonly List<Proxy> arms = new List<Proxy>();
        private readonly List<Proxy> torso = new List<Proxy>();
        private PlayerArmVisual player;
        private Scene queryScene;
        private Proxy playerElbow, opponentElbow, playerPad, opponentPad;

        internal void Bind(VirtualArmRig rig, Animator opponentAnimator, PlayerArmVisual playerArm)
        {
            player = playerArm;
            bodyAnimator = opponentAnimator;
            chestJoint = Bone(opponentAnimator, HumanBodyBones.Chest);
            deviceFrame = rig.transform;
            opponentShoulder = Bone(opponentAnimator, HumanBodyBones.RightUpperArm);
            opponentWrist = Bone(opponentAnimator, HumanBodyBones.RightHand);
            queryScene = SceneManager.CreateScene("Avatar collision queries " + GetInstanceID(),
                new CreateSceneParameters(LocalPhysicsMode.Physics3D));
            var playerAnimator = player.GetComponent<Animator>();
            AddHand(playerHand, "Player", playerAnimator);
            AddHand(opponentHand, "Opponent", opponentAnimator);
            playerElbow = AddArm("Player right", playerAnimator, true);
            int rightArmStart = arms.Count;
            opponentElbow = AddArm("Opponent right", opponentAnimator, true);
            opponentUpperShape = arms[rightArmStart];
            opponentForeShape = arms[rightArmStart + 1];
            AddArm("Opponent left", opponentAnimator, false);
            AddTorso(opponentAnimator);

            playerPad = ExistingPad(rig.transform, "Player Elbow Pad");
            opponentPad = ExistingPad(rig.transform, "Opponent Elbow Pad");

            // A known overlap must work before reporting zero live contacts.
            if (!Physics.ComputePenetration(playerElbow.shape, Vector3.zero, Quaternion.identity,
                    opponentElbow.shape, Vector3.right * 0.05f, Quaternion.identity, out _, out float testDepth)
                || Mathf.Abs(testDepth - 0.018f) > 0.0002f
                || Physics.ComputePenetration(playerElbow.shape, Vector3.zero, Quaternion.identity,
                    opponentElbow.shape, Vector3.right * 0.08f, Quaternion.identity, out _, out _))
                throw new InvalidOperationException("Paired collision query self-check failed.");
        }

        private void AddHand(List<Proxy> hand, string label, Animator animator)
        {
            var wrist = Bone(animator, HumanBodyBones.RightHand);
            var fingers = PairedGripPose.MapFingers(animator);
            float palmLength = Vector3.Distance(wrist.position, fingers[6].position);
            float palmWidth = Vector3.Distance(fingers[3].position, fingers[12].position);
            hand.Add(Box(label + " palm", new Vector3(palmWidth * 0.94f, 0.023f, palmLength * 0.76f), () =>
            {
                Vector3 forward = (fingers[6].position - wrist.position).normalized;
                Vector3 dorsal = Vector3.Cross(fingers[3].position - fingers[12].position, forward).normalized;
                return new Pose(wrist.position + forward * (palmLength * 0.53f) - dorsal * 0.003f,
                    Quaternion.LookRotation(forward, dorsal));
            }));

            string[] digits = { "thumb", "index", "middle", "ring", "little" };
            float[] proximalRadii = { 0.010f, 0.009f, 0.009f, 0.0085f, 0.0075f };
            string[] segments = { "proximal", "middle", "tip" };
            for (int digit = 0; digit < digits.Length; digit++)
            {
                int offset = digit * 3;
                for (int segment = 0; segment < 3; segment++)
                {
                    Transform from = fingers[offset + segment];
                    string name = label + " " + digits[digit] + " " + segments[segment];
                    float radius = proximalRadii[digit] * (1f - segment * 0.12f);
                    if (segment < 2)
                    {
                        Transform to = fingers[offset + segment + 1];
                        hand.Add(Capsule(name, from, to, radius, 0.17f, 0.83f));
                    }
                    else
                    {
                        Vector3 tip = from.InverseTransformVector(from.position - fingers[offset + 1].position) * 0.58f;
                        hand.Add(NewCapsule(name, radius, from.TransformVector(tip).magnitude,
                            () => Segment(from.position, from.TransformPoint(tip)), from));
                    }
                }
            }
        }

        private Proxy AddArm(string label, Animator animator, bool right)
        {
            Transform shoulder = Bone(animator, right ? HumanBodyBones.RightUpperArm : HumanBodyBones.LeftUpperArm);
            Transform elbow = Bone(animator, right ? HumanBodyBones.RightLowerArm : HumanBodyBones.LeftLowerArm);
            Transform wrist = Bone(animator, right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            // Trim the shoulder attachment and wrist ends before testing against the chest.
            arms.Add(Capsule(label + " upper arm", shoulder, elbow, 0.043f, 0.30f, 0.88f));
            arms.Add(Capsule(label + " forearm", elbow, wrist, 0.031f, 0.11f, 0.82f));
            if (!right) return null;
            var shape = Make<SphereCollider>(label + " elbow");
            shape.radius = 0.034f;
            return Register(shape, () => new Pose(elbow.position, Quaternion.identity), elbow);
        }

        private void AddTorso(Animator animator)
        {
            var spine = Bone(animator, HumanBodyBones.Spine);
            var chest = Bone(animator, HumanBodyBones.Chest);
            var neck = Bone(animator, HumanBodyBones.Neck);
            var left = Bone(animator, HumanBodyBones.LeftShoulder);
            var right = Bone(animator, HumanBodyBones.RightShoulder);
            Vector3 rightward() => (right.position - left.position).normalized;
            Quaternion rotation()
            {
                var up = (neck.position - spine.position).normalized;
                return Quaternion.LookRotation(Vector3.Cross(rightward(), up), up);
            }
            torso.Add(Box("Opponent chest", new Vector3(0.304f, 0.220f, 0.160f), () =>
            {
                Quaternion q = rotation();
                return new Pose(Vector3.Lerp(chest.position, neck.position, 0.49f) + q * new Vector3(0f, -0.015f, 0.014f), q);
            }));
            torso.Add(Box("Opponent abdomen", new Vector3(0.242f, 0.203f, 0.146f), () =>
            {
                Quaternion q = rotation();
                return new Pose(Vector3.Lerp(spine.position, chest.position, 0.30f) + q * new Vector3(0f, -0.018f, 0.018f), q);
            }));
        }

        public void Evaluate()
        {
            player.RestorePairedFingers();
            MaxFingerCorrection = 0f;
            ArmCorrection = 0f;
            if (mode == PairedCollisionMode.Off)
            {
                contacts.Clear();
                return;
            }
            EvaluationCount++;
            foreach (var proxy in proxies) proxy.pose = proxy.sample();
            if (mode == PairedCollisionMode.Correct) CorrectPose();
            if (mode == PairedCollisionMode.ContactGrip) SolveContactGrip();
            CollectContacts();
        }

        partial void CorrectPose();

        private void CollectContacts()
        {
            contacts.Clear();
            foreach (var proxy in proxies) proxy.touching = false;

            foreach (var a in playerHand)
                foreach (var b in opponentHand)
                    Record(a, b, Region.Hands);
            foreach (var a in arms)
                foreach (var b in torso)
                    Record(a, b, Region.Torso);
            Record(playerElbow, playerPad, Region.Pads);
            Record(opponentElbow, opponentPad, Region.Pads);
        }

        private void Record(Proxy a, Proxy b, Region region)
        {
            if (a == null || b == null || !Overlap(a, b, out Vector3 direction, out float depth)) return;
            a.touching = b.touching = true;
            contacts.Add(new Contact(region, a.name, b.name, a.pose.position, direction, depth));
        }

        private static bool Overlap(Proxy a, Proxy b, out Vector3 direction, out float depth) =>
            Physics.ComputePenetration(a.shape, a.pose.position, a.pose.rotation,
                b.shape, b.pose.position, b.pose.rotation, out direction, out depth);

        private T Make<T>(string label) where T : Collider
        {
            var node = new GameObject(label);
            SceneManager.MoveGameObjectToScene(node, queryScene);
            return node.AddComponent<T>();
        }

        private void OnDestroy()
        {
            if (queryScene.IsValid() && queryScene.isLoaded && SceneManager.sceneCount > 1)
                SceneManager.UnloadSceneAsync(queryScene);
        }

        private Proxy Register(Collider shape, Func<Pose> sample, Transform joint = null)
        {
            var proxy = new Proxy { shape = shape, sample = sample, joint = joint, name = shape.name, pose = sample() };
            proxies.Add(proxy);
            return proxy;
        }

        private Proxy Box(string label, Vector3 size, Func<Pose> sample)
        {
            var box = Make<BoxCollider>(label);
            box.size = size;
            return Register(box, sample);
        }

        private Proxy Capsule(string label, Transform start, Transform end, float radius, float trimStart, float trimEnd) =>
            NewCapsule(label, radius, Vector3.Distance(start.position, end.position) * (trimEnd - trimStart),
                () => Segment(Vector3.Lerp(start.position, end.position, trimStart),
                    Vector3.Lerp(start.position, end.position, trimEnd)), start);

        private Proxy NewCapsule(string label, float radius, float length, Func<Pose> sample, Transform joint)
        {
            var capsule = Make<CapsuleCollider>(label);
            capsule.radius = radius;
            capsule.height = length + 2f * radius;
            return Register(capsule, sample, joint);
        }

        private Proxy ExistingPad(Transform frame, string label)
        {
            var pad = frame.Find(label);
            if (pad == null || !pad.TryGetComponent(out BoxCollider shape)) return null;
            var copy = Make<BoxCollider>(label);
            copy.size = Vector3.Scale(shape.size, pad.lossyScale);
            copy.center = Vector3.Scale(shape.center, pad.lossyScale);
            return Register(copy, () => new Pose(pad.position, pad.rotation));
        }

        private static Pose Segment(Vector3 start, Vector3 end) =>
            new Pose((start + end) * 0.5f, Quaternion.FromToRotation(Vector3.up, end - start));

        private static Transform Bone(Animator animator, HumanBodyBones bone) => animator.GetBoneTransform(bone);

        private void OnDrawGizmos()
        {
            if (!showGizmos || mode == PairedCollisionMode.Off || EvaluationCount == 0) return;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;
            foreach (var proxy in proxies)
            {
                Gizmos.color = proxy.touching ? Color.red : Color.cyan;
                Gizmos.matrix = Matrix4x4.TRS(proxy.pose.position, proxy.pose.rotation, proxy.shape.transform.lossyScale);
                switch (proxy.shape)
                {
                    case BoxCollider box:
                        Gizmos.DrawWireCube(box.center, box.size);
                        break;
                    case SphereCollider sphere:
                        Gizmos.DrawWireSphere(sphere.center, sphere.radius);
                        break;
                    case CapsuleCollider capsule:
                        float half = capsule.height * 0.5f - capsule.radius;
                        Vector3 end = Vector3.up * half;
                        Gizmos.DrawWireSphere(end, capsule.radius);
                        Gizmos.DrawWireSphere(-end, capsule.radius);
                        for (int axis = 0; axis < 4; axis++)
                        {
                            Vector3 offset = Quaternion.Euler(0f, axis * 90f, 0f) * Vector3.right * capsule.radius;
                            Gizmos.DrawLine(offset - end, offset + end);
                        }
                        break;
                }
            }
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.yellow;
            foreach (var hit in contacts)
                Gizmos.DrawRay(hit.position, hit.direction * Mathf.Max(0.012f, 2f * hit.depth));
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
