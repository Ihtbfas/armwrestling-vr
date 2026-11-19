using System;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    // Explicit Humanoid curls provide anatomical flexion axes at authoring time.
    // Runtime uses the saved local rotations, never a guessed world-space axis.
    public static class ContactGripAuthoring
    {
        private const string Path = "Assets/ArmWrestling/Resources/P2A/ContactPairedGrip.asset";
        private const string RootName = "P2C Contact Pose Authoring";

        [MenuItem("扳手腕/P2-C/Create Contact Pose Draft")]
        public static void Build()
        {
            if (AssetDatabase.LoadAssetAtPath<PairedGripPose>(Path) != null
                && Array.IndexOf(Environment.GetCommandLineArgs(), "-p2crebuild") < 0)
            {
                Debug.Log("[P2-C] Existing authored pose preserved. Use the preview save commands to edit it; -p2crebuild explicitly replaces the draft.");
                if (Application.isBatchMode) EditorApplication.Exit(0);
                return;
            }
            var root = new GameObject(RootName);
            try
            {
                var pose = AssetDatabase.LoadAssetAtPath<PairedGripPose>(Path);
                if (pose == null)
                {
                    pose = ScriptableObject.CreateInstance<PairedGripPose>();
                    AssetDatabase.CreateAsset(pose, Path);
                }
                pose.fingerStart = 0.65f;
                pose.player = Author(root.transform, true);
                pose.opponent = Author(root.transform, false);
                FitThumbs(root.transform, pose);
                EditorUtility.SetDirty(pose);
                AssetDatabase.SaveAssets();
                Debug.Log("[P2-C] Saved explicit relaxed/closed endpoints for 30 joints.");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        private static PairedGripPose.Hand Author(Transform root, bool player)
        {
            var model = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(PairedGripPose.AvatarResource), root);
            var animator = model.GetComponent<Animator>();
            animator.enabled = false;
            model.transform.localScale = Vector3.one * PairedGripPose.AvatarScale;
            var fingers = PairedGripPose.MapFingers(animator);
            var result = new PairedGripPose.Hand();
            using (var handler = new HumanPoseHandler(animator.avatar, animator.transform))
            {
                var human = new HumanPose();
                handler.GetHumanPose(ref human);
                SetCurls(ref human, false);
                handler.SetHumanPose(ref human);
                result.openFingerRotations = Read(fingers);
                SetCurls(ref human, true);
                handler.SetHumanPose(ref human);
            }
            result.fingerRotations = Read(fingers);
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 forward = (fingers[6].position - wrist.position).normalized;
            Vector3 dorsal = Vector3.Cross(fingers[3].position - fingers[12].position, forward).normalized;
            Vector3 desiredForward = player ? new Vector3(-0.28f, 0.87f, 0.40f) : new Vector3(0.25f, 0.70f, -0.67f);
            Vector3 across = Vector3.Cross(new Vector3(0.25f, 0.70f, -0.67f), new Vector3(-0.28f, 0.87f, 0.40f)).normalized;
            Vector3 side = Vector3.Slerp(Vector3.right, across, 0.65f);
            // The measured curl side is negative: this basis is truly dorsal.
            Vector3 desiredDorsal = player ? side : -side;
            result.wristRotation = Quaternion.LookRotation(desiredForward, desiredDorsal)
                * Quaternion.Inverse(Quaternion.LookRotation(forward, dorsal)) * wrist.rotation;
            result.wristRotation = Quaternion.AngleAxis(-15f, desiredForward) * result.wristRotation;
            result.wristPosition = player ? new Vector3(0.031f, -0.057f, -0.023f)
                : new Vector3(-0.031f, -0.050f, 0.029f);
            return result;
        }

        private static void FitThumbs(Transform root, PairedGripPose pose)
        {
            var player = PreviewHand(root, pose.player);
            var opponent = PreviewHand(root, pose.opponent);
            FitThumb(player, opponent, pose.player, 0.012f);
            FitThumb(opponent, player, pose.opponent, 0.022f);
            UnityEngine.Object.DestroyImmediate(player.gameObject);
            UnityEngine.Object.DestroyImmediate(opponent.gameObject);
        }

        private static Animator PreviewHand(Transform root, PairedGripPose.Hand pose)
        {
            var model = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(PairedGripPose.AvatarResource), root);
            model.transform.localScale = Vector3.one * PairedGripPose.AvatarScale;
            var animator = model.GetComponent<Animator>(); animator.enabled = false;
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            model.transform.rotation = pose.wristRotation * Quaternion.Inverse(wrist.rotation);
            model.transform.position += pose.wristPosition - wrist.position;
            pose.Apply(PairedGripPose.MapFingers(animator));
            return animator;
        }

        private static void FitThumb(Animator hand, Animator partner, PairedGripPose.Hand pose, float setback)
        {
            var fingers = PairedGripPose.MapFingers(hand);
            var other = PairedGripPose.MapFingers(partner);
            var wrist = hand.GetBoneTransform(HumanBodyBones.RightHand);
            var partnerWrist = partner.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 forward = (fingers[6].position - wrist.position).normalized;
            Vector3 dorsal = Vector3.Cross(fingers[3].position - fingers[12].position, forward).normalized;
            Vector3 otherForward = (other[6].position - partnerWrist.position).normalized;
            Vector3 otherDorsal = Vector3.Cross(other[3].position - other[12].position, otherForward).normalized;
            var a = fingers[0]; var b = fingers[1]; var c = fingers[2];
            Vector3 tipAxis = c.InverseTransformDirection(c.position - b.position);
            // The two art guides lie beside the partner's index web, with a
            // vertical stagger for the top thumb. This is saved once, not runtime IK.
            Vector3 target = other[3].position + otherDorsal * 0.030f - Vector3.up * setback;
            Vector3 pole = a.position + forward * 0.04f + dorsal * 0.015f;
            ArmChain.SolveJoints(a.position, target, pole, Vector3.up,
                Vector3.Distance(a.position, b.position), Vector3.Distance(b.position, c.position),
                out Vector3 middle, out Vector3 distal);
            a.rotation = Quaternion.FromToRotation(b.position - a.position, middle - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, distal - b.position) * b.rotation;
            c.rotation = Quaternion.FromToRotation(c.TransformDirection(tipAxis), forward) * c.rotation;
            for (int i = 0; i < 3; i++)
            {
                pose.fingerRotations[i] = fingers[i].localRotation;
                pose.openFingerRotations[i] = Quaternion.Slerp(pose.fingerRotations[i], pose.openFingerRotations[i], 0.08f);
            }
        }

        private static void SetCurls(ref HumanPose human, bool closed)
        {
            string[] digits = { "Thumb", "Index", "Middle", "Ring", "Little" };
            for (int i = 0; i < HumanTrait.MuscleCount; i++)
            {
                string name = HumanTrait.MuscleName[i];
                if (!name.Contains("Right")) continue;
                for (int digit = 0; digit < 5; digit++)
                {
                    if (!name.Contains(digits[digit])) continue;
                    if (name.Contains("Spread")) human.muscles[i] = digit == 0 ? 0.60f : 0f;
                    else if (name.Contains("Stretched"))
                    {
                        if (digit == 0) human.muscles[i] = closed ? -0.15f : 0.25f;
                        else human.muscles[i] = closed ? (name.Contains("2") ? -0.50f : -0.35f)
                            : (name.Contains("1") ? 0.20f : 0.30f);
                    }
                }
            }
        }

        private static Quaternion[] Read(Transform[] fingers)
        {
            var rotations = new Quaternion[fingers.Length];
            for (int i = 0; i < fingers.Length; i++) rotations[i] = fingers[i].localRotation;
            return rotations;
        }

        [MenuItem("扳手腕/P2-C/Open Contact Pose Preview")]
        public static void Preview()
        {
            var pose = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
            if (pose == null) throw new InvalidOperationException("Create the contact draft first.");
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Open paired contact preview");
            for (int side = 0; side < 2; side++)
            {
                var model = UnityEngine.Object.Instantiate(Resources.Load<GameObject>(PairedGripPose.AvatarResource), root.transform);
                model.name = side == 0 ? "Player" : "Opponent";
                model.transform.localScale = Vector3.one * PairedGripPose.AvatarScale;
                var animator = model.GetComponent<Animator>(); animator.enabled = false;
                var hand = side == 0 ? pose.player : pose.opponent;
                var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                model.transform.rotation = hand.wristRotation * Quaternion.Inverse(wrist.rotation);
                model.transform.position += hand.wristPosition - wrist.position;
                hand.Apply(PairedGripPose.MapFingers(animator));
            }
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, Vector3.one * 0.3f), false);
        }

        [MenuItem("扳手腕/P2-C/Load Open Endpoints In Selected Preview")]
        public static void LoadOpen()
        {
            var root = SelectedRoot();
            var pose = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
            for (int side = 0; side < 2; side++)
            {
                var animator = root.Find(side == 0 ? "Player" : "Opponent").GetComponent<Animator>();
                var hand = side == 0 ? pose.player : pose.opponent;
                var bones = PairedGripPose.MapFingers(animator);
                for (int i = 0; i < bones.Length; i++) bones[i].localRotation = hand.openFingerRotations[i];
            }
        }

        [MenuItem("扳手腕/P2-C/Save Selected Closed Endpoints")]
        public static void SaveClosed() => Save(false);
        [MenuItem("扳手腕/P2-C/Save Selected Open Endpoints")]
        public static void SaveOpen() => Save(true);
        private static Transform SelectedRoot()
        {
            var root = Selection.activeTransform;
            if (root == null || root.name != RootName) throw new InvalidOperationException("Select P2C Contact Pose Authoring root.");
            return root;
        }
        private static void Save(bool open)
        {
            var root = SelectedRoot();
            var pose = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
            Undo.RecordObject(pose, "Save paired contact endpoints");
            for (int side = 0; side < 2; side++)
            {
                var animator = root.Find(side == 0 ? "Player" : "Opponent").GetComponent<Animator>();
                var hand = side == 0 ? pose.player : pose.opponent;
                var rotations = Read(PairedGripPose.MapFingers(animator));
                if (open) hand.openFingerRotations = rotations;
                else
                {
                    var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    hand.wristPosition = root.InverseTransformPoint(wrist.position);
                    hand.wristRotation = Quaternion.Inverse(root.rotation) * wrist.rotation;
                    hand.fingerRotations = rotations;
                }
            }
            EditorUtility.SetDirty(pose); AssetDatabase.SaveAssets();
        }
    }
}
