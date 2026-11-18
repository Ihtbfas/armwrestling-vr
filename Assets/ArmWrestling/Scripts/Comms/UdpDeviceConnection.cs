using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 通过局域网 UDP 与 PC 控制端通信。
    ///
    /// 设计要点（对应开题报告 4.1.4 (5)）：
    /// 1. 连续角度数据走无连接 UDP，不做重传，只做“序号 + 时间戳 + 有效性”判断；
    /// 2. 开始、暂停、结束等重要事件带事件编号，控制端回 ACK，未确认则重发；
    /// 3. 心跳机制双向保活，超时后进入“数据超时/通信故障”状态并提示用户；
    /// 4. 支持自动发现：向广播地址发送 HELLO，收到 HELLO_ACK 后记录控制端地址。
    /// </summary>
    public class UdpDeviceConnection : IDeviceConnection
    {
        private readonly ArmWrestlingConfig config;
        private UdpClient client;
        private IPEndPoint receiveEndpoint;
        private IPEndPoint hostEndpoint;
        private bool hostAddressKnown;

        private readonly byte[] sendBuffer = new byte[WireProtocol.HeaderSize + WireProtocol.MaxPayload + WireProtocol.CrcSize];
        private readonly byte[] receiveBuffer = new byte[2048];

        private ushort sequence;
        private ushort nextEventId = 1;

        private LinkStatus status = LinkStatus.Disconnected;
        private string detail = "未启动";
        private float secondsSinceLastData = 999f;
        private float lastTelemetrySeconds = 999f;
        private float heartbeatTimer;
        private float discoveryTimer;
        private float connectTimer;

        private AngleSample latestSample;
        private bool hasNewSample;
        private ulong frameCounter;
        private float lastTorque;
        private DeviceState lastDeviceState = DeviceState.Init;
        private MatchPhase hostPhase = MatchPhase.None;
        private ushort lastTelemetrySequence;
        private bool hasTelemetrySequence;

        private bool socketReady;

        private readonly List<PendingEvent> pendingEvents = new List<PendingEvent>(8);

        private struct PendingEvent
        {
            public ushort eventId;
            public SessionEventCode code;
            public byte parameter;
            public float value;
            public int attempts;
            public float nextSendTime;
        }

        public UdpDeviceConnection(ArmWrestlingConfig config)
        {
            this.config = config;
            OpenSocket();
        }

        public string Name
        {
            get { return "UDP 局域网"; }
        }

        public LinkStatus Status
        {
            get { return status; }
        }

        public string Detail
        {
            get { return detail; }
        }

        public bool IsDataAvailable
        {
            get { return status == LinkStatus.Connected || status == LinkStatus.Stale; }
        }

        public float SecondsSinceLastData
        {
            get { return secondsSinceLastData; }
        }

        public float LastTorqueNm
        {
            get { return lastTorque; }
        }

        public DeviceState LastDeviceState
        {
            get { return lastDeviceState; }
        }

        public MatchPhase HostMatchPhase
        {
            get { return hostPhase; }
        }

        // ------------------------------------------------------------------
        // 套接字
        // ------------------------------------------------------------------

        private void OpenSocket()
        {
            CloseSocket();
            try
            {
                client = new UdpClient(config.localPort);
                client.EnableBroadcast = true;
                client.Client.ReceiveBufferSize = 64 * 1024;
                socketReady = true;
                status = LinkStatus.Connecting;
                detail = "绑定本地端口 " + ((IPEndPoint)client.Client.LocalEndPoint).Port;
            }
            catch (Exception exception)
            {
                socketReady = false;
                status = LinkStatus.Fault;
                detail = "UDP 端口打开失败：" + exception.Message;
                Debug.LogError("[ArmWrestling] UDP 初始化失败: " + exception);
            }
        }

        private void CloseSocket()
        {
            if (client != null)
            {
                try
                {
                    client.Close();
                }
                catch (Exception)
                {
                    // 关闭失败不影响后续重连。
                }

                client = null;
            }

            socketReady = false;
        }

        private IPEndPoint BroadcastEndpoint
        {
            get { return new IPEndPoint(IPAddress.Broadcast, config.hostPort); }
        }

        private void ResolveManualHost()
        {
            hostAddressKnown = false;
            hostEndpoint = null;

            if (!string.IsNullOrEmpty(config.manualHostIp))
            {
                IPAddress address;
                if (IPAddress.TryParse(config.manualHostIp.Trim(), out address))
                {
                    hostEndpoint = new IPEndPoint(address, config.hostPort);
                    hostAddressKnown = true;
                }
            }
        }

        // ------------------------------------------------------------------
        // 主循环
        // ------------------------------------------------------------------

        public void Tick(float deltaTime, bool matchActive)
        {
            if (!socketReady)
            {
                return;
            }

            secondsSinceLastData += deltaTime;
            lastTelemetrySeconds += deltaTime;

            if (!hostAddressKnown && !string.IsNullOrEmpty(config.manualHostIp))
            {
                ResolveManualHost();
            }

            ReceiveAll();
            UpdateStatus(deltaTime);
            SendHeartbeat(deltaTime);
            FlushEvents();
        }

        private void UpdateStatus(float deltaTime)
        {
            if (lastTelemetrySeconds > config.linkLostSeconds)
            {
                if (status != LinkStatus.Disconnected || hostAddressKnown)
                {
                    status = LinkStatus.Disconnected;
                }

                detail = hostAddressKnown
                    ? "等待控制端数据 " + lastTelemetrySeconds.ToString("F1") + " s"
                    : "正在搜索控制端（UDP " + config.hostPort + "）";
            }
            else if (lastTelemetrySeconds > config.dataTimeoutSeconds)
            {
                status = LinkStatus.Stale;
                detail = "数据超时 " + lastTelemetrySeconds.ToString("F2") + " s";
            }
            else
            {
                status = LinkStatus.Connected;
                if (hostEndpoint != null)
                {
                    detail = hostEndpoint.Address + ":" + hostEndpoint.Port
                             + "  " + (lastTelemetrySeconds * 1000f).ToString("F0") + " ms";
                }
                else
                {
                    detail = "已连接";
                }
            }

            // 未指定手动地址时周期性广播 HELLO 做自动发现。
            if (!hostAddressKnown)
            {
                discoveryTimer -= deltaTime;
                if (discoveryTimer <= 0f)
                {
                    discoveryTimer = 0.5f;
                    SendHello(BroadcastEndpoint);
                }
            }
            else if (lastTelemetrySeconds > config.linkLostSeconds)
            {
                connectTimer -= deltaTime;
                if (connectTimer <= 0f)
                {
                    connectTimer = 1.5f;
                    SendHello(hostEndpoint);
                }
            }
        }

        private void SendHeartbeat(float deltaTime)
        {
            heartbeatTimer -= deltaTime;
            if (heartbeatTimer > 0f)
            {
                return;
            }

            heartbeatTimer = Mathf.Max(0.2f, config.heartbeatIntervalSeconds);

            int length = WireProtocol.BuildHeartbeat(
                sendBuffer,
                NextSequence(),
                (uint)(Time.unscaledTimeAsDouble * 1000.0),
                AppState.Boot,
                (byte)(status == LinkStatus.Connected ? 0 : 1));

            SendTo(WireProtocol.TypeHeartbeat, sendBuffer, length, hostAddressKnown ? hostEndpoint : BroadcastEndpoint);
        }

        private void SendHello(IPEndPoint target)
        {
            if (target == null)
            {
                return;
            }

            int length = WireProtocol.BuildHello(sendBuffer, NextSequence(), 0xA1E1, 0x01);
            SendTo(WireProtocol.TypeHello, sendBuffer, length, target);
        }

        private void SendTo(byte type, byte[] buffer, int length, IPEndPoint target)
        {
            if (client == null || target == null)
            {
                return;
            }

            try
            {
                client.Send(buffer, length, target);
                if (config.verboseComms)
                {
                    Debug.Log("[ArmWrestling] → " + WireProtocol.DescribeType(type) + " " + length + " B to " + target);
                }
            }
            catch (Exception exception)
            {
                if (config.verboseComms)
                {
                    Debug.LogWarning("[ArmWrestling] 发送失败: " + exception.Message);
                }
            }
        }

        private ushort NextSequence()
        {
            sequence = (ushort)(sequence + 1);
            return sequence;
        }

        // ------------------------------------------------------------------
        // 接收
        // ------------------------------------------------------------------

        private void ReceiveAll()
        {
            if (client == null)
            {
                return;
            }

            int guard = 0;
            while (client.Available > 0 && guard++ < 64)
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 0);
                int length;
                try
                {
                    byte[] data = client.Receive(ref sender);
                    length = data.Length;
                    receiveEndpoint = sender;
                    int copy = Mathf.Min(length, receiveBuffer.Length);
                    Buffer.BlockCopy(data, 0, receiveBuffer, 0, copy);
                    length = copy;
                }
                catch (SocketException)
                {
                    return;
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                HandleDatagram(length, sender);
            }
        }

        private void HandleDatagram(int length, IPEndPoint sender)
        {
            WireProtocol.Frame frame;
            if (!WireProtocol.TryParseFrame(receiveBuffer, length, out frame))
            {
                if (config.verboseComms)
                {
                    Debug.LogWarning("[ArmWrestling] 丢弃非法数据报 " + length + " B from " + sender);
                }

                return;
            }

            switch (frame.type)
            {
                case WireProtocol.TypeHelloAck:
                    HandleHelloAck(frame, sender);
                    break;

                case WireProtocol.TypeTelemetry:
                    HandleTelemetry(frame);
                    break;

                case WireProtocol.TypeEventAck:
                    HandleEventAck(frame);
                    break;

                default:
                    if (config.verboseComms)
                    {
                        Debug.Log("[ArmWrestling] 忽略报文 " + WireProtocol.DescribeType(frame.type));
                    }

                    break;
            }
        }

        private void HandleHelloAck(WireProtocol.Frame frame, IPEndPoint sender)
        {
            WireProtocol.DeviceInfo info;
            if (!WireProtocol.TryReadHelloAck(receiveBuffer, frame, out info))
            {
                return;
            }

            if (info.result != 0)
            {
                detail = "控制端拒绝连接（代码 " + info.result + "）";
                status = LinkStatus.Fault;
                return;
            }

            if (!string.IsNullOrEmpty(config.manualHostIp))
            {
                // 手动指定地址时不接受自动发现结果，避免连到错误的机器。
                return;
            }

            if (!hostAddressKnown || hostEndpoint == null || !hostEndpoint.Equals(sender))
            {
                hostEndpoint = new IPEndPoint(sender.Address, config.hostPort);
                hostAddressKnown = true;
                status = LinkStatus.Connecting;
                detail = "发现控制端 " + sender.Address;
                Debug.Log("[ArmWrestling] 自动发现控制端: " + hostEndpoint);
            }

            lastTelemetrySeconds = Mathf.Min(lastTelemetrySeconds, config.linkLostSeconds * 0.5f);
        }

        private void HandleTelemetry(WireProtocol.Frame frame)
        {
            WireProtocol.Telemetry telemetry;
            if (!WireProtocol.TryReadTelemetry(receiveBuffer, frame, out telemetry))
            {
                return;
            }

            // 序号回绕判断：仅当“倒退”超过 1000 才认为是乱序旧包。
            if (hasTelemetrySequence)
            {
                int delta = (short)(frame.sequence - lastTelemetrySequence);
                if (delta <= 0 && delta > -1000)
                {
                    return;
                }
            }

            lastTelemetrySequence = frame.sequence;
            hasTelemetrySequence = true;

            lastTelemetrySeconds = 0f;
            secondsSinceLastData = 0f;
            lastTorque = telemetry.torqueNm;
            lastDeviceState = telemetry.deviceState;
            hostPhase = telemetry.matchPhase;

            bool angleValid = config.IsMechanicalAngleValid(telemetry.angleDeg);

            latestSample = new AngleSample
            {
                localTime = Time.unscaledTimeAsDouble,
                angleDeg = telemetry.angleDeg,
                angularVelocityDegPerSecond = telemetry.angularVelocityDegPerSecond,
                torqueNm = telemetry.torqueNm,
                deviceState = telemetry.deviceState,
                valid = telemetry.dataValid && angleValid,
                faultCode = telemetry.faultCode,
                frameCounter = ++frameCounter
            };

            hasNewSample = true;

            if (!angleValid && config.verboseComms)
            {
                Debug.LogWarning("[ArmWrestling] 角度超范围被丢弃: " + telemetry.angleDeg.ToString("F2"));
            }
        }

        private void HandleEventAck(WireProtocol.Frame frame)
        {
            ushort eventId;
            byte result;
            if (!WireProtocol.TryReadEventAck(receiveBuffer, frame, out eventId, out result))
            {
                return;
            }

            for (int i = pendingEvents.Count - 1; i >= 0; i--)
            {
                if (pendingEvents[i].eventId != eventId)
                {
                    continue;
                }

                if (result != 0)
                {
                    Debug.LogWarning("[ArmWrestling] 事件被控制端拒绝，eventId=" + eventId + " result=" + result);
                }

                pendingEvents.RemoveAt(i);
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

        // ------------------------------------------------------------------
        // 事件
        // ------------------------------------------------------------------

        public void SendEvent(SessionEventCode code, byte parameter, float value)
        {
            PendingEvent pending = new PendingEvent
            {
                eventId = nextEventId++,
                code = code,
                parameter = parameter,
                value = value,
                attempts = 0,
                nextSendTime = 0f
            };

            pendingEvents.Add(pending);
            FlushEvents();
        }

        private void FlushEvents()
        {
            if (pendingEvents.Count == 0 || client == null)
            {
                return;
            }

            float now = Time.unscaledTime;
            IPEndPoint target = hostAddressKnown ? hostEndpoint : BroadcastEndpoint;

            for (int i = pendingEvents.Count - 1; i >= 0; i--)
            {
                PendingEvent pending = pendingEvents[i];
                if (pending.attempts > 0 && now < pending.nextSendTime)
                {
                    continue;
                }

                if (pending.attempts >= config.eventMaxAttempts)
                {
                    Debug.LogWarning("[ArmWrestling] 事件发送失败（无确认）: " + pending.code);
                    pendingEvents.RemoveAt(i);
                    continue;
                }

                int length = WireProtocol.BuildEvent(
                    sendBuffer, NextSequence(), pending.eventId, pending.code, pending.parameter, pending.value);
                SendTo(WireProtocol.TypeEvent, sendBuffer, length, target);

                pending.attempts++;
                pending.nextSendTime = now + Mathf.Max(0.05f, config.eventRetrySeconds);
                pendingEvents[i] = pending;
            }
        }

        public void Reconnect()
        {
            pendingEvents.Clear();
            hasTelemetrySequence = false;
            lastTelemetrySeconds = 999f;
            secondsSinceLastData = 999f;
            hostAddressKnown = false;
            hostEndpoint = null;
            OpenSocket();
        }

        public void Shutdown()
        {
            pendingEvents.Clear();
            CloseSocket();
            status = LinkStatus.Disconnected;
        }
    }
}
