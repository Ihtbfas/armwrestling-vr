using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ArmWrestlingVR.EditorTools
{
    // Run in a public checkout with -batchmode -executeMethod ArmWrestlingVR.EditorTools.P2FValidation.Run.
    // Press drives the actual HUD callbacks. Physical desktop input is tested separately.
    [InitializeOnLoad]
    public static class P2FValidation
    {
        private const string Key = "ArmWrestling.P2FValidation";
        private static readonly string[] Models = { "Male_Adult_01", "Female_Adult_01", "Sports_Female_02", "Male_Adult_03" };
        private static ArmWrestlingApp app;
        private static IEnumerator sequence;
        private static string output;
        private static RenderTexture target;
        private static Texture2D pixels;
        private static Camera front, side, detail;
        private static double start;
        private static int errors;
        private static string stage;
        private static bool authored;

        static P2FValidation()
        {
            EditorApplication.update += Tick;
            Application.logMessageReceived += (message, stack, type) =>
            {
                if (SessionState.GetBool(Key, false) && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                    errors++;
            };
        }

        public static void Run()
        {
            EditorSceneManager.OpenScene(SceneGenerator.MainScenePath, OpenSceneMode.Single);
            if (Resources.Load<GameObject>("Characters/Ch03") || Resources.Load<Texture2D>("HDRI/circus_arena") ||
                Resources.Load<Font>("ArmWrestlingFont"))
                throw new InvalidOperationException("Use the public-only checkout to capture P2F evidence.");
            if (Environment.GetCommandLineArgs().Contains("-p2fauthored"))
                Object.FindObjectOfType<RuntimeBootstrap>().useAuthoredGrip = true;
            SessionState.SetBool(Key, true);
            EditorApplication.EnterPlaymode();
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Key, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (sequence == null)
                {
                    start = EditorApplication.timeSinceStartup;
                    sequence = Validate();
                }
                Check(EditorApplication.timeSinceStartup - start < 280, "Timeout at " + stage);
                Check(errors == 0, "Runtime error at " + stage + ". Inspect the preceding Unity log.");
                if (sequence.MoveNext()) return;
                SessionState.SetBool(Key, false);
                Debug.Log("[P2F] PASS " + output);
                EditorApplication.Exit(0);
            }
            catch (Exception error)
            {
                SessionState.SetBool(Key, false);
                Debug.LogError("[P2F] " + stage + " " + error);
                EditorApplication.Exit(1);
            }
        }

        private static IEnumerator Validate()
        {
            stage = "startup";
            while (!(app = Object.FindObjectOfType<ArmWrestlingApp>()) || app.Pipeline == null ||
                !app.Pipeline.HasSample || app.state != AppState.DeviceCheck || !app.hud.primaryButton.Caption.Contains("标定"))
                yield return null;
            Check(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && app.dataSource == DataSourceMode.Simulated, "Graphics/offline mode");
            authored = Environment.GetCommandLineArgs().Contains("-p2fauthored");
            output = Path.GetFullPath(Path.Combine(Application.dataPath, "../.p0-local/p2f",
                (authored ? "authored-" : "default-") + DateTime.Now.ToString("yyyyMMdd-HHmmss")));
            Directory.CreateDirectory(output);
            target = new RenderTexture(960, 600, 24);
            pixels = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            app.hud.primaryButton.Press();
            while (app.state != AppState.Calibration || !app.hud.tertiaryButton.Caption.Contains("跳过")) yield return null;
            app.hud.tertiaryButton.Press();
            while (!Lobby()) yield return null;
            var appearance = GameObject.Find("AppearanceNext").GetComponent<VrButton>();
            var arena = GameObject.Find("ArenaNext").GetComponent<VrButton>();
            var geometry = new[] { "Table Top", "Player Seat", "Opponent Seat", "Player Elbow Pad", "Opponent Elbow Pad" }
                .Select(name => app.deviceRoot.Find(name)).Concat(new[] { app.armRig.gripAnchor }).ToArray();
            var locations = geometry.Select(t => t.localPosition).ToArray();
            var dimensions = geometry.Select(t => t.localScale).ToArray();
            float strength = app.opponentScale;
            int combinations = 0;
            var player = app.armRig.playerVisual;
            front = AnimationComparison.View(app.deviceRoot, new Vector3(-0.28f, 0.79f, -1.39f), new Vector3(0f, 0.27f, 0.36f));
            side = AnimationComparison.View(app.deviceRoot, new Vector3(-2.33f, 0.45f, 0.35f), new Vector3(-0.07f, -0.04f, 0.33f));
            detail = AnimationComparison.View(app.deviceRoot, Vector3.back, Vector3.zero);
            detail.nearClipPlane = Camera.main.nearClipPlane;
            detail.fieldOfView = 28f;
            for (int i = 0; i < Models.Length; i++)
            {
                stage = Models[i] + " selection";
                if (i > 0)
                {
                    var oldRole = app.role;
                    int oldTheme = app.SelectedArena;
                    appearance.Press();
                    yield return null;
                    Check(app.role == oldRole && app.SelectedArena == oldTheme, "Appearance changed another selection.");
                }
                Check(app.SelectedAppearance == i && app.armRig.playerVisual == player, "Opponent/player replacement failed.");
                var model = app.deviceRoot.GetComponentsInChildren<OpponentAvatar>().Single();
                var animator = model.GetComponent<Animator>();
                var mesh = model.GetComponentsInChildren<SkinnedMeshRenderer>().First(r => r.enabled);
                Check(Path.GetFileNameWithoutExtension(AssetDatabase.GetAssetPath(animator.avatar)) == Models[i],
                    "Expected " + Models[i] + ", found " + AssetDatabase.GetAssetPath(animator.avatar));
                Check(animator.isHuman && animator.avatar.isValid && model.UsesPairedGrip &&
                    model.CollisionExperiment.UsesHumanoidAdapter && model.CollisionExperiment.ResolvedContactPose.usesAuthoredEndpoints == authored,
                    "Humanoid/paired IK or requested grip path fell back.");
                Check(app.deviceRoot.GetComponentsInChildren<AvatarCollisionExperiment>().Length == 1, "Extra active grip experiment.");
                VerifySkin(animator, mesh);
                for (int t = 0; t < 3; t++)
                {
                    int theme = app.SelectedArena;
                    for (int r = 0; r < 3; r++)
                    {
                        app.hud.roleButtons[r].Press();
                        Check((int)app.role == r + 1 && app.SelectedAppearance == i && app.SelectedArena == theme &&
                            Mathf.Approximately(app.opponentScale, strength), "Strategy/visual/strength selections coupled.");
                        combinations++;
                    }
                    if (i == 0 && !authored)
                    {
                        app.hud.canvas.enabled = false;
                        app.armRig.ApplyAngle(0);
                        animator.Update(0);
                        model.ApplyPairedHandBones();
                        Capture(Camera.main, "venue-" + theme + ".png");
                        app.hud.canvas.enabled = true;
                    }
                    arena.Press();
                    yield return null;
                    Check(app.role == OpponentRole.Rhythmic && app.SelectedAppearance == i && app.SelectedArena == (theme + 1) % 3,
                        "Arena cycle changed the role/appearance or failed to wrap.");
                    var room = app.deviceRoot.Find("Arena Environment");
                    Check(room && room.gameObject.activeInHierarchy && room.GetComponentsInChildren<MeshRenderer>().Length > 0,
                        "Selected venue has no active geometry.");
                    Check(!app.deviceRoot.GetComponentsInChildren<Transform>().Any(x => x.name == "Replaced Arena Environment"),
                        "A previous venue remained active.");
                    for (int n = 0; n < geometry.Length; n++)
                        Check(geometry[n] && geometry[n].localPosition == locations[n] && geometry[n].localScale == dimensions[n],
                            "Changed fixed table, seat, elbow pad or grip-anchor geometry at index " + n);
                }
                // All body and hand sweeps share the same lighting and camera placement.
                while (app.SelectedArena != 1) { arena.Press(); yield return null; }
                app.hud.roleButtons[i % 3].Press();
                if (i == 1 && !authored)
                {
                    int selectedFrame = Time.frameCount;
                    while (Time.frameCount == selectedFrame) yield return null;
                    Capture(Camera.main, "lobby.png");
                    CaptureLobbyPanel();
                }
                stage = Models[i] + " ready";
                app.hud.primaryButton.Press();
                while (app.state != AppState.Ready || app.hud.primaryButton.Caption != "立即开始") yield return null;
                if (!authored)
                {
                    app.hud.canvas.enabled = false;
                    AimAtGrip();
                    Capture(detail, Models[i] + "-ready.png");
                    app.hud.canvas.enabled = true;
                }
                app.hud.primaryButton.Press();
                Check(app.state == AppState.Countdown, "Ready did not start countdown.");
                stage = Models[i] + " contest";
                while (app.state != AppState.Fighting || app.hud.secondaryButton.Caption != "暂停" ||
                    !animator.GetCurrentAnimatorClipInfo(0).Any(c => c.clip.name == "SeatedContest" && c.weight > 0.1f)) yield return null;
                int expectedArena = app.SelectedArena;
                app.CycleAppearance();
                app.CycleArena();
                Check(app.SelectedAppearance == i && app.SelectedArena == expectedArena, "Changed selection during contest.");
                var sweep = Sweep(model, animator);
                while (sweep.MoveNext()) yield return null;
                app.hud.secondaryButton.Press();
                while (app.state != AppState.Paused || app.hud.primaryButton.Caption != "继续") yield return null;
                app.hud.primaryButton.Press();
                Check(app.state == AppState.Fighting, "Resume failed.");
                app.autoDemo = true;
                stage = Models[i] + " result";
                while (app.state != AppState.Finished || app.hud.secondaryButton.Caption != "返回选择") yield return null;
                Check(app.outcome != MatchOutcome.None, "Match had no result.");
                Debug.Log($"[P2F] MATCH model={Models[i]} role={app.role} venue={app.SelectedArena} result={app.outcome} IK={model.IkPassCount} gripEvaluations={model.CollisionExperiment.EvaluationCount}");
                app.autoDemo = false;
                app.hud.secondaryButton.Press();
                while (!Lobby()) yield return null;
            }
            appearance.Press();
            while (app.SelectedArena != 0) { arena.Press(); yield return null; }
            Check(app.SelectedAppearance == 0 && app.deviceRoot.GetComponentsInChildren<OpponentAvatar>().Single().name.Contains(Models[0]),
                "Cannot restore original opponent.");
            Debug.Log($"[P2F] {combinations} combinations; 4 full matches, default restored; Editor {Application.unityVersion}, {SystemInfo.graphicsDeviceType}, font={FontProvider.UiFont.name}, authored={authored}");
        }

        private static bool Lobby() => app.state == AppState.Lobby && app.hud.primaryButton.Caption == "准备对战";

        private static void VerifySkin(Animator animator, SkinnedMeshRenderer skin)
        {
            var weighted = new HashSet<int>();
            foreach (var w in skin.sharedMesh.boneWeights)
            {
                if (w.weight0 > 0) weighted.Add(w.boneIndex0);
                if (w.weight1 > 0) weighted.Add(w.boneIndex1);
                if (w.weight2 > 0) weighted.Add(w.boneIndex2);
                if (w.weight3 > 0) weighted.Add(w.boneIndex3);
            }
            var joints = skin.bones;
            for (int j = 0; j < PairedGripPose.FingerCount; j++)
            {
                var bone = animator.GetBoneTransform(PairedGripPose.Finger(j));
                Check(bone && weighted.Contains(Array.IndexOf(joints, bone)), "Unweighted or unmapped " + PairedGripPose.Finger(j));
            }
            Check(skin.sharedMaterials.All(m => m && m.shader.isSupported && m.mainTexture), "Missing/unsupported model surface.");
            Debug.Log("[P2F] " + animator.avatar.name + " weighted right fingers 15/15, textures " +
                string.Join(", ", skin.sharedMaterials.Select(m => m.mainTexture.name + " " + m.mainTexture.width + "x" + m.mainTexture.height)));
        }

        private static IEnumerator Sweep(OpponentAvatar model, Animator animator)
        {
            app.hud.canvas.enabled = false;
            app.enabled = false;
            var pointer = app.GetComponent<VrPointer>();
            var ray = app.GetComponent<LineRenderer>();
            bool rayWasVisible = ray.enabled;
            pointer.enabled = false;
            ray.enabled = false;
            float previousSpeed = animator.speed;
            var rig = app.armRig;
            string id = Models[app.SelectedAppearance];
            Directory.CreateDirectory(Path.Combine(output, id));
            var pad = app.deviceRoot.Find("Opponent Elbow Pad");
            var wrist = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var elbow = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var seat = app.deviceRoot.Find("Opponent Seat");
            var hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            // Advance the actual rig by 1/30 s, then let Unity submit the skinned meshes for that Play frame.
            using (var data = new StreamWriter(Path.Combine(output, id, "sweep.csv")))
            {
                data.WriteLine("frame,angle,ik_calls,wrist_target_error_mm,elbow_dx_mm,elbow_dy_mm,elbow_dz_mm,hips_above_seat_mm");
                for (int f = 0; f <= 180; f++)
                {
                    float angle = -30 * Mathf.Cos(2 * Mathf.PI * f / 180);
                    rig.ApplyAngle(angle);
                    int count = model.IkPassCount;
                    animator.speed = 1;
                    animator.Update(1f / 30);
                    animator.speed = 0;
                    model.ApplyPairedHandBones();
                    int calls = model.IkPassCount - count;
                    Check(calls > 0, "Animator IK pass missing at frame " + f);
                    var offset = 1000 * (app.deviceRoot.InverseTransformPoint(elbow.position) - pad.localPosition - Vector3.up * pad.localScale.y / 2);
                    float wristError = 1000 * Vector3.Distance(wrist.position, model.DesiredRightWrist);
                    float seatHeight = 1000 * (app.deviceRoot.InverseTransformPoint(hips.position).y - seat.localPosition.y - seat.localScale.y / 2);
                    data.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1:F3},{2},{3:F3},{4:F3},{5:F3},{6:F3},{7:F3}",
                        f, angle, calls, wristError, offset.x, offset.y, offset.z, seatHeight));
                    int sampleFrame = Time.frameCount;
                    while (Time.frameCount == sampleFrame) yield return null;
                    if (authored) continue;
                    AimAtGrip();
                    if (f == 0 || f == 45 || f == 90)
                    {
                        Capture(front, $"{id}/front-{f}.png");
                        Capture(side, $"{id}/side-{f}.png");
                        Capture(detail, $"{id}/hand-{f}.png");
                    }
                    if (app.SelectedAppearance == 1 && f % 3 == 0 && f < 180)
                    {
                        Capture(detail, $"{id}/hand-{f:D3}.jpg");
                        Capture(side, $"{id}/side-{f:D3}.jpg");
                    }
                }
            }
            animator.speed = previousSpeed;
            pointer.enabled = true;
            ray.enabled = rayWasVisible;
            app.enabled = true;
            rig.ApplyAngle(app.Pipeline.VisualAngleDeg);
            app.hud.canvas.enabled = true;
        }

        private static void CaptureLobbyPanel()
        {
            var panel = app.hud.canvas.transform;
            var camera = AnimationComparison.View(app.deviceRoot, Vector3.back, Vector3.zero);
            camera.transform.SetPositionAndRotation(panel.position - panel.forward * 1.18f, panel.rotation);
            camera.fieldOfView = 38f;
            var buffer = new RenderTexture(1600, 1000, 24);
            var image = new Texture2D(1600, 1000, TextureFormat.RGB24, false);
            try { AnimationComparison.Capture(camera, Path.Combine(output, "lobby-panel.png"), buffer, image); }
            finally
            {
                buffer.Release();
                Object.DestroyImmediate(image);
                Object.DestroyImmediate(buffer);
                Object.DestroyImmediate(camera.gameObject);
            }
        }

        private static void AimAtGrip()
        {
            detail.transform.position = Camera.main.transform.position;
            detail.transform.LookAt(app.armRig.GripWorldPosition, Camera.main.transform.up);
        }

        private static void Capture(Camera camera, string name)
            => AnimationComparison.Capture(camera, Path.Combine(output, name), target, pixels);

        private static void Check(bool condition, string reason)
        {
            if (!condition) throw new InvalidOperationException(reason);
        }
    }
}
