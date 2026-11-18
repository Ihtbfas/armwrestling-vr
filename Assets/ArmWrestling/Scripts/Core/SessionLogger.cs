using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 会话数据记录。按固定周期把角度、状态与事件写入 CSV，
    /// 供后续“角度映射误差 / 画面抖动 / 事件时序”分析使用（开题报告 4.1.4 (7)）。
    /// 文件位置：Application.persistentDataPath/sessions/
    /// </summary>
    public class SessionLogger : IDisposable
    {
        private readonly StreamWriter writer;
        private readonly float interval;
        private float timer;
        private int rowCount;
        private bool disposed;

        public string FilePath { get; private set; }

        public bool IsOpen
        {
            get { return writer != null && !disposed; }
        }

        public SessionLogger(float sampleIntervalSeconds)
        {
            interval = Mathf.Max(0.01f, sampleIntervalSeconds);

            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "sessions");
                Directory.CreateDirectory(directory);
                string fileName = "armwrestling_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
                FilePath = Path.Combine(directory, fileName);

                writer = new StreamWriter(FilePath, false, new UTF8Encoding(true));
                writer.WriteLine("time_s,state,mech_deg,visual_deg,omega_deg_s,torque_nm,device_state,role,note");
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArmWrestling] 日志文件创建失败: " + exception.Message);
                writer = null;
            }
        }

        public void Log(string state, float mechanicalAngle, float visualAngle, float angularVelocity, float torque, string deviceState, string role, string note, bool force)
        {
            if (writer == null || disposed)
            {
                return;
            }

            timer += Time.unscaledDeltaTime;
            if (!force && timer < interval)
            {
                return;
            }

            timer = 0f;

            try
            {
                writer.Write(Time.unscaledTimeAsDouble.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(state);
                writer.Write(',');
                writer.Write(mechanicalAngle.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(visualAngle.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(angularVelocity.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(torque.ToString("F3", CultureInfo.InvariantCulture));
                writer.Write(',');
                writer.Write(deviceState);
                writer.Write(',');
                writer.Write(role);
                writer.Write(',');
                writer.Write(note ?? string.Empty);
                writer.WriteLine();
                rowCount++;

                if (rowCount % 64 == 0)
                {
                    writer.Flush();
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArmWrestling] 写日志失败: " + exception.Message);
            }
        }

        public void LogEvent(string state, string note)
        {
            Log(state, 0f, 0f, 0f, 0f, string.Empty, string.Empty, note, true);
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;

            if (writer == null)
            {
                return;
            }

            try
            {
                writer.Flush();
                writer.Dispose();
                Debug.Log("[ArmWrestling] 会话日志已保存: " + FilePath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[ArmWrestling] 关闭日志失败: " + exception.Message);
            }
        }
    }
}
