using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    public static class GripConstraintExperiment
    {
        [MenuItem("扳手腕/P2-D/Prepare Joint Limited Experiment")]
        public static void Prepare()
        {
            const string folder="Assets/ArmWrestling/Resources/P2D/";
            var contacts=AssetDatabase.LoadAssetAtPath<GripContactTargets>(folder+"ConstrainedContacts.asset");
            if(contacts==null)
            {
                contacts=Object.Instantiate(Resources.Load<GripContactTargets>("P2D/SurfaceTargets"));
                contacts.jointLimited=true;
                AssetDatabase.CreateAsset(contacts,folder+"ConstrainedContacts.asset");
            }
            var definition=AssetDatabase.LoadAssetAtPath<HumanoidGripDefinition>(folder+"ConstrainedHumanoidGrip.asset");
            if(definition==null)
            {
                // Preserve the comfortable wrist reference, not the previous point-chasing axes.
                definition=Object.Instantiate(Resources.Load<HumanoidGripDefinition>("P2D/LegacyHumanoidGrip"));
                definition.contactTargets=contacts;
                AssetDatabase.CreateAsset(definition,folder+"ConstrainedHumanoidGrip.asset");
            }
            AssetDatabase.SaveAssets();
            EditorSceneManager.OpenScene(SceneGenerator.MainScenePath,OpenSceneMode.Single);
            var bootstrap=Object.FindObjectOfType<RuntimeBootstrap>();
            bootstrap.humanoidGrip=definition;bootstrap.useHumanoidGrip=true;bootstrap.useGripSurfaceTargets=true;
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }

        [MenuItem("扳手腕/P2-D/Restore Stable Humanoid Default")]
        public static void SelectStableDefault()
        {
            EditorSceneManager.OpenScene(SceneGenerator.MainScenePath,OpenSceneMode.Single);
            var bootstrap=Object.FindObjectOfType<RuntimeBootstrap>();
            bootstrap.useHumanoidGrip=true;bootstrap.useGripSurfaceTargets=false;
            EditorSceneManager.MarkSceneDirty(bootstrap.gameObject.scene);EditorSceneManager.SaveScene(bootstrap.gameObject.scene);
            if(Application.isBatchMode) EditorApplication.Exit(0);
        }
    }
}
