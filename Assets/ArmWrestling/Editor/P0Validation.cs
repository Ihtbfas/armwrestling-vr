using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace ArmWrestlingVR.EditorTools
{
    // Integration check for the delivered scene. Use without -quit or -autotest.
    [InitializeOnLoad]
    public static class P0Validation
    {
        private const string ActiveKey = "ArmWrestling.P0Validation";
        private static double started;
        private static double lastStep;
        private static int step;
        private static int errors;
        private static float minAngle = float.PositiveInfinity;
        private static float maxAngle = float.NegativeInfinity;
        private static string states = "";
        private static bool sawContestClip;

        static P0Validation()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += OnLog;
        }

        public static void Run()
        {
            try
            {
                Require(ArmWrestlingConfig.Load().defaultDataSource == DataSourceMode.Simulated,
                    "A fresh checkout must start in offline simulation.");
                Require(FontProvider.UiFont.HasCharacter('\u4e2d'), "UI font must render Chinese on this machine.");
                foreach (string path in new[] { SceneGenerator.MainScenePath, SceneGenerator.PreviewScenePath })
                {
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int missing = 0;
                    foreach (var root in scene.GetRootGameObjects())
                        foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                            missing += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                    Require(missing == 0, path + ": missing scripts=" + missing);
                    Debug.Log("[P0] Scene references OK: " + path);
                }
                string[] args = Environment.GetCommandLineArgs();
                string scenePath = SceneGenerator.MainScenePath;
                for (int i = 0; i + 1 < args.Length; i++)
                    if (args[i] == "-p0scene") scenePath = args[i + 1];
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var bootstrap = UnityEngine.Object.FindObjectOfType<RuntimeBootstrap>();
                if (Array.IndexOf(args, "-p0contact") >= 0)
                    Require(bootstrap.usePairedGrip && bootstrap.pairedCollisions == PairedCollisionMode.ContactGrip,
                        "Main scene is not configured for integrated contact grip.");
                bootstrap.usePairedGrip = false;
                bootstrap.pairedCollisions = PairedCollisionMode.Off;
                bool grip = Array.IndexOf(args, "-p0grip") >= 0;
                bool handContact = Array.IndexOf(args, "-p0handcontact") >= 0;
                bool contactGrip = Array.IndexOf(args, "-p0contact") >= 0;
                bool legacyContact = Array.IndexOf(args, "-p0contactlegacy") >= 0;
                bool missingContact = Array.IndexOf(args, "-p0contactfallback") >= 0;
                if (missingContact) Require(Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource) == null,
                    "The contact fallback test requires the new pose to be absent.");
                bool collision = missingContact || legacyContact || contactGrip || Array.IndexOf(args, "-p0collision") >= 0;
                bool paired = collision || Array.IndexOf(args, "-p0pairedgrip") >= 0;
                if (paired)
                {
                    var pose = Resources.Load<PairedGripPose>(PairedGripPose.PoseResource);
                    Require(pose != null && pose.IsComplete, "The paired pose is missing or incomplete.");
                }
                bool motion = paired || handContact || Array.IndexOf(args, "-p0motion") >= 0;
                if (bootstrap != null && (Array.IndexOf(args, "-p0art") >= 0 || grip || motion))
                {
                    Require(Resources.Load<GameObject>(paired ? PairedGripPose.AvatarResource : "Characters/Ch03") != null,
                        "The requested avatar is missing.");
                    bootstrap.useOriginalArt = !paired; // Play copy only; never save the scene.
                    bootstrap.usePairedGrip = paired;
                    bootstrap.pairedCollisions = legacyContact ? PairedCollisionMode.LegacyContactGrip : (missingContact || contactGrip) ? PairedCollisionMode.ContactGrip : collision ? PairedCollisionMode.Correct : PairedCollisionMode.Off;
                    bootstrap.useExperimentalGrip = grip;
                    bootstrap.useCharacterMotion = motion;
                    bootstrap.useHandContact = handContact;
                }
                SessionState.SetBool(ActiveKey + ".motion", motion);
                SessionState.SetBool(ActiveKey + ".handContact", handContact);
                SessionState.SetBool(ActiveKey + ".paired", paired);
                SessionState.SetBool(ActiveKey + ".collision", collision);
                SessionState.SetBool(ActiveKey + ".contactGrip", contactGrip);
                SessionState.SetBool(ActiveKey + ".legacyContact", legacyContact || missingContact);
                SessionState.SetBool(ActiveKey, true);
                SessionState.SetString(ActiveKey + ".scene", scenePath);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                if (started == 0) { started = lastStep = EditorApplication.timeSinceStartup; }
                double now = EditorApplication.timeSinceStartup;
                Require(now - started < 45, "Play validation timed out at step " + step);
                Require(errors == 0, "Runtime logged " + errors + " errors/exceptions.");
                var app = UnityEngine.Object.FindObjectOfType<ArmWrestlingApp>();
                if (app == null || app.Pipeline == null || now - lastStep < 0.2) return;
                Require(app.hud != null && app.armRig != null && Camera.main != null, "Missing scene bindings.");
                if (app.Pipeline.HasSample)
                {
                    minAngle = Mathf.Min(minAngle, app.Pipeline.VisualAngleDeg);
                    maxAngle = Mathf.Max(maxAngle, app.Pipeline.VisualAngleDeg);
                }
                string current = app.state.ToString();
                if (!states.EndsWith(current + ";", StringComparison.Ordinal))
                { states += current + ";"; Debug.Log("[P0] State: " + current); }
                switch (step)
                {
                    case 0:
                        Require(app.dataSource == DataSourceMode.Simulated, "Expected offline simulation.");
                        Require(app.Connection.Status == LinkStatus.Connected, "Simulation not connected.");
                        Require(app.Pipeline.HasSample, "No angle sample.");
                        Require(app.state == AppState.DeviceCheck, "Unexpected initial state.");
                        app.hud.primaryButton.Press(); Advance(now); break;
                    case 1:
                        Require(app.state == AppState.Calibration, "Device check button failed.");
                        app.hud.tertiaryButton.Press(); Advance(now); break;
                    case 2:
                        Require(app.state == AppState.Lobby, "Skip calibration button failed.");
                        app.hud.roleButtons[1].Press();
                        Require(app.role == OpponentRole.Defensive, "Role selection failed.");
                        app.hud.primaryButton.Press(); Advance(now); break;
                    case 3:
                        // The state changes at the end of Countdown's Update; wait for
                        // Fighting's next Update to bind the pause action to this button.
                        if (app.state != AppState.Fighting || app.hud.secondaryButton.Caption != "暂停") return;
                        app.hud.secondaryButton.Press(); Advance(now); break;
                    case 4:
                        Require(app.state == AppState.Paused, "Pause button failed.");
                        app.hud.primaryButton.Press(); Advance(now); break;
                    case 5:
                        Require(app.state == AppState.Fighting, "Resume button failed.");
                        app.autoDemo = true; Advance(now); break;
                    case 6:
                        if (!sawContestClip && app.state == AppState.Fighting
                            && SessionState.GetBool(ActiveKey + ".motion", false))
                        {
                            var avatar = app.deviceRoot.GetComponentInChildren<OpponentAvatar>();
                            if (avatar != null)
                                sawContestClip = Array.Exists(avatar.GetComponent<Animator>().GetCurrentAnimatorClipInfo(0),
                                    info => info.clip.name == "SeatedContest" && info.weight > 0.1f);
                        }
                        if (app.state != AppState.Finished
                            || app.hud.bigText.text != DisplayNames.Of(app.outcome)) return;
                        Require(app.outcome != MatchOutcome.None, "No match result.");
                        Require(maxAngle - minAngle > 5, "Visual arm angle did not move.");
                        Require(!app.Pipeline.IsStale(Time.unscaledTimeAsDouble), "Angle stream is stale.");
                        SaveResult(app);
                        SessionState.SetBool(ActiveKey, false);
                        Debug.Log("[P0] PASS: Editor Play, UI flow, role, live angles, pause/resume, outcome.");
                        EditorApplication.Exit(0); break;
                }
            }
            catch (Exception exception) { Fail(exception.ToString()); }
        }

        private static void Advance(double now) { step++; lastStep = now; }

        private static void SaveResult(ArmWrestlingApp app)
        {
            var model = UnityEngine.Object.FindObjectOfType<OpponentAvatar>();
            string opponent = "Capsule fallback";
            bool paired = SessionState.GetBool(ActiveKey + ".paired", false);
            if (paired)
                Require(model != null && model.UsesPairedGrip && app.armRig.playerVisual != null
                    && app.armRig.playerVisual.UsesPairedGrip, "The Rocketbox pair did not start.");
            if (ArenaFactory.UseCharacterModel)
            {
                Require(model != null, "Local character asset exists but was not instantiated.");
                opponent = "Humanoid with IK targets";
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "-p0grip") >= 0)
                    Require(model.useGripPose, "The grip trial did not reach the spawned avatar.");
                var animator = model.GetComponent<Animator>();
                if (SessionState.GetBool(ActiveKey + ".motion", false))
                {
                    Require(app.armRig.playerVisual != null && app.armRig.playerVisual.Skin.enabled,
                        "P1-D player surface was not enabled.");
                    Require(model.UsesAuthoredMotion && (paired || model.UsedStableElbow), "Character motion fell back.");
                    Require(model.UsesPairedGrip == paired && app.armRig.playerVisual.UsesPairedGrip == paired,
                        "The requested paired grip mode did not start.");
                    if (paired)
                    {
                        Require(app.armRig.playerVisual.HasSleeve, "Rocketbox sleeve is missing.");
                        Require(app.armRig.playerVisual.Skin.sharedMaterial.mainTexture != null,
                            "Rocketbox skin texture is missing.");
                        bool expectCollision = SessionState.GetBool(ActiveKey + ".collision", false);
                        var experiment = model.CollisionExperiment;
                        Require(expectCollision == (experiment != null), "The requested collision mode did not start.");
                        if (expectCollision)
                        {
                            Require(experiment.Mode == (SessionState.GetBool(ActiveKey + ".legacyContact", false) ? PairedCollisionMode.LegacyContactGrip : SessionState.GetBool(ActiveKey + ".contactGrip", false) ? PairedCollisionMode.ContactGrip : PairedCollisionMode.Correct) && experiment.EvaluationCount > 0
                                && experiment.MaxFingerCorrection > 0f, "The collision correction did not execute.");
                            opponent += "\nCollision=" + experiment.Mode + ", proxies=" + experiment.ProxyCount;
                            if (experiment.Mode == PairedCollisionMode.ContactGrip || experiment.Mode == PairedCollisionMode.LegacyContactGrip)
                                Require(experiment.StoppedJoints > 0 && experiment.BlockedClosureExcess > 0f, "Contact did not limit finger closure.");
                            if (experiment.Mode == PairedCollisionMode.ContactGrip)
                            {
                                var contact = Resources.Load<PairedGripPose>(PairedGripPose.ContactPoseResource);
                                Require(contact != null && contact.HasContactEndpoints && experiment.ContactBuilds > 0
                                    && !experiment.previewClosedPose, "Authored contact endpoints were not used.");
                                Require(Vector3.Distance(model.DesiredRightWrist, contact.opponent.WorldWrist(app.armRig.gripAnchor).position) < 0.00001f,
                                    "The main scene did not use the saved contact wrist pose.");
                                opponent += "\nContact=Authored endpoints, cached simultaneous closure";
                            }
                        }
                        opponent += "\nPairedGrip=Rocketbox opponent and player";
                    }
                    Require(sawContestClip, "The contest clip did not play during the simulated match.");
                    bool expectedContact = SessionState.GetBool(ActiveKey + ".handContact", false);
                    Require(app.armRig.playerVisual.UsesHandContact == expectedContact,
                        "The requested hand contact mode did not start.");
                    if (expectedContact)
                    {
                        Require(app.armRig.playerVisual.HasSleeve,
                            "The player sleeve mesh did not start.");
                        opponent += "\nHandContact=Paired palm frame and sleeve";
                    }
                    opponent += "\nPlayer=Skinned arm, Contest clip observed in Fighting";
                }
                Require(animator != null && animator.isHuman && animator.avatar.isValid
                    && animator.runtimeAnimatorController != null && model.rightHandTarget != null,
                    "Local Humanoid / IK bindings are invalid.");
                foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Require(renderer.bounds.max.y > app.deviceRoot.position.y + 0.1f,
                        "Character remains below the tabletop after Animator update: " + renderer.bounds);
                    var baked = new Mesh();
                    renderer.BakeMesh(baked);
                    opponent += "\nModelDiagnostics=" + renderer.name + ", enabled=" + renderer.enabled
                        + ", active=" + renderer.gameObject.activeInHierarchy
                        + ", worldBounds=" + renderer.bounds + ", bakedBounds=" + baked.bounds
                        + ", modelScale=" + model.transform.lossyScale
                        + ", inFrustum=" + GeometryUtility.TestPlanesAABB(
                            GeometryUtility.CalculateFrustumPlanes(Camera.main), renderer.bounds);
                    UnityEngine.Object.DestroyImmediate(baked);
                }
            }
            string directory = Path.GetFullPath(".p0-local");
            Directory.CreateDirectory(directory);
            string name = Path.GetFileNameWithoutExtension(SessionState.GetString(ActiveKey + ".scene", "scene"));
            File.WriteAllText(Path.Combine(directory, name + "-play.txt"),
                "PASS\nEditor=" + Application.unityVersion + "\nScene=" + name + "\nStates=" + states
                + "\nVisualAngleRange=" + minAngle + ".." + maxAngle + "\nOutcome=" + app.outcome
                + "\nRuntimeErrors=" + errors + "\nGraphics=" + SystemInfo.graphicsDeviceType
                + "\nOpponent=" + opponent + "\nFont=" + FontProvider.UiFont.name + "\n");
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
            Camera camera = Camera.main;
            RenderTexture previous = camera.targetTexture;
            RenderTexture active = RenderTexture.active;
            var target = new RenderTexture(1280, 720, 24);
            var texture = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                texture.Apply();
                File.WriteAllBytes(Path.Combine(directory, name + "-play.png"), texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(texture);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void OnLog(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(ActiveKey, false)
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Fail(string message)
        {
            SessionState.SetBool(ActiveKey, false);
            Debug.LogError("[P0] FAIL: " + message);
            EditorApplication.Exit(1);
        }
    }
}
