using System;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>UDP V0.1 status exchange on the Unity main loop.</summary>
    public sealed class VrUdpClient : IDeviceConnection
    {
        private const double HeartbeatSeconds = 0.2;
        private const double StatusTimeoutSeconds = 1.0;
        private const long MaxSequence = 9007199254740991L;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly ArmWrestlingConfig config;
        private readonly string host;
        private readonly int port;
        private UdpClient socket;
        private long nextSequence;
        private double lastTick = double.NegativeInfinity;
        private double lastStatus = double.NegativeInfinity;
        private double nextSend;
        private double openAfter;
        private string lastError = "";
        private StatusMessage status;
        private AngleSample sample;
        private bool pendingSample;

        public string CharacterId { get; private set; } = "demo";
        public string MatchState { get; private set; } = "waiting";
        public long LastSequence => status?.seq ?? -1;
        public string Reason => status?.reason ?? "";
        public string Name => "上位机 UDP V0.1";
        public float LastTorqueNm => float.NaN;
        public float SecondsSinceLastData => (float)(Time.realtimeSinceStartupAsDouble - lastStatus);
        public LinkStatus Status => socket == null ? LinkStatus.Disconnected
            : status == null ? LinkStatus.Connecting
            : SecondsSinceLastData >= StatusTimeoutSeconds ? LinkStatus.Stale : LinkStatus.Connected;
        public bool IsDataAvailable => Status == LinkStatus.Connected && status.angle_rad.HasValue
            && config.IsMechanicalAngleValid(ToDegrees(status.angle_rad.Value));
        public bool IsRunning => IsDataAvailable && status.mode == "enabled" && Reason == "running";
        public bool IsReadyToRun => IsDataAvailable && SecondsSinceLastData <= config.dataTimeoutSeconds && status.mode == "stop"
            && (Reason == "match_waiting" || Reason == "match_paused");
        public DeviceState LastDeviceState => Reason == "device_fault" ? DeviceState.Fault
            : IsRunning ? DeviceState.Running : DeviceState.Idle;
        public MatchPhase HostMatchPhase => MatchPhase.Fighting;
        public string Detail => Status != LinkStatus.Connected
            ? (string.IsNullOrEmpty(lastError) ? "等待上位机 " + host + ":" + port : lastError)
            : (IsDataAvailable ? ToDegrees(status.angle_rad.Value).ToString("F1") + "°" : "角度无效")
                + " | " + status.mode + " | " + status.reason;

        public VrUdpClient(ArmWrestlingConfig config, string hostIp, int hostPort)
        {
            this.config = config;
            host = string.IsNullOrEmpty(hostIp) ? "127.0.0.1" : hostIp;
            port = hostPort;
        }

        public void SetSession(string character, string match)
        {
            if (character == CharacterId && match == MatchState) return;
            CharacterId = character;
            MatchState = match;
            nextSend = 0;
            SendIfDue(Time.realtimeSinceStartupAsDouble);
        }

        public void Tick(float deltaTime, bool matchActive)
        {
            double now = Time.realtimeSinceStartupAsDouble;
            bool loopInterrupted = now - lastTick >= config.dataTimeoutSeconds;
            lastTick = now;
            if (matchActive && (loopInterrupted || (status != null && now - lastStatus >= StatusTimeoutSeconds)))
            {
                // Let the app enter Paused before it can send another running heartbeat.
                Reconnect();
                return;
            }
            if (status != null && now - lastStatus >= StatusTimeoutSeconds) Reconnect();
            if (socket == null && now >= openAfter) Open(now);
            if (socket == null) return;
            ReceivePending(now);
            SendIfDue(now);
        }

        public bool TryConsumeSample(out AngleSample result)
        {
            result = sample;
            bool pending = pendingSample;
            pendingSample = false;
            return pending;
        }

        public void SendEvent(SessionEventCode code, byte parameter, float value)
        {
            // UDP V0.1 sends complete match states, not one-shot events.
        }

        public void Reconnect()
        {
            Shutdown();
            openAfter = 0;
        }

        public void Shutdown()
        {
            socket?.Close();
            socket = null;
            status = null;
            pendingSample = false;
            lastStatus = double.NegativeInfinity;
            nextSequence = 0;
            nextSend = 0;
        }

        private void Open(double now)
        {
            if (!IPAddress.TryParse(host, out var address) || address.AddressFamily != AddressFamily.InterNetwork
                || address.Equals(IPAddress.Any) || address.Equals(IPAddress.Broadcast))
            {
                lastError = "请填写明确的控制端 IPv4 地址";
                openAfter = double.PositiveInfinity;
                return;
            }
            UdpClient created = null;
            try
            {
                created = new UdpClient(0);
                created.Connect(new IPEndPoint(address, port));
                created.Client.Blocking = false;
                socket = created;
            }
            catch (SocketException exception)
            {
                created?.Close();
                lastError = exception.SocketErrorCode.ToString();
                openAfter = now + HeartbeatSeconds;
            }
        }

        private void SendIfDue(double now)
        {
            if (socket == null || now < nextSend) return;
            nextSend = now + HeartbeatSeconds;
            if (nextSequence > MaxSequence)
            {
                Reconnect();
                return;
            }
            var update = new UpdateMessage
            {
                seq = nextSequence, character_id = CharacterId, match_state = MatchState
            };
            byte[] bytes = Utf8.GetBytes(JsonUtility.ToJson(update));
            try
            {
                if (socket.Send(bytes, bytes.Length) == bytes.Length) nextSequence++;
            }
            catch (SocketException exception)
            {
                lastError = exception.SocketErrorCode.ToString();
            }
        }

        private void ReceivePending(double now)
        {
            for (int i = 0; i < 64; i++)
            {
                try
                {
                    if (!socket.Client.Poll(0, SelectMode.SelectRead)) break;
                    IPEndPoint source = null;
                    byte[] packet = socket.Receive(ref source);
                    if (!TryDecode(packet, out var received) || received.seq <= LastSequence) continue;

                    bool previousValid = IsDataAvailable;
                    double age = now - lastStatus;
                    status = received;
                    lastStatus = now;
                    lastError = "";
                    pendingSample = IsDataAvailable;
                    if (!pendingSample) continue;
                    float angle = ToDegrees(received.angle_rad.Value);
                    // Estimated speed is for pose smoothing only.
                    float speed = previousValid && age > 0.001 && age <= config.dataTimeoutSeconds
                        ? (angle - sample.angleDeg) / (float)age : 0f;
                    sample = new AngleSample
                    {
                        localTime = Time.unscaledTimeAsDouble,
                        angleDeg = angle,
                        angularVelocityDegPerSecond = speed,
                        torqueNm = float.NaN,
                        deviceState = LastDeviceState,
                        valid = true,
                        frameCounter = (ulong)received.seq
                    };
                }
                catch (SocketException exception)
                {
                    if (exception.SocketErrorCode != SocketError.WouldBlock)
                        lastError = exception.SocketErrorCode.ToString();
                    break;
                }
            }
        }

        private static float ToDegrees(double radians) => -(float)radians * Mathf.Rad2Deg;

        [Serializable]
        private sealed class UpdateMessage
        {
            public int v = 1;
            public string type = "vr_update";
            public long seq;
            public string character_id;
            public string match_state;
        }

        public sealed class StatusMessage
        {
            public int v;
            public string type;
            public long seq;
            public double? angle_rad;
            public string mode;
            public string reason;
        }

        private static bool IsJsonSpace(byte value) => value == 32 || value == 9 || value == 10 || value == 13;

        private static bool IsSingleObject(byte[] packet)
        {
            int first = 0;
            while (first < packet.Length && IsJsonSpace(packet[first])) first++;
            if (first == packet.Length || packet[first] != '{') return false;
            bool quoted = false;
            int depth = 0;
            for (int i = first; i < packet.Length; i++)
            {
                byte value = packet[i];
                if (quoted)
                {
                    if (value == 92) i++; // Skip an escaped string byte.
                    else if (value == '"') quoted = false;
                }
                else if (value == '"') quoted = true;
                else if (value == '{' || value == '[') depth++;
                else if ((value == '}' || value == ']') && --depth == 0)
                {
                    if (value != '}') return false;
                    while (++i < packet.Length)
                        if (!IsJsonSpace(packet[i])) return false;
                    return true;
                }
            }
            return false;
        }

        public static bool TryDecode(byte[] packet, out StatusMessage result)
        {
            result = null;
            // Mono's JSON reader can ignore trailing objects, so check datagram framing first.
            if (packet == null || packet.Length == 0 || packet.Length > 8192 || !IsSingleObject(packet))
                return false;
            try
            {
                // The .NET JSON reader retains JSON types, including null vs. absent vs. zero.
                Utf8.GetCharCount(packet);
                using (var reader = JsonReaderWriterFactory.CreateJsonReader(packet, XmlDictionaryReaderQuotas.Max))
                {
                    if (reader.MoveToContent() != XmlNodeType.Element || reader.GetAttribute("type") != "object")
                        return false;
                    var value = new StatusMessage();
                    int fields = 0;
                    reader.ReadStartElement();
                    while (reader.MoveToContent() == XmlNodeType.Element)
                    {
                        string field = reader.LocalName;
                        string kind = reader.GetAttribute("type");
                        int flag;
                        switch (field)
                        {
                            case "v":
                                if (kind != "number" || reader.ReadElementContentAsString() != "1") return false;
                                value.v = 1; flag = 1; break;
                            case "type":
                                if (kind != "string" || reader.ReadElementContentAsString() != "status") return false;
                                value.type = "status"; flag = 2; break;
                            case "seq":
                                if (kind != "number" || !long.TryParse(reader.ReadElementContentAsString(),
                                    NumberStyles.None, CultureInfo.InvariantCulture, out value.seq)
                                    || value.seq > MaxSequence) return false;
                                flag = 4; break;
                            case "angle_rad":
                                if (kind == "null") reader.ReadElementContentAsString();
                                else
                                {
                                    if (kind != "number" || !double.TryParse(reader.ReadElementContentAsString(),
                                        NumberStyles.Float, CultureInfo.InvariantCulture, out double angle)
                                        || double.IsNaN(angle) || double.IsInfinity(angle) || Math.Abs(angle) > 100)
                                        return false;
                                    value.angle_rad = angle;
                                }
                                flag = 8; break;
                            case "mode":
                                if (kind != "string") return false;
                                value.mode = reader.ReadElementContentAsString();
                                if (value.mode != "enabled" && value.mode != "stop") return false;
                                flag = 16; break;
                            case "reason":
                                if (kind != "string") return false;
                                value.reason = reader.ReadElementContentAsString();
                                if (string.IsNullOrWhiteSpace(value.reason)) return false;
                                flag = 32; break;
                            default:
                                reader.Skip();
                                continue;
                        }
                        if ((fields & flag) != 0) return false;
                        fields |= flag;
                    }
                    if (fields != 63) return false;
                    reader.ReadEndElement();
                    if (reader.MoveToContent() != XmlNodeType.None) return false;
                    result = value;
                    return true;
                }
            }
            catch (Exception exception) when (exception is XmlException || exception is ArgumentException
                || exception is FormatException || exception is InvalidOperationException)
            {
                return false;
            }
        }
    }
}

