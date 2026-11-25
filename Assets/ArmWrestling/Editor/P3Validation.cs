using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace ArmWrestlingVR.EditorTools
{
    // Run the production scene against PC common.bridge and sim.mock_stm32.
    // External PC/simulator processes are owned by the run's caller.
    [InitializeOnLoad]
    public static class P3Validation
    {
        private const string ActiveKey = "ArmWrestling.P3Validation";
        private const string ScenePath = "Assets/Scenes/ArmWrestlingMain.unity";
        // First two rounds validate both directions. The final three replay the same down input.
        private static readonly string[] Characters = { "demo", "demo", "person_01", "person_02", "person_03" };

        private enum Step
        {
            Connect, Calibration, Select, Prepare, Ready, Countdown, Fight,
            Paused, Resumed, EditorFrozen, EditorResumed,
            SimulatorLost, SimulatorRecovered, ResumeForPcFault, PcLost, PcRecovered, ResumeForFinish,
            Finish, RestartWaiting
        }

        [Serializable]
        private sealed class PcStatus
        {
            public string type, mode, reason, match_state, character_id, active_character_id;
            public long seq;
            public float angle_rad, target_torque_nm, fatigue_level, elapsed_s;
            public bool telemetry_fresh, vr_fresh, character_fallback;
        }

        [Serializable]
        private sealed class Observation
        {
            public string check, sceneState, character, outcome, statusReason;
            public long vrStatusSequence;
            public float visualAngleDeg, directionCorrectedAngleDeg;
            public bool vrAngleValid;
            public PcStatus pc;
        }

        [Serializable]
        private sealed class Result
        {
            public string editor, graphics, scene, status = "running", error;
            public bool externalFaults, decoderAndSequenceChecks;
            public int runtimeErrors;
            public List<Observation> checks = new List<Observation>();
        }

        private static string directory;
        private static Step step;
        private static int round, countdownSamples, errors;
        private static long lastCountdownSequence, sequenceBeforeRestart;
        private static double started, since, nextPoll;
        private static bool pauseRequested;
        private static Result result;

        static P3Validation()
        {
            EditorApplication.update += Update;
            Application.logMessageReceived += ObserveError;
        }

        public static void Run()
        {
            try
            {
                string[] args = Environment.GetCommandLineArgs();
                int index = Array.IndexOf(args, "-p3dir");
                Require(index >= 0 && index + 1 < args.Length, "Provide -p3dir <output directory>.");
                directory = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(directory);
                File.Delete(Path.Combine(directory, "fault-request.txt"));
                File.Delete(Path.Combine(directory, "result.json"));
                SetInput("reset");
                CheckDecoder();

                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                var bootstrap = UnityEngine.Object.FindObjectOfType<RuntimeBootstrap>();
                Require(bootstrap != null && bootstrap.useHumanoidGrip && !bootstrap.useAuthoredGrip
                    && !bootstrap.useOriginalArt && bootstrap.playerAvatar != null && bootstrap.opponentAvatar != null,
                    "Expected the public P2-D profiles in the production scene.");
                SessionState.SetString(ActiveKey + ".directory", directory);
                SessionState.SetBool(ActiveKey + ".faults", Array.IndexOf(args, "-p3faults") >= 0);
                SessionState.SetBool(ActiveKey, true);
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        private static void Update()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying) return;
            try
            {
                double now = EditorApplication.timeSinceStartup;
                if (result == null)
                {
                    started = since = now;
                    directory = SessionState.GetString(ActiveKey + ".directory", "");
                    result = new Result
                    {
                        editor = Application.unityVersion,
                        graphics = SystemInfo.graphicsDeviceType.ToString(),
                        scene = ScenePath,
                        externalFaults = SessionState.GetBool(ActiveKey + ".faults", false)
                    };
                }
                if (now < nextPoll) return;
                nextPoll = now + 0.06;
                Require(now - started < 100, "Play validation timed out at " + step + " / round " + round);
                Require(now - since < (step == Step.Connect ? 25 : 12), "No progress at " + step + " / round " + round);
                Require(errors == 0, "Play logged " + errors + " runtime errors.");

                var app = UnityEngine.Object.FindObjectOfType<ArmWrestlingApp>();
                if (app == null || app.Pipeline == null || now - since < 0.12) return;
                var client = app.Connection as VrUdpClient;
                Require(client != null && app.dataSource == DataSourceMode.Network, "Expected production Network mode.");
                PcStatus pc = ReadLatestStatus();
                RunStep(app, client, pc, now);
            }
            catch (Exception error)
            {
                Fail(error);
            }
        }

        private static void RunStep(ArmWrestlingApp app, VrUdpClient client, PcStatus pc, double now)
        {
            string character = Characters[round];
            switch (step)
            {
                case Step.Connect:
                    if (!client.IsDataAvailable || !PcMatches(pc, "waiting", character)) return;
                    Require(app.state == AppState.DeviceCheck && Mathf.Abs(app.Pipeline.MechanicalAngleDeg) < 0.01f,
                        "Expected a valid software zero angle in DeviceCheck.");
                    Require(float.IsNaN(client.LastTorqueNm), "Network torque should be unavailable.");
                    var opponent = UnityEngine.Object.FindObjectOfType<OpponentAvatar>();
                    var animator = opponent != null ? opponent.GetComponent<Animator>() : null;
                    Require(animator != null && animator.isHuman && animator.avatar.isValid && opponent.UsesPairedGrip,
                        "Production Humanoid opponent/IK is missing.");
                    ExpectStop(pc);
                    CheckReplay(app.config);
                    result.decoderAndSequenceChecks = true;
                    Record("production-scene bilateral UDP with valid zero", app, client, pc);
                    app.hud.primaryButton.Press();
                    Move(Step.Calibration, now);
                    break;

                case Step.Calibration:
                    if (app.state != AppState.Calibration) return;
                    app.hud.tertiaryButton.Press();
                    Move(Step.Select, now);
                    break;

                case Step.Select:
                    if (app.state != AppState.Lobby) return;
                    if (round >= 2) app.hud.roleButtons[round - 2].Press();
                    Move(Step.Prepare, now);
                    break;

                case Step.Prepare:
                    if (client.CharacterId != character || !PcMatches(pc, "waiting", character)) return;
                    Require(!pc.character_fallback && pc.fatigue_level == 0f, "Wrong character or waiting did not reset fatigue.");
                    ExpectStop(pc);
                    Record("waiting confirmed " + character, app, client, pc);
                    app.hud.primaryButton.Press();
                    Move(Step.Ready, now);
                    break;

                case Step.Ready:
                    if (app.state != AppState.Ready || !PcMatches(pc, "waiting", character)) return;
                    ExpectStop(pc);
                    Record("ready STOP", app, client, pc);
                    app.hud.primaryButton.Press();
                    countdownSamples = 0;
                    lastCountdownSequence = -1;
                    Move(Step.Countdown, now);
                    break;

                case Step.Countdown:
                    if (app.state == AppState.Countdown && pc != null && pc.seq != lastCountdownSequence)
                    {
                        Require(PcMatches(pc, "waiting", character) && pc.fatigue_level == 0f,
                            "Countdown must publish waiting with zero fatigue.");
                        ExpectStop(pc);
                        lastCountdownSequence = pc.seq;
                        countdownSamples++;
                        if (countdownSamples == 2) Record("countdown STOP and zero fatigue", app, client, pc);
                    }
                    if (app.state != AppState.Fighting) return;
                    Require(countdownSamples >= 2, "Expected multiple PC samples during countdown.");
                    Move(Step.Fight, now);
                    break;

                case Step.Fight:
                    if (!Running(app, client, pc)) return;
                    Record("fighting enabled", app, client, pc);
                    if (round == 0)
                    {
                        app.hud.secondaryButton.Press();
                        Move(Step.Paused, now);
                    }
                    else
                    {
                        SetInput(round == 1 ? "back" : "down");
                        Move(Step.Finish, now);
                    }
                    break;

                case Step.Paused:
                    if (app.state != AppState.Paused || !PcMatches(pc, "paused", character)) return;
                    ExpectStop(pc);
                    Record("pause STOP", app, client, pc);
                    Capture("p3-paused.png");
                    app.hud.primaryButton.Press();
                    Move(Step.Resumed, now);
                    break;

                case Step.Resumed:
                    if (!Running(app, client, pc)) return;
                    Record("continue enabled", app, client, pc);
                    if (result.externalFaults)
                    {
                        pauseRequested = true;
                        EditorApplication.isPaused = true;
                        Move(Step.EditorFrozen, now);
                    }
                    else
                    {
                        SetInput("down");
                        Move(Step.Finish, now);
                    }
                    break;

                case Step.EditorFrozen:
                    if (pc == null || pc.reason != "vr_timeout") return;
                    ExpectStop(pc);
                    Record("Unity main-loop pause caused PC heartbeat timeout", app, client, pc);
                    EditorApplication.isPaused = false;
                    pauseRequested = false;
                    Move(Step.EditorResumed, now);
                    break;

                case Step.EditorResumed:
                    if (!RecoveredPaused(app, client, pc)) return;
                    Record("Unity main-loop recovery kept round paused", app, client, pc);
                    app.hud.primaryButton.Press();
                    Move(Step.SimulatorLost, now);
                    break;

                case Step.SimulatorLost:
                    if (!pauseRequested)
                    {
                        if (!Running(app, client, pc)) return;
                        Request("stop-sim");
                        pauseRequested = true;
                    }
                    if (app.state != AppState.Paused || client.IsDataAvailable || pc == null || pc.telemetry_fresh
                        || app.hud.primaryButton.Interactable) return;
                    Require(client.Reason == "telemetry_missing" || client.Reason == "telemetry_timeout",
                        "Expected a fresh status with null angle on simulator loss.");
                    Require(app.outcome == MatchOutcome.None, "Simulator loss awarded a match result.");
                    ExpectStop(pc);
                    Record("simulator lost, null angle, paused without result", app, client, pc);
                    Request("start-sim");
                    pauseRequested = false;
                    Move(Step.SimulatorRecovered, now);
                    break;

                case Step.SimulatorRecovered:
                    if (!RecoveredPaused(app, client, pc)) return;
                    Record("recovered valid zero after simulator restart", app, client, pc);
                    app.hud.primaryButton.Press();
                    Move(Step.ResumeForPcFault, now);
                    break;

                case Step.ResumeForPcFault:
                    if (!Running(app, client, pc)) return;
                    sequenceBeforeRestart = client.LastSequence;
                    Request("stop-pc");
                    Move(Step.PcLost, now);
                    break;

                case Step.PcLost:
                    if (app.state != AppState.Paused || client.IsDataAvailable) return;
                    Require(app.outcome == MatchOutcome.None, "PC loss awarded a match result.");
                    Record("status timeout paused round without result", app, client, pc);
                    Request("start-pc");
                    Move(Step.PcRecovered, now);
                    break;

                case Step.PcRecovered:
                    if (!RecoveredPaused(app, client, pc)) return;
                    Require(client.LastSequence < sequenceBeforeRestart, "Restart did not accept the new PC sequence.");
                    Record("PC restart accepted lower status sequence", app, client, pc);
                    app.hud.primaryButton.Press();
                    Move(Step.ResumeForFinish, now);
                    break;

                case Step.ResumeForFinish:
                    if (!Running(app, client, pc)) return;
                    SetInput("down");
                    Move(Step.Finish, now);
                    break;

                case Step.Finish:
                    if (app.state != AppState.Finished || !PcMatches(pc, "finished", character)) return;
                    MatchOutcome expected = round == 1 ? MatchOutcome.OpponentWins : MatchOutcome.PlayerWins;
                    Require(app.outcome == expected, "Unexpected outcome for the applied torque direction: " + app.outcome);
                    Require(round == 1 ? pc.angle_rad > 0.43f && app.Pipeline.VisualAngleDeg < -29f
                        : pc.angle_rad < -0.43f && app.Pipeline.VisualAngleDeg > 29f, "Angle or visual direction is wrong.");
                    ExpectStop(pc);
                    Record("finished STOP after " + expected, app, client, pc);
                    if (round <= 1) Capture(round == 0 ? "p3-win.png" : "p3-loss.png");
                    SetInput("reset");
                    if (round == Characters.Length - 1)
                    {
                        app.hud.primaryButton.Press();
                        Move(Step.RestartWaiting, now);
                    }
                    else
                    {
                        app.hud.secondaryButton.Press();
                        round++;
                        Move(Step.Select, now);
                    }
                    break;

                case Step.RestartWaiting:
                    if (app.state != AppState.Ready || !PcMatches(pc, "waiting", character)) return;
                    ExpectStop(pc);
                    Require(pc.fatigue_level == 0f, "New round did not clear fatigue.");
                    Record("play again reached Ready, STOP, zero fatigue", app, client, pc);
                    Complete();
                    break;
            }
        }

        private static bool PcMatches(PcStatus pc, string state, string character)
        {
            return pc != null && pc.vr_fresh && pc.match_state == state
                && pc.character_id == character && pc.active_character_id == character;
        }

        private static bool Running(ArmWrestlingApp app, VrUdpClient client, PcStatus pc)
        {
            return app.state == AppState.Fighting && client.IsRunning
                && PcMatches(pc, "running", Characters[round]) && pc.mode == "enabled";
        }

        private static bool RecoveredPaused(ArmWrestlingApp app, VrUdpClient client, PcStatus pc)
        {
            return app.state == AppState.Paused && client.IsDataAvailable && client.Reason == "match_paused"
                && PcMatches(pc, "paused", Characters[round]) && pc.telemetry_fresh && app.hud.primaryButton.Interactable;
        }

        private static void ExpectStop(PcStatus pc)
        {
            Require(pc != null && pc.mode == "stop" && pc.target_torque_nm == 0f, "Expected PC STOP with zero target.");
        }

        private static PcStatus ReadLatestStatus()
        {
            string path = Path.Combine(directory, "pc.jsonl");
            if (!File.Exists(path)) return null;
            using (var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                string[] lines = reader.ReadToEnd().Split('\n');
                // Skip the last token, which can be a partially written line.
                for (int i = lines.Length - 2; i >= 0; i--)
                {
                    if (!lines[i].Contains("\"type\": \"status\"")) continue;
                    return JsonUtility.FromJson<PcStatus>(lines[i]);
                }
                return null;
            }
        }

        private static void Record(string check, ArmWrestlingApp app, VrUdpClient client, PcStatus pc)
        {
            result.checks.Add(new Observation
            {
                check = check, sceneState = app.state.ToString(), character = client.CharacterId,
                outcome = app.outcome.ToString(), statusReason = client.Reason,
                vrStatusSequence = client.LastSequence, vrAngleValid = client.IsDataAvailable,
                visualAngleDeg = app.Pipeline.VisualAngleDeg,
                directionCorrectedAngleDeg = app.Pipeline.MechanicalAngleDeg, pc = pc
            });
            Debug.Log("[P3] " + check);
        }

        private static void CheckDecoder()
        {
            const string prefix = "{\"v\":1,\"type\":\"status\",\"seq\":";
            const string tail = ",\"mode\":\"stop\",\"reason\":\"match_waiting\"}";
            bool Parse(string json, out VrUdpClient.StatusMessage message) =>
                VrUdpClient.TryDecode(Encoding.UTF8.GetBytes(json), out message);

            Require(Parse(prefix + "65536,\"angle_rad\":0" + tail, out var zero)
                && zero.seq == 65536 && zero.angle_rad == 0, "Sequence beyond ushort or legal zero was rejected.");
            Require(Parse(prefix + "9007199254740991,\"angle_rad\":null" + tail, out var missingAngle)
                && !missingAngle.angle_rad.HasValue, "Legal null or maximum interoperable sequence was rejected.");
            foreach (string invalid in new[]
            {
                prefix + "9007199254740992,\"angle_rad\":0" + tail,
                prefix + "1.5,\"angle_rad\":0" + tail,
                prefix + "0" + tail,
                prefix + "0,\"angle_rad\":\"null\"" + tail,
                prefix + "0,\"angle_rad\":1e999" + tail,
                prefix + "0,\"angle_rad\":0" + tail + "{}"
            }) Require(!Parse(invalid, out _), "Invalid status accepted: " + invalid);
            Debug.Log("[P3] Decoder accepted null, numeric zero and wide seq; rejected malformed status.");
        }

        // Exercise the real socket client in isolation before driving the live match.
        // This endpoint uses an ephemeral port and never touches the production PC session.
        private static void CheckReplay(ArmWrestlingConfig config)
        {
            using (var peer = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                peer.Client.ReceiveTimeout = 250;
                var client = new VrUdpClient(config, "127.0.0.1", ((IPEndPoint)peer.Client.LocalEndPoint).Port);
                try
                {
                    client.Tick(0f, false);
                    IPEndPoint local = null;
                    peer.Receive(ref local);
                    byte[] Packet(long sequence, string angle) => Encoding.UTF8.GetBytes(
                        "{\"v\":1,\"type\":\"status\",\"seq\":" + sequence + ",\"angle_rad\":" + angle
                        + ",\"mode\":\"stop\",\"reason\":\"match_waiting\"}");

                    void Send(long sequence, string angle)
                    {
                        byte[] bytes = Packet(sequence, angle);
                        peer.Send(bytes, bytes.Length, local);
                        client.Tick(0f, false);
                    }

                    Send(65536, "0");
                    Require(client.IsDataAvailable && client.LastSequence == 65536
                        && client.TryConsumeSample(out var sample) && sample.angleDeg == 0f,
                        "Wide sequence and valid zero did not reach the sample pipeline.");
                    float acceptedAge = client.SecondsSinceLastData;
                    System.Threading.Thread.Sleep(20);
                    Send(65536, "-0.4");
                    Send(65535, "-0.4");
                    Require(client.LastSequence == 65536 && client.SecondsSinceLastData > acceptedAge
                        && !client.TryConsumeSample(out _), "Duplicate or older packets revived or replaced a sample.");
                    Send(65537, "null");
                    Require(!client.IsDataAvailable && client.LastSequence == 65537,
                        "A status with null angle did not invalidate the last measurement.");
                }
                finally
                {
                    client.Shutdown();
                }
            }
            Debug.Log("[P3] Wide sequence, duplicate/old packet and null behavior verified on an isolated loopback socket.");
        }

        private static void Capture(string name)
        {
            Require(SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null, "A graphics device is needed for P3 evidence.");
            var camera = Camera.main;
            Require(camera != null, "Production camera is missing.");
            var labels = UnityEngine.Object.FindObjectOfType<HudPresenter>().GetComponentsInChildren<Text>(true);
            var previousFonts = new Font[labels.Length];
            for (int i = 0; i < labels.Length; i++) previousFonts[i] = labels[i].font;
            Font publicFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var active = RenderTexture.active;
            var previous = camera.targetTexture;
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                // Local projects can override FontProvider with a privately licensed font.
                for (int i = 0; i < labels.Length; i++) labels[i].font = publicFont;
                Canvas.ForceUpdateCanvases();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                File.WriteAllBytes(Path.Combine(directory, name), pixels.EncodeToPNG());
            }
            finally
            {
                for (int i = 0; i < labels.Length; i++) labels[i].font = previousFonts[i];
                camera.targetTexture = previous;
                RenderTexture.active = active;
                UnityEngine.Object.DestroyImmediate(pixels);
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

        private static void SetInput(string value) => File.WriteAllText(Path.Combine(directory, "input.txt"), value);
        private static void Request(string value) => File.WriteAllText(Path.Combine(directory, "fault-request.txt"), value);

        private static void Move(Step next, double now)
        {
            step = next;
            since = now;
        }

        private static void ObserveError(string message, string stack, LogType type)
        {
            if (SessionState.GetBool(ActiveKey, false) && EditorApplication.isPlaying
                && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)) errors++;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Complete()
        {
            result.status = "pass";
            result.runtimeErrors = errors;
            File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(result, true));
            SessionState.SetBool(ActiveKey, false);
            Debug.Log("[P3] PASS: production scene, actual PC and simulated STM32.");
            EditorApplication.Exit(0);
        }

        private static void Fail(Exception error)
        {
            SessionState.SetBool(ActiveKey, false);
            if (EditorApplication.isPaused) EditorApplication.isPaused = false;
            if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            {
                result = result ?? new Result();
                result.status = "fail";
                result.error = error.ToString();
                result.runtimeErrors = errors;
                File.WriteAllText(Path.Combine(directory, "result.json"), JsonUtility.ToJson(result, true));
            }
            Debug.LogError("[P3] FAIL: " + error);
            EditorApplication.Exit(1);
        }
    }
}
