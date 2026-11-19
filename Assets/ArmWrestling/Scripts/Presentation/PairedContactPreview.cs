#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace ArmWrestlingVR.EditorTools
{
    // A disposable extension of the existing paired-pose preview, not a game scene.
    public sealed class PairedContactPreview : MonoBehaviour
    {
        public HumanoidGripDefinition definition;
        public GripAvatarProfile player, opponent;
        [Range(0f,1f)] public float closure = 1f;
        [NonSerialized] public PairedGripPose pose;
        [NonSerialized] public Animator[] hands = new Animator[2];
        private readonly System.Collections.Generic.List<Mesh> crops = new System.Collections.Generic.List<Mesh>();

        [MenuItem("扳手腕/P2-D/Open Surface Contact Preview")]
        public static void Open()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play before authoring a grip.");
            var root = new GameObject("P2D Surface Contact Authoring") { hideFlags = HideFlags.DontSaveInEditor };
            Undo.RegisterCreatedObjectUndo(root,"Open surface grip preview");
            var preview = root.AddComponent<PairedContactPreview>();
            preview.definition = Resources.Load<HumanoidGripDefinition>("P2D/ArmWrestlingGrip");
            preview.player = preview.opponent = Resources.Load<GripAvatarProfile>("P2D/RocketboxProfile");
            preview.Rebuild(); Selection.activeGameObject = root;
            SceneView.lastActiveSceneView?.Frame(new Bounds(Vector3.zero,Vector3.one*0.28f),false);
        }

        public void Rebuild()
        {
            Clear();
            pose = HumanoidGripBuilder.Build(definition,player,opponent,out _,out _);
            for (int side = 0; side < 2; side++)
            {
                var profile = side == 0 ? player : opponent;
                var hand = side == 0 ? pose.player : pose.opponent;
                var copy = Instantiate(profile.prefab,transform,false); copy.name = side == 0 ? "Player hand" : "Opponent hand";
                copy.transform.localScale = Vector3.one*profile.modelScale;
                var animator = copy.GetComponent<Animator>(); animator.enabled = false;
                var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
                copy.transform.rotation = transform.rotation*hand.wristRotation*Quaternion.Inverse(wrist.rotation)*copy.transform.rotation;
                copy.transform.position += transform.TransformPoint(hand.wristPosition)-wrist.position;
                foreach (var skin in copy.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (Array.IndexOf(skin.bones,wrist)<0) { skin.enabled = false; continue; }
                    var crop = ArmMeshCrop.RightArm(skin,animator.GetBoneTransform(HumanBodyBones.RightUpperArm),true);
                    if (crop == null) { skin.enabled = false; continue; }
                    crops.Add(crop); skin.sharedMesh = crop; skin.updateWhenOffscreen = true;
                }
                hands[side] = animator;
            }
            ApplyClosure(); SceneView.RepaintAll();
        }

        public void ApplyClosure()
        {
            if (pose == null) return;
            for (int s = 0; s < 2; s++)
            {
                if (hands[s] == null) continue;
                var hand = s == 0 ? pose.player : pose.opponent;
                var fingers = PairedGripPose.MapFingers(hands[s]);
                for (int i = 0; i < 15; i++) fingers[i].localRotation = Quaternion.Slerp(hand.openFingerRotations[i],hand.fingerRotations[i],closure);
            }
        }

        private void Clear()
        {
            while (transform.childCount>0) DestroyImmediate(transform.GetChild(0).gameObject);
            foreach (var mesh in crops) if (mesh != null) DestroyImmediate(mesh);
            crops.Clear();
            if (pose != null) DestroyImmediate(pose);
        }
        private void OnDestroy() => Clear();

        // Reproducible capture of the same editable preview, including target guides.
        public static void Capture()
        {
            Open();
            var view = Selection.activeGameObject.GetComponent<PairedContactPreview>();
            try
            {
                var material = new Material(Shader.Find("Standard"));
                for (int side = 0; side < 2; side++)
                {
                    var hand = side == 0 ? view.pose.player : view.pose.opponent;
                    for (int i = 0; i < hand.contactPoints.Length; i++)
                    {
                        var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.transform.SetParent(view.transform,false);
                        dot.transform.localPosition = hand.contactPoints[i]; dot.transform.localScale = Vector3.one*0.0035f;
                        DestroyImmediate(dot.GetComponent<Collider>());
                        dot.GetComponent<Renderer>().sharedMaterial = material;
                        var colour = new MaterialPropertyBlock(); colour.SetColor("_Color",side == 0 ? Color.cyan : Color.green);
                        dot.GetComponent<Renderer>().SetPropertyBlock(colour);
                        if (i < 2) continue;
                        var line = dot.AddComponent<LineRenderer>(); line.sharedMaterial = material;
                        line.startColor = line.endColor = Color.yellow; line.startWidth = line.endWidth = 0.001f;
                        line.positionCount = 2; line.SetPositions(new[] { hand.contactPoints[i],hand.padPoints[i-2] });
                    }
                }
                RenderSettings.skybox = null; RenderSettings.ambientLight = new Color(0.65f,0.65f,0.65f);
                var light = new GameObject("Preview light").AddComponent<Light>(); light.transform.SetParent(view.transform);
                light.type = LightType.Directional; light.intensity = 0.8f; light.transform.rotation = Quaternion.Euler(30f,150f,0f);
                var camera = new GameObject("Contact guide camera").AddComponent<Camera>(); camera.transform.SetParent(view.transform);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(0.05f,0.05f,0.06f);
                camera.nearClipPlane = 0.01f; camera.fieldOfView = 42f;
                var rt = new RenderTexture(768,576,24); var pixels = new Texture2D(768,576,TextureFormat.RGB24,false);
                camera.targetTexture = rt;
                string folder = System.IO.Path.GetFullPath(".p0-local/p2d-cross-avatar/contact-guide-preview"); System.IO.Directory.CreateDirectory(folder);
                for (int i = 0; i < 3; i++)
                {
                    camera.transform.position = i == 0 ? new Vector3(-0.12f,0.11f,-0.27f) : i == 1 ? new Vector3(-0.28f,0.08f,0.04f) : new Vector3(0.28f,0.08f,-0.04f);
                    camera.transform.LookAt(Vector3.zero,Vector3.up); camera.Render(); RenderTexture.active = rt;
                    pixels.ReadPixels(new Rect(0,0,768,576),0,0); pixels.Apply();
                    // Screen-space guides match Scene Handles: deliberately visible through skin.
                    // These are diagnostic overlays, never used in the main-scene comparison frames.
                    for (int side = 0; side < 2; side++)
                    {
                        var hand = side == 0 ? view.pose.player : view.pose.opponent;
                        foreach (Vector3 point in hand.contactPoints)
                        {
                            Vector3 screen = camera.WorldToScreenPoint(point);
                            if (screen.z <= 0f) continue;
                            for (int x = -4; x <= 4; x++) for (int y = -4; y <= 4; y++)
                            {
                                int px = Mathf.RoundToInt(screen.x)+x, py = Mathf.RoundToInt(screen.y)+y;
                                if (x*x+y*y <= 16 && px>=0 && py>=0 && px<768 && py<576)
                                    pixels.SetPixel(px,py,side == 0 ? Color.cyan : Color.green);
                            }
                        }
                    }
                    pixels.Apply();
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder,"targets-"+i+".png"),pixels.EncodeToPNG());
                }
                RenderTexture.active = null; camera.targetTexture = null;
                DestroyImmediate(pixels); DestroyImmediate(rt); DestroyImmediate(material);
                Debug.Log("[P2-D] Editable paired surface preview rendered: " + folder);
            }
            finally { DestroyImmediate(view.gameObject); }
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
    }

}
#endif
