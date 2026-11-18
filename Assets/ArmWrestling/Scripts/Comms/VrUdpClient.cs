using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 上位机 UDP V0.1 客户端（每个报文一个 UTF-8 JSON 对象）。
    ///
    /// 这些约定来自上位机仓库的 <c>docs/vr-udp-v01.md</c>，以那个文件为准：
    ///   VR → PC，每 200 ms 一条：
    ///     {"v":1,"type":"vr_update","seq":0,"character_id":"opponent_01","match_state":"running"}
    ///   PC → VR，约 50 Hz：
    ///     {"v":1,"type":"status","seq":20,"angle_rad":-0.4,"mode":"enabled","reason":"running"}
    ///
    /// 三个必须记住的细节：
    ///   1. 角度单位是**弧度**，零位为 0，玩家往获胜侧下压时为**负**；
    ///   2. <c>angle_rad</c> 为 null 表示没有有效角度，此时不能拿上一帧当正常数据用；
    ///   3. 收发用同一个 socket，PC 只认最近一个有效发送端，1 秒收不到就释放会话。
    /// </summary>
    public class VrUdpClient : IDeviceConnection
    {
        private const float HeartbeatIntervalSeconds = 0.2f;
        private const float StatusTimeoutSeconds = 1.0f;

        private readonly ArmWrestlingConfig config;
        private readonly string hostIp;
        private readonly int hostPort;

        private UdpClient socket;
        private IPEndPoint hostEndpoint;
        private IPEndPoint broadcastEndpoint;
        private IPEndPoint receiveEndpoint;
        private bool autoDiscover;
        private bool discovered;
        private IPAddress discoveredAddress;

        private ushort outSequence;
        private int lastInSequence;
        private int hostPortActual;
        private bool hasInSequence;

        private double lastStatusTime = -999.0;
        private bool haveEverReceived;
        private string lastReason = string.Empty;
        private string lastMode = string.Empty;
        private bool hasValidAngle;
        private float angleDeg;
        private float angularVelocityDeg;
        private bool hasNewSample;
        private float lastSampleTime;
        private AngleSample latestSample;

        private string matchState = "waiting";
        private string characterId = "opponent_01";

        private string lastError = string.Empty;
        private readonly object socketLock = new object();
        private System.Threading.Timer heartbeatTimer;
        private volatile bool running;

        public VrUdpClient(ArmWrestlingConfig config, string hostIp, int hostPort)
        {
            this.config = config;
            // 留空表示自动发现：先广播，谁回 status 就用谁的地址，换网络不用重新打包。
            this.autoDiscover = string.IsNullOrEmpty(hostIp);
            this.hostIp = this.autoDiscover ? "0.0.0.0" : hostIp;
            this.hostPort = hostPort;
        }

        /// <summary>由主流程每帧写入：waiting / running / paused / finished。</summary>
        public string MatchState
        {
            get { return matchState; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    matchState = value;
                }
            }
        }

        /// <summary>角色编号，1–64 字符，PC 目前只用于记录。</summary>
        public string CharacterId
        {
            get { return characterId; }
            set
            {
                if (!string.IsNullOrEmpty(value))
                {
                    characterId = value.Length > 64 ? value.Substring(0, 64) : value;
                }
            }
        }

        public string Name
        {
            get { return "上位机 UDP V0.1"; }
        }

        public LinkStatus Status
        {
            get
            {
                if (socket == null)
                {
                    return LinkStatus.Disconnected;
                }

                if (!haveEverReceived)
                {
                    return LinkStatus.Connecting;
                }

                if (SecondsSinceLastData > StatusTimeoutSeconds * 2f)
                {
                    return LinkStatus.Disconnected;
                }

                if (SecondsSinceLastData > StatusTimeoutSeconds)
                {
                    return LinkStatus.Stale;
                }

                return LinkStatus.Connected;
            }
        }

        public string Detail
        {
            get
            {
                if (socket == null)
                {
                    return lastError.Length > 0 ? lastError : "未打开 socket";
                }

                if (!haveEverReceived)
                {
                    string target = discovered && discoveredAddress != null
                        ? discoveredAddress.ToString() + ":" + hostPortActual
                        : (autoDiscover ? "（广播搜索中）" : hostIp + ":" + hostPortActual);
                    return "正在等上位机 " + target + " 的 status";
                }

                string angleText = hasValidAngle
                    ? angleDeg.ToString("F1") + "°"
                    : "无效";

                return "角度 " + angleText + " ｜ " + (string.IsNullOrEmpty(lastMode) ? "?" : lastMode)
                       + " ｜ " + (string.IsNullOrEmpty(lastReason) ? "?" : lastReason);
            }
        }

        public bool IsDataAvailable
        {
            get { return hasValidAngle && SecondsSinceLastData <= StatusTimeoutSeconds; }
        }

        public float SecondsSinceLastData
        {
            get { return (float)(Time.unscaledTimeAsDouble - lastStatusTime); }
        }

        public float LastTorqueNm
        {
            get { return 0f; }
        }

        public DeviceState LastDeviceState
        {
            get
            {
                if (lastMode == "stop")
                {
                    return lastReason == "device_fault" ? DeviceState.Fault : DeviceState.Idle;
                }

                return DeviceState.Running;
            }
        }

        public MatchPhase HostMatchPhase
        {
            get { return MatchPhase.Fighting; }
        }

        public void Tick(float deltaTime, bool matchActive)
        {
            EnsureSocket();
            if (socket == null)
            {
                return;
            }

            ReceivePending();

            // 断流后不要把旧角度继续当有效数据，直接判为无效。
            if (SecondsSinceLastData > StatusTimeoutSeconds)
            {
                hasValidAngle = false;
            }
        }

        public bool TryConsumeSample(out AngleSample sample)
        {
            sample = latestSample;
            if (!hasNewSample)
            {
                return false;
            }

            hasNewSample = false;
            return true;
        }

        /// <summary>
        /// V0.1 里没有一次性事件，比赛状态靠 <see cref="MatchState"/> 心跳携带。
        /// 这里保留接口只是为了让上层不用改。
        /// </summary>
        public void SendEvent(SessionEventCode code, byte parameter, float value)
        {
            if (config.verboseComms)
            {
                Debug.Log("[ArmWrestling][上位机] 事件 " + code + " 并入心跳状态 " + matchState);
            }
        }

        public void Reconnect()
        {
            Shutdown();
            haveEverReceived = false;
            hasInSequence = false;
            lastStatusTime = -999.0;
            EnsureSocket();
        }

        public void Shutdown()
        {
            if (socket == null)
            {
                return;
            }

            running = false;
            if (heartbeatTimer != null)
            {
                heartbeatTimer.Dispose();
                heartbeatTimer = null;
            }

            lock (socketLock)
            {
                try
                {
                    socket.Close();
                }
                catch (Exception)
                {
                    // 关不掉也无所谓，进程退出会回收。
                }

                socket = null;
            }
        }

        private void EnsureSocket()
        {
            if (socket != null)
            {
                return;
            }

            try
            {
                UdpClient created = new UdpClient(0);
                created.Client.Blocking = false;
                created.EnableBroadcast = true;
                socket = created;
                hostPortActual = hostPort;
                broadcastEndpoint = new IPEndPoint(IPAddress.Broadcast, hostPort);
                hostEndpoint = autoDiscover
                    ? broadcastEndpoint
                    : new IPEndPoint(IPAddress.Parse(hostIp), hostPort);
                receiveEndpoint = new IPEndPoint(IPAddress.Any, 0);
                lastError = string.Empty;

                // 心跳放在后台线程：头显不戴时 Unity 主循环会降频甚至暂停，
                // 光靠 Update 发心跳会让上位机误判"VR 掉线"。
                running = true;
                heartbeatTimer = new System.Threading.Timer(_ => HeartbeatTick(), null, 200, 200);
            }
            catch (Exception exception)
            {
                lastError = exception.Message;
                socket = null;
                Debug.LogError("[ArmWrestling][上位机] 建立 UDP socket 失败: " + exception.Message);
            }
        }

        /// <summary>后台线程每 200 ms 调一次，发一条 vr_update。</summary>
        private void HeartbeatTick()
        {
            if (!running)
            {
                return;
            }

            SendHeartbeat();
        }

        private void SendHeartbeat()
        {
            string json = "{\"v\":1,\"type\":\"vr_update\",\"seq\":" + outSequence
                          + ",\"character_id\":\"" + Escape(characterId)
                          + "\",\"match_state\":\"" + matchState + "\"}";

            outSequence++;

            byte[] payload = Encoding.UTF8.GetBytes(json);
            lock (socketLock)
            {
                if (socket == null)
                {
                    return;
                }

                try
                {
                    socket.Send(payload, payload.Length, hostEndpoint);

                    // 同时发一份广播：现场换网络后 IP 变了也能自动找到上位机，
                    // 不用为了改一个 IP 重新打包。收到谁的回包就锁定谁。
                    if (!discovered && broadcastEndpoint != null && hostEndpoint != broadcastEndpoint)
                    {
                        socket.Send(payload, payload.Length, broadcastEndpoint);
                    }
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.WouldBlock)
                    {
                        Debug.LogWarning("[ArmWrestling][上位机] 发送心跳失败: " + exception.SocketErrorCode);
                    }
                }
                catch (ObjectDisposedException)
                {
                    // 正在关闭，忽略。
                }
            }
        }

        private void ReceivePending()
        {
            lock (socketLock)
            {
                ReceivePendingUnlocked();
            }
        }

        private void ReceivePendingUnlocked()
        {
            // 每帧把积压的报文全部读完，避免延迟累积。
            for (int guard = 0; guard < 32; guard++)
            {
                if (socket.Available <= 0)
                {
                    return;
                }

                byte[] payload;
                try
                {
                    payload = socket.Receive(ref receiveEndpoint);
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode == SocketError.WouldBlock)
                    {
                        return;
                    }

                    lastError = exception.SocketErrorCode.ToString();
                    return;
                }

                HandleDatagram(payload);
            }
        }

        private void HandleDatagram(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 8192)
            {
                return;
            }

            string json = Encoding.UTF8.GetString(payload);

            StatusMessage message;
            try
            {
                message = JsonUtility.FromJson<StatusMessage>(json);
            }
            catch (Exception)
            {
                return;
            }

            if (message == null || message.v != 1 || message.type != "status")
            {
                return;
            }

            // UDP 允许重复与乱序：只接受严格递增的 seq，新会话允许从 0 重新开始。
            if (hasInSequence && message.seq <= lastInSequence)
            {
                return;
            }

            lastInSequence = message.seq;
            hasInSequence = true;

            if (!discovered && receiveEndpoint != null && receiveEndpoint.Address != null)
            {
                // 广播之后第一包从哪来，上位机就在哪。
                discovered = true;
                discoveredAddress = receiveEndpoint.Address;
                hostEndpoint = new IPEndPoint(discoveredAddress, hostPortActual);
                Debug.Log("[ArmWrestling][上位机] 自动发现控制端: " + discoveredAddress);
            }
            lastStatusTime = Time.unscaledTimeAsDouble;
            haveEverReceived = true;

            lastMode = message.mode ?? string.Empty;
            lastReason = message.reason ?? string.Empty;

            // JsonUtility 无法表达 null 数值，直接从原文判断。
            bool nullAngle = json.IndexOf("\"angle_rad\":null", StringComparison.Ordinal) >= 0
                             || json.IndexOf("\"angle_rad\": null", StringComparison.Ordinal) >= 0;

            hasValidAngle = !nullAngle;

            if (!hasValidAngle)
            {
                return;
            }

            // 上位机约定：玩家往获胜侧下压时角度为负；VR 内部约定"正 = 玩家占优"，
            // 所以这里先取反，再交给配置里的方向系数。
            float radians = message.angle_rad;
            angleDeg = -radians * Mathf.Rad2Deg;

            double now = Time.unscaledTimeAsDouble;
            float dt = (float)(now - lastSampleTime);
            if (dt > 0.0001f)
            {
                angularVelocityDeg = (angleDeg - latestSample.angleDeg) / dt;
            }

            lastSampleTime = (float)now;
            hasNewSample = true;

            latestSample = new AngleSample
            {
                localTime = now,
                angleDeg = angleDeg,
                angularVelocityDegPerSecond = angularVelocityDeg,
                torqueNm = 0f,
                // 上位机文档：mode=stop 时仍可能有有效角度，模型要继续跟着动，
                // 只是不该把它当成"正常比赛数据"来判胜负——这一点由 DeviceState 区分。
                deviceState = lastMode == "stop" ? DeviceState.Idle : DeviceState.Running,
                valid = true,
                faultCode = lastReason == "device_fault" ? (ushort)1 : (ushort)0,
                frameCounter = (ulong)message.seq
            };
        }

        private static string Escape(string text)
        {
            return text.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        [Serializable]
        private class StatusMessage
        {
            public int v;
            public string type;
            public int seq;
            public float angle_rad;
            public string mode;
            public string reason;
        }
    }
}





