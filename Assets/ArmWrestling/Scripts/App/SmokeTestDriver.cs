using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 自动冒烟测试驱动。
    ///
    /// 只在启动参数包含 <c>-autotest</c> 时工作，其余情况下自动禁用，对正式运行没有任何影响。
    /// 它会按固定节奏把流程推进一遍（设备检查 → 标定 → 角色选择 → 准备 → 倒计时 → 对战 →
    /// 暂停 → 继续 → 结算），并逐条打印日志，最后退出。用于打包后确认
    /// “场景能加载、引用没丢、状态机不会卡死”，比只看界面可靠。
    ///
    /// 用法：
    /// <c>ArmWrestlingVR.exe -batchmode -nographics -autotest -logFile out.log</c>
    /// </summary>
    public class SmokeTestDriver : MonoBehaviour
    {
        public ArmWrestlingApp app;
        public float stepSeconds = 2.5f;
        public float totalSeconds = 30f;

        private static readonly AppState[] Sequence =
        {
            AppState.DeviceCheck,
            AppState.Calibration,
            AppState.Lobby,
            AppState.Ready,
            AppState.Countdown,
            AppState.Fighting,
            AppState.Paused,
            AppState.Fighting,
            AppState.Finished,
            AppState.Lobby
        };

        private float elapsed;
        private int stepIndex = -1;
        private bool running;

        private void Start()
        {
            running = HasArgument("-autotest");
            if (!running)
            {
                enabled = false;
                return;
            }

            // 录制演示视频时把节奏放慢，好看清每个阶段：-teststep 4.5
            string stepArg = GetArgumentValue("-teststep");
            float parsed;
            if (!string.IsNullOrEmpty(stepArg) && float.TryParse(stepArg, out parsed) && parsed > 0.2f)
            {
                stepSeconds = parsed;
            }

            Debug.Log("[SmokeTest] 启动自动冒烟测试，共 " + Sequence.Length + " 步，每步 " + stepSeconds + " s。");
        }

        private static string GetArgumentValue(string key)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private void Update()
        {
            if (!running || app == null)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            int desired = Mathf.FloorToInt(elapsed / Mathf.Max(0.2f, stepSeconds));
            if (desired != stepIndex && desired < Sequence.Length)
            {
                stepIndex = desired;
                AppState target = Sequence[stepIndex];
                Debug.Log("[SmokeTest] 步骤 " + (stepIndex + 1) + "/" + Sequence.Length + " → " + target);
                app.ForceState(target);
            }

            if (elapsed > Mathf.Min(totalSeconds, Sequence.Length * stepSeconds + 2f))
            {
                Debug.Log("[SmokeTest] 完成。最终状态=" + app.state
                          + "，最近角度=" + app.Pipeline.MechanicalAngleDeg.ToString("F1")
                          + "，链路=" + (app.Connection != null ? app.Connection.Status.ToString() : "null"));
                running = false;
                Application.Quit(0);
            }
        }

        private static bool HasArgument(string argument)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], argument, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
