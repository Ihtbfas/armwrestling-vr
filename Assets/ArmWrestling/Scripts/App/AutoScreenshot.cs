using System.IO;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 自动截图，用于“没有头显时确认场景渲染是否正常”。
    ///
    /// 只有启动参数带 <c>-shotdir &lt;目录&gt;</c> 时才挂载，正常运行时完全不参与。
    /// 例如：
    /// <c>ArmWrestlingVR.exe -shotdir C:\tmp\shots -autotest</c>
    /// </summary>
    public class AutoScreenshot : MonoBehaviour
    {
        private static readonly float[] CaptureTimes = { 3f, 5.5f, 8.5f, 13f, 17f, 21.5f };

        private string directory;
        private float elapsed;
        private int index;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfRequested()
        {
            string path = GetArgumentValue("-shotdir");
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameObject host = new GameObject("Auto Screenshot");
            AutoScreenshot capture = host.AddComponent<AutoScreenshot>();
            capture.directory = path;

            try
            {
                Directory.CreateDirectory(path);
            }
            catch (System.Exception exception)
            {
                Debug.LogWarning("[ArmWrestling] 截图目录创建失败: " + exception.Message);
            }
        }

        private void Update()
        {
            if (index >= CaptureTimes.Length)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;
            if (elapsed < CaptureTimes[index])
            {
                return;
            }

            string file = Path.Combine(directory, "shot_" + index.ToString("00") + ".png");
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("[ArmWrestling] 截图: " + file);
            index++;
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
    }
}
