using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class HumanoidGripAssets
    {
        [MenuItem("扳手腕/P2-D/Prepare Profiles And Main Scene")]
        public static void Prepare()
        {
            const string folder = "Assets/ArmWrestling/Resources/P2D";
            if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/ArmWrestling/Resources","P2D");
            var definition = AssetDatabase.LoadAssetAtPath<HumanoidGripDefinition>(folder + "/ArmWrestlingGrip.asset");
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<HumanoidGripDefinition>();
                AssetDatabase.CreateAsset(definition,folder + "/ArmWrestlingGrip.asset");
            }
            var profile = AssetDatabase.LoadAssetAtPath<GripAvatarProfile>(folder + "/RocketboxProfile.asset");
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<GripAvatarProfile>();
                profile.prefab = Resources.Load<GameObject>(PairedGripPose.AvatarResource);
                profile.modelScale = 0.94f;
                AssetDatabase.CreateAsset(profile,folder + "/RocketboxProfile.asset");
            }
            const string localFolder = "Assets/ArmWrestling/Resources/P2DLocal";
            var localPrefab = Resources.Load<GameObject>("Characters/Ch03");
            if (localPrefab != null && !AssetDatabase.IsValidFolder(localFolder))
                AssetDatabase.CreateFolder("Assets/ArmWrestling/Resources","P2DLocal");
            if (localPrefab != null && AssetDatabase.LoadAssetAtPath<GripAvatarProfile>(localFolder + "/Ch03Profile.asset") == null)
            {
                var local = ScriptableObject.CreateInstance<GripAvatarProfile>();
                local.prefab = localPrefab; local.modelScale = 1.12f;
                local.originFromSeat = new Vector3(0f,-0.22f,-0.08f); local.footLift = 0f;
                local.elbowSupportHeight = 0.121f; local.bodySideAtLimits = Vector2.zero; local.uprightAtLimits = Vector2.zero;
                AssetDatabase.CreateAsset(local,localFolder + "/Ch03Profile.asset");
                SetCh03OpponentCalibration(local);
                EditorUtility.SetDirty(local);
            }
            var targets = AssetDatabase.LoadAssetAtPath<GripContactTargets>(folder + "/SurfaceTargets.asset");
            if (targets == null)
            {
                targets = ScriptableObject.CreateInstance<GripContactTargets>();
                AssetDatabase.CreateAsset(targets,folder + "/SurfaceTargets.asset");
            }
            definition.contactTargets = targets; EditorUtility.SetDirty(definition);
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneGenerator.MainScenePath,OpenSceneMode.Single);
            var bootstrap = Object.FindObjectOfType<RuntimeBootstrap>();
            bootstrap.useGripSurfaceTargets = false;
            bootstrap.useHumanoidGrip = true; bootstrap.usePairedGrip = true;
            bootstrap.legacyHumanoidGrip = Resources.Load<HumanoidGripDefinition>("P2D/LegacyHumanoidGrip");
            bootstrap.humanoidGrip = Resources.Load<HumanoidGripDefinition>("P2D/ConstrainedHumanoidGrip") ?? definition; bootstrap.playerAvatar = profile; bootstrap.opponentAvatar = profile;
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            Debug.Log("[P2-D] Main scene configured. Select profiles in Runtime Bootstrap to switch models.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("扳手腕/P2-E/Apply Ch03 Middle Finger Calibration", true)]
        private static bool CanCalibrateCh03() => !EditorApplication.isPlayingOrWillChangePlaymode
            && Resources.Load<GripAvatarProfile>("P2DLocal/Ch03Profile") != null;

        [MenuItem("扳手腕/P2-E/Apply Ch03 Middle Finger Calibration")]
        private static void CalibrateCh03()
        {
            var profile = Resources.Load<GripAvatarProfile>("P2DLocal/Ch03Profile");
            Undo.RecordObject(profile, "Calibrate Ch03 opponent middle finger");
            SetCh03OpponentCalibration(profile);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
        }

        // Numeric adjustments authored on the local Avatar; its mesh and profile stay local.
        private static void SetCh03OpponentCalibration(GripAvatarProfile profile)
        {
            profile.opponentCalibrationGrip = Resources.Load<HumanoidGripDefinition>("P2D/ImagePairedGrip");
            var rocketbox = Resources.Load<GripAvatarProfile>("P2D/RocketboxProfile");
            GripAvatarProfile.FingerAdjustment Joint(GripAvatarProfile player, HumanBodyBones bone, Vector3 angles) =>
                new GripAvatarProfile.FingerAdjustment { player = player, joint = bone, localEuler = angles };
            var proximal = new Vector3(-2.568f, 0.087f, -4.214f);
            var intermediate = new Vector3(-7.451f, 0.293f, -4.491f);
            var distal = new Vector3(-2.464f, -0.691f, -2.858f);
            // Fractions of the sculpted correction selected against the two posed palm surfaces.
            profile.opponentFingerAdjustments = new[] {
                Joint(profile, HumanBodyBones.RightMiddleProximal, proximal * 0.14f),
                Joint(profile, HumanBodyBones.RightMiddleIntermediate, intermediate * 0.14f),
                Joint(profile, HumanBodyBones.RightMiddleDistal, distal * 0.14f),
                Joint(rocketbox, HumanBodyBones.RightMiddleProximal, proximal * 0.71f),
                Joint(rocketbox, HumanBodyBones.RightMiddleIntermediate, intermediate * 0.71f),
                Joint(rocketbox, HumanBodyBones.RightMiddleDistal, distal * 0.71f)
            };
        }
    }
}
