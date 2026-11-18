using System;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    // The neutral layout is built in metres. Wrist and finger guides are
    // evaluated against both visible hands once, then stored as one pose.
    public static class PairedGripAuthoring
    {
        private const string AssetPath = "Assets/ArmWrestling/Resources/P2A/NeutralPairedGrip.asset";
        private const string PreviewName = "P2A Paired Grip Authoring";

        public static void Prepare()
        {
            RocketboxAssets.Prepare();
            if (AssetDatabase.LoadAssetAtPath<PairedGripPose>(AssetPath) == null) BuildDraft();
        }

        [MenuItem("扳手腕/P2-A/Open Neutral Grip Preview")]
        public static void OpenPreview()
        {
            var root = new GameObject(PreviewName);
            Undo.RegisterCreatedObjectUndo(root, "Open neutral grip preview");
            var prefab = Resources.Load<GameObject>(PairedGripPose.AvatarResource);
            var player = Model(prefab, root.transform, "Player");
            var opponent = Model(prefab, root.transform, "Opponent");
            var pose = AssetDatabase.LoadAssetAtPath<PairedGripPose>(AssetPath);
            if (pose != null && pose.IsComplete)
            {
                Preview(player, pose.player, root.transform);
                Preview(opponent, pose.opponent, root.transform);
            }
            else ComposeNeutral(player, opponent);
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(root.transform.position, Vector3.one * 0.27f), false);
        }

        [MenuItem("扳手腕/P2-A/Save Selected Neutral Grip")]
        public static void SavePreview()
        {
            var root = Selection.activeTransform;
            if (root == null || root.name != PreviewName)
                throw new InvalidOperationException("Select the paired grip authoring root.");
            Save(root.Find("Player").GetComponent<Animator>(), root.Find("Opponent").GetComponent<Animator>(), root);
        }

        public static void BuildDraft()
        {
            var prefab = Resources.Load<GameObject>(PairedGripPose.AvatarResource);
            var root = new GameObject(PreviewName);
            try
            {
                var player = Model(prefab, root.transform, "Player");
                var opponent = Model(prefab, root.transform, "Opponent");
                ComposeNeutral(player, opponent);
                Save(player, opponent, root.transform);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static Animator Model(GameObject prefab, Transform root, string name)
        {
            var instance = UnityEngine.Object.Instantiate(prefab, root);
            instance.name = name;
            instance.transform.localScale = Vector3.one * PairedGripPose.AvatarScale;
            var animator = instance.GetComponent<Animator>();
            if (!animator.isHuman || !animator.avatar.isValid) throw new InvalidOperationException("Prepare the Rocketbox Avatar first.");
            animator.enabled = false;
            return animator;
        }

        private static void ComposeNeutral(Animator player, Animator opponent)
        {
            // Right-hand web to web, with a small vertical stagger so thumbs
            // have room over the opposite index rather than crossing at the wrist.
            Vector3 playerAxis = new Vector3(-0.28f, 0.87f, 0.40f);
            Vector3 opponentAxis = new Vector3(0.25f, 0.70f, -0.67f);
            Vector3 across = Vector3.Cross(opponentAxis, playerAxis).normalized;
            Vector3 dorsal = Vector3.Slerp(Vector3.right, across, 0.65f);
            PlaceWrist(player, new Vector3(0.030f, -0.057f, -0.025f), playerAxis, dorsal);
            PlaceWrist(opponent, new Vector3(-0.030f, -0.050f, 0.031f), opponentAxis, -dorsal);
            Wrap(player, opponent);
            Wrap(opponent, player);
        }

        private static void PlaceWrist(Animator animator, Vector3 position, Vector3 forward, Vector3 dorsal)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            Axes(animator, out Vector3 sourceForward, out Vector3 sourceDorsal);
            var delta = Quaternion.LookRotation(forward, dorsal)
                * Quaternion.Inverse(Quaternion.LookRotation(sourceForward, sourceDorsal));
            MoveModelToWrist(animator, new Pose(position, delta * wrist.rotation));
        }

        private static void MoveModelToWrist(Animator animator, Pose pose)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var root = animator.transform;
            root.rotation = pose.rotation * Quaternion.Inverse(wrist.rotation) * root.rotation;
            root.position += pose.position - wrist.position;
        }

        private static void Preview(Animator animator, PairedGripPose.Hand pose, Transform anchor)
        {
            MoveModelToWrist(animator, pose.WorldWrist(anchor));
            pose.Apply(PairedGripPose.MapFingers(animator));
        }

        private static void Wrap(Animator animator, Animator partner)
        {
            Axes(animator, out Vector3 forward, out _);
            Axes(partner, out Vector3 partnerForward, out Vector3 partnerDorsal);
            Vector3 acrossBack = Vector3.Cross(partnerDorsal, partnerForward).normalized;
            Vector3 partnerWrist = partner.GetBoneTransform(HumanBodyBones.RightHand).position;
            var joints = PairedGripPose.MapFingers(animator);
            // Place the distal knuckles on the opposite dorsal envelope, then
            // lay the tips down that back. The curve passes above the knuckles.
            for (int digit = 1; digit < 5; digit++)
            {
                Transform a = joints[digit * 3], b = joints[digit * 3 + 1], c = joints[digit * 3 + 2];
                Vector3 lastAxis = c.InverseTransformDirection(c.position - b.position);
                Vector3 fromPartner = a.position - partnerWrist;
                float upBack = Mathf.Clamp(Vector3.Dot(fromPartner, partnerForward) + 0.006f, 0.053f, 0.094f);
                float across = Mathf.Clamp(Vector3.Dot(fromPartner, acrossBack), -0.032f, 0.032f);
                Vector3 landing = partnerWrist + upBack * partnerForward + across * acrossBack + 0.023f * partnerDorsal;
                Vector3 passOver = a.position + 0.05f * forward;
                ArmChain.SolveJoints(a.position, landing, passOver, Vector3.up,
                    Vector3.Distance(a.position, b.position), Vector3.Distance(b.position, c.position),
                    out Vector3 bend, out Vector3 distal);
                Aim(a, b.position - a.position, bend - a.position);
                Aim(b, c.position - b.position, distal - b.position);
                Aim(c, c.TransformDirection(lastAxis), -partnerForward + 0.12f * partnerDorsal);
            }

            // Lay the thumb across its partner's index web. The two guides
            // come from each other's actual Humanoid hands in this neutral pose.
            Transform thumb = joints[0], mid = joints[1], end = joints[2];
            Vector3 tipAxis = end.InverseTransformDirection(end.position - mid.position);
            Vector3 web = partner.GetBoneTransform(HumanBodyBones.RightIndexProximal).position;
            Vector3 target = web + 0.013f * partnerDorsal + 0.008f * Vector3.up;
            Vector3 pole = thumb.position + 0.040f * Vector3.up - 0.018f * partnerDorsal;
            ArmChain.SolveJoints(thumb.position, target, pole, Vector3.up,
                Vector3.Distance(thumb.position, mid.position), Vector3.Distance(mid.position, end.position),
                out Vector3 thumbJoint, out Vector3 thumbEnd);
            Aim(thumb, mid.position - thumb.position, thumbJoint - thumb.position);
            Aim(mid, end.position - mid.position, thumbEnd - mid.position);
            Aim(end, end.TransformDirection(tipAxis), 0.8f * partnerDorsal - 0.60f * partnerForward);
        }

        private static void Aim(Transform joint, Vector3 current, Vector3 desired) =>
            joint.rotation = Quaternion.FromToRotation(current, desired) * joint.rotation;

        private static void Axes(Animator animator, out Vector3 forward, out Vector3 dorsal)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
            var little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            forward = (middle.position - wrist.position).normalized;
            dorsal = Vector3.Cross(index.position - little.position, forward).normalized;
        }

        private static void Save(Animator player, Animator opponent, Transform anchor)
        {
            var pose = AssetDatabase.LoadAssetAtPath<PairedGripPose>(AssetPath);
            if (pose == null)
            {
                pose = ScriptableObject.CreateInstance<PairedGripPose>();
                AssetDatabase.CreateAsset(pose, AssetPath);
            }
            Undo.RecordObject(pose, "Save neutral paired grip");
            pose.player = Read(player, anchor);
            pose.opponent = Read(opponent, anchor);
            EditorUtility.SetDirty(pose);
            AssetDatabase.SaveAssets();
            Debug.Log("[P2-A] Saved both wrist poses and both sets of 15 finger rotations.");
        }

        private static PairedGripPose.Hand Read(Animator animator, Transform anchor)
        {
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var fingers = PairedGripPose.MapFingers(animator);
            var hand = new PairedGripPose.Hand
            {
                wristPosition = anchor.InverseTransformPoint(wrist.position),
                wristRotation = Quaternion.Inverse(anchor.rotation) * wrist.rotation
            };
            for (int i = 0; i < fingers.Length; i++) hand.fingerRotations[i] = fingers[i].localRotation;
            return hand;
        }
    }
}
