using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class AuthoredGripAuthoring
    {
        public const string Resource = "P2D/AuthoredPairedGrip";
        private const string AssetPath = "Assets/ArmWrestling/Resources/" + Resource + ".asset";
        internal static readonly string[] BoneLabels = Enumerable.Range(0, 15)
            .Select(i => PairedGripPose.Finger(i).ToString().Substring("Right".Length))
            .Append("Wrist").ToArray();

        [MenuItem("扳手腕/P2-E/Open Authored Grip Preview")]
        public static void Open() => OpenPreview(Resources.Load<HumanoidGripDefinition>(Resource));

        private static PairedContactPreview OpenPreview(HumanoidGripDefinition definition)
        {
            if (Application.isPlaying || definition == null)
                throw new InvalidOperationException("Open a saved grip definition in Edit mode.");
            var root = new GameObject("P2-E Paired Bone Authoring") { hideFlags = HideFlags.DontSaveInEditor };
            Undo.RegisterCreatedObjectUndo(root, "Open authored grip");
            var preview = root.AddComponent<PairedContactPreview>();
            preview.definition = definition;
            preview.player = preview.opponent = Resources.Load<GripAvatarProfile>("P2D/RocketboxProfile");
            try { preview.Rebuild(); }
            catch { UnityEngine.Object.DestroyImmediate(root); throw; }
            Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero, Vector3.one * 0.25f), false);
            return preview;
        }

        internal static void SaveFromPreview(PairedContactPreview view, int side, bool contest)
        {
            var definition = view.definition;
            var endpoint = side == 0 ? definition.authoredPlayer : definition.authoredOpponent;
            if (view.hands[side] == null || endpoint == null || endpoint.sourceAvatar != view.hands[side].avatar)
            {
                EditorUtility.DisplayDialog("Grip source Avatar", "Select the source Avatar to edit these keys. Use the destination Profile for retargeted calibration.", "OK");
                return;
            }
            Undo.RecordObject(definition, "Save paired hand endpoint");
            // Rebuild the neutral reference independently of the visible edits.
            var reference = UnityEngine.Object.Instantiate(definition);
            reference.authoredPlayer = reference.authoredOpponent = null;
            PairedGripPose baseline = null;
            try
            {
                baseline = HumanoidGripBuilder.Build(reference, view.player, view.opponent, out _, out _, false);
                Snapshot(view, side, side == 0 ? baseline.player : baseline.opponent, endpoint, contest);
                var visiblePose = side == 0 ? view.pose.player : view.pose.opponent;
                visiblePose.fingerRotations = endpoint.contestBones;
                visiblePose.openFingerRotations = endpoint.readyBones;
                EditorUtility.SetDirty(definition);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(baseline);
                UnityEngine.Object.DestroyImmediate(reference);
            }
        }

        private static void Snapshot(PairedContactPreview view, int side, PairedGripPose.Hand baseline,
            HumanoidGripDefinition.AuthoredHand key, bool contest)
        {
            var hand = view.hands[side];
            var wrist = hand.GetBoneTransform(HumanBodyBones.RightHand);
            key.sourceAvatar = hand.avatar;
            key.wristOffset = (view.transform.InverseTransformPoint(wrist.position) - baseline.wristPosition) / baseline.palmLength;
            key.wristDelta = Quaternion.Inverse(view.transform.rotation) * wrist.rotation * Quaternion.Inverse(baseline.wristRotation);
            var rotations = PairedGripPose.MapFingers(hand).Select(bone => bone.localRotation).ToArray();
            using var handler = new HumanPoseHandler(hand.avatar, hand.transform);
            var human = new HumanPose();
            handler.GetHumanPose(ref human);
            var muscles = HumanoidGripBuilder.ReadFingerMuscles(human);
            if (contest) { key.contestBones = rotations; key.contestMuscles = muscles; }
            else { key.readyBones = rotations; key.readyMuscles = muscles; }
        }

        // A reproducible first blocking pass, applied to the two visible rigs.
        // Angles are individual art decisions from the neutral preview, not a fit/search.
        public static void CreateDraft()
        {
            if (AssetDatabase.LoadAssetAtPath<HumanoidGripDefinition>(AssetPath) != null)
                throw new InvalidOperationException("The authored asset already exists. Open the preview to edit its keys.");
            var legacy = Resources.Load<HumanoidGripDefinition>("P2D/LegacyHumanoidGrip");
            var view = OpenPreview(legacy);
            var asset = UnityEngine.Object.Instantiate(legacy);
            asset.name = "AuthoredPairedGrip";
            try
            {
                asset.authoredPlayer = new HumanoidGripDefinition.AuthoredHand();
                asset.authoredOpponent = new HumanoidGripDefinition.AuthoredHand();
                for (int side = 0; side < 2; side++)
                {
                    var neutral = side == 0 ? view.pose.player : view.pose.opponent;
                    var key = side == 0 ? asset.authoredPlayer : asset.authoredOpponent;
                    var hand = view.hands[side];
                    var wrist = hand.GetBoneTransform(HumanBodyBones.RightHand);
                    Vector3 dorsal = wrist.rotation * neutral.palmDorsal;
                    Vector3 forward = (hand.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position - wrist.position).normalized;
                    // Keep the supported wrist positions; add asymmetric palm roll to clear the webs.
                    Quaternion roll = Quaternion.AngleAxis(side == 0 ? -5f : -7f, forward);
                    view.PlaceWrist(side, wrist.position, roll * wrist.rotation);
                    dorsal = roll * dorsal;
                    PoseDigits(hand, neutral, dorsal, false, side);
                    Snapshot(view, side, neutral, key, false);
                    PoseDigits(hand, neutral, dorsal, true, side);
                    RefineContest(hand, dorsal, side);
                    Snapshot(view, side, neutral, key, true);
                }
                AssetDatabase.CreateAsset(asset, AssetPath);
                AssetDatabase.SaveAssets();
                AttachToMain();
            }
            finally { UnityEngine.Object.DestroyImmediate(view.gameObject); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        // Second art pass: the first close-up showed raised thumbs and tips
        // reaching through the partner. Fold the thumbs and ease those curls.
        private static void RefineContest(Animator hand, Vector3 dorsal, int side)
        {
            var bones = PairedGripPose.MapFingers(hand);
            for (int j = 0; j <= 1; j++)
            {
                Vector3 current = bones[j + 1].position - bones[j].position;
                Vector3 towardWeb = Vector3.Lerp(bones[3].position, bones[6].position, 0.22f) - bones[j].position;
                float turn = j == 1 ? 16f : side == 0 ? 25f : 21f;
                var latch = Quaternion.FromToRotation(current, towardWeb);
                bones[j].rotation = Quaternion.RotateTowards(Quaternion.identity, latch, turn) * bones[j].rotation;
            }
            for (int digit = 1; digit < 5; digit++)
            {
                int middle = digit * 3 + 1;
                Vector3 axis = Vector3.Cross(bones[middle].position - bones[middle - 1].position, -dorsal).normalized;
                bones[middle].rotation = Quaternion.AngleAxis(digit <= 2 ? -9f : -6f, axis) * bones[middle].rotation;
            }
        }

        private static void PoseDigits(Animator hand, PairedGripPose.Hand neutral, Vector3 dorsal, bool contest, int side)
        {
            Transform[] bones = PairedGripPose.MapFingers(hand);
            for (int i = 0; i < bones.Length; i++) bones[i].localRotation = neutral.openFingerRotations[i];
            // Index and middle wrap higher across the back; ring and pinky curl progressively around the palm.
            Vector3[] bend = side == 0
                ? new[] { new Vector3(8, 18, 9), new Vector3(52, 54, 20), new Vector3(55, 63, 24), new Vector3(59, 66, 26), new Vector3(65, 69, 30) }
                : new[] { new Vector3(4, 22, 11), new Vector3(48, 54, 22), new Vector3(53, 60, 24), new Vector3(59, 64, 28), new Vector3(63, 68, 31) };
            for (int digit = 0; digit < 5; digit++)
            {
                int first = digit * 3;
                Vector3 direction = bones[first + 1].position - bones[first].position;
                Vector3 axis = Vector3.Cross(direction, -dorsal).normalized;
                for (int segment = 0; segment < 3; segment++)
                {
                    var joint = bones[first + segment];
                    joint.rotation = Quaternion.AngleAxis(bend[digit][segment] * (contest ? 1f : 0.24f), axis) * joint.rotation;
                }
            }
        }

        private static void AttachToMain()
        {
            var scene = EditorSceneManager.OpenScene(SceneGenerator.MainScenePath);
            var bootstrap = UnityEngine.Object.FindObjectOfType<RuntimeBootstrap>();
            bootstrap.authoredGrip = Resources.Load<HumanoidGripDefinition>(Resource);
            bootstrap.useAuthoredGrip = false;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

    }
}
