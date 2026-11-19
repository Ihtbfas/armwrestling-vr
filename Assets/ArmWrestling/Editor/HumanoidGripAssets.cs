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
                local.elbowSupportHeight = 0.109f; local.bodySideAtLimits = Vector2.zero; local.uprightAtLimits = Vector2.zero;
                AssetDatabase.CreateAsset(local,localFolder + "/Ch03Profile.asset");
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneGenerator.MainScenePath,OpenSceneMode.Single);
            var bootstrap = Object.FindObjectOfType<RuntimeBootstrap>();
            bootstrap.useHumanoidGrip = true; bootstrap.usePairedGrip = true;
            bootstrap.humanoidGrip = definition; bootstrap.playerAvatar = profile; bootstrap.opponentAvatar = profile;
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);
            EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            Debug.Log("[P2-D] Main scene configured. Select profiles in Runtime Bootstrap to switch models.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
