using System;
using System.Text;
using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 上下位机（VR 端 ↔ PC 控制端）UDP 二进制协议的编解码实现。
    ///
    /// 帧格式（小端）：
    /// <code>
    ///  偏移  长度  字段
    ///  0     2     magic     = 0x41 0x57  ('A' 'W')
    ///  2     1     version   = 0x01
    ///  3     1     type      消息类型
    ///  4     2     seq       uint16，发送方递增序号
    ///  6     2     length    uint16，payload 字节数
    ///  8     N     payload
    ///  8+N   2     crc16     CRC16-CCITT-FALSE，覆盖 [2, 8+N)
    /// </code>
    /// 详细字段定义见 <c>docs/通信协议.md</c>。
    /// </summary>
    public static class WireProtocol
    {
        public const byte Magic0 = 0x41;
        public const byte Magic1 = 0x57;
        public const byte Version = 0x01;
        public const int HeaderSize = 8;
        public const int CrcSize = 2;
        public const int MaxPayload = 256;

        public const byte TypeHello = 0x01;
        public const byte TypeHelloAck = 0x02;
        public const byte TypeTelemetry = 0x10;
        public const byte TypeEvent = 0x20;
        public const byte TypeEventAck = 0x21;
        public const byte TypeHeartbeat = 0x40;

        /// <summary>一帧解析结果。</summary>
        public struct Frame
        {
            public byte type;
            public ushort sequence;
            public int payloadOffset;
            public int payloadLength;
        }

        /// <summary>上位机上报的一帧遥测（0x10）。</summary>
        public struct Telemetry
        {
            public uint hostTimeMs;
            public float angleDeg;
            public float angularVelocityDegPerSecond;
            public float torqueNm;
            public DeviceState deviceState;
            public MatchPhase matchPhase;
            public bool dataValid;
            public bool calibrated;
            public bool torqueIsEstimated;
            public ushort faultCode;

            public const int PayloadSize = 24;
        }

        /// <summary>HELLO_ACK 中下发的设备参数（0x02）。</summary>
        public struct DeviceInfo
        {
            public ushort clientId;
            public float angleMinDeg;
            public float angleMaxDeg;
            public float torqueLimitNm;
            public uint hostTimeMs;
            public byte result;
        }

        // ------------------------------------------------------------------
        // CRC16 / CCITT-FALSE
        // ------------------------------------------------------------------

        public static ushort Crc16(byte[] buffer, int offset, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= (ushort)(buffer[offset + i] << 8);
                for (int bit = 0; bit < 8; bit++)
                {
                    if ((crc & 0x8000) != 0)
                    {
                        crc = (ushort)((crc << 1) ^ 0x1021);
                    }
                    else
                    {
                        crc = (ushort)(crc << 1);
                    }
                }
            }

            return crc;
        }

        // ------------------------------------------------------------------
        // 写帧
        // ------------------------------------------------------------------

        public static int WriteHeader(byte[] buffer, byte type, ushort sequence, ushort payloadLength)
        {
            buffer[0] = Magic0;
            buffer[1] = Magic1;
            buffer[2] = Version;
            buffer[3] = type;
            WriteUInt16(buffer, 4, sequence);
            WriteUInt16(buffer, 6, payloadLength);
            return HeaderSize;
        }

        public static void FinishFrame(byte[] buffer, int totalPayloadLength)
        {
            int bodyEnd = HeaderSize + totalPayloadLength;
            ushort crc = Crc16(buffer, 2, bodyEnd - 2);
            WriteUInt16(buffer, bodyEnd, crc);
        }

        public static int BuildHello(byte[] buffer, ushort sequence, ushort clientId, byte capabilities)
        {
            WriteHeader(buffer, TypeHello, sequence, 4);
            WriteUInt16(buffer, 8, clientId);
            buffer[10] = Version;
            buffer[11] = capabilities;
            FinishFrame(buffer, 4);
            return HeaderSize + 4 + CrcSize;
        }

        public static int BuildHeartbeat(byte[] buffer, ushort sequence, uint unityTimeMs, AppState state, byte faultFlags)
        {
            WriteHeader(buffer, TypeHeartbeat, sequence, 8);
            WriteUInt32(buffer, 8, unityTimeMs);
            buffer[12] = (byte)state;
            buffer[13] = faultFlags;
            WriteUInt16(buffer, 14, 0);
            FinishFrame(buffer, 8);
            return HeaderSize + 8 + CrcSize;
        }

        public static int BuildEvent(byte[] buffer, ushort sequence, ushort eventId, SessionEventCode code, byte parameter, float value)
        {
            WriteHeader(buffer, TypeEvent, sequence, 8);
            WriteUInt16(buffer, 8, eventId);
            buffer[10] = (byte)code;
            buffer[11] = parameter;
            WriteSingle(buffer, 12, value);
            FinishFrame(buffer, 8);
            return HeaderSize + 8 + CrcSize;
        }

        // ------------------------------------------------------------------
        // 读帧
        // ------------------------------------------------------------------

        /// <summary>校验并解析帧头。返回 false 表示该数据报不是本协议的有效帧。</summary>
        public static bool TryParseFrame(byte[] buffer, int length, out Frame frame)
        {
            frame = default(Frame);

            if (length < HeaderSize + CrcSize || length > HeaderSize + MaxPayload + CrcSize)
            {
                return false;
            }

            if (buffer[0] != Magic0 || buffer[1] != Magic1 || buffer[2] != Version)
            {
                return false;
            }

            ushort payloadLength = ReadUInt16(buffer, 6);
            if (payloadLength > MaxPayload || HeaderSize + payloadLength + CrcSize != length)
            {
                return false;
            }

            ushort expected = ReadUInt16(buffer, HeaderSize + payloadLength);
            ushort actual = Crc16(buffer, 2, HeaderSize + payloadLength - 2);
            if (expected != actual)
            {
                return false;
            }

            frame.type = buffer[3];
            frame.sequence = ReadUInt16(buffer, 4);
            frame.payloadOffset = HeaderSize;
            frame.payloadLength = payloadLength;
            return true;
        }

        public static bool TryReadTelemetry(byte[] buffer, Frame frame, out Telemetry telemetry)
        {
            telemetry = default(Telemetry);
            if (frame.type != TypeTelemetry || frame.payloadLength < Telemetry.PayloadSize)
            {
                return false;
            }

            int o = frame.payloadOffset;
            telemetry.hostTimeMs = ReadUInt32(buffer, o);
            telemetry.angleDeg = ReadSingle(buffer, o + 4);
            telemetry.angularVelocityDegPerSecond = ReadSingle(buffer, o + 8);
            telemetry.torqueNm = ReadSingle(buffer, o + 12);
            telemetry.deviceState = (DeviceState)buffer[o + 16];
            telemetry.matchPhase = (MatchPhase)buffer[o + 17];
            byte flags = buffer[o + 18];
            telemetry.dataValid = (flags & 0x01) != 0;
            telemetry.calibrated = (flags & 0x02) != 0;
            telemetry.torqueIsEstimated = (flags & 0x04) != 0;
            telemetry.faultCode = ReadUInt16(buffer, o + 20);
            return true;
        }

        public static bool TryReadHelloAck(byte[] buffer, Frame frame, out DeviceInfo info)
        {
            info = default(DeviceInfo);
            if (frame.type != TypeHelloAck || frame.payloadLength < 16)
            {
                return false;
            }

            int o = frame.payloadOffset;
            info.clientId = ReadUInt16(buffer, o);
            info.result = buffer[o + 2];
            info.angleMinDeg = ReadSingle(buffer, o + 4);
            info.angleMaxDeg = ReadSingle(buffer, o + 8);
            info.torqueLimitNm = ReadSingle(buffer, o + 12);
            info.hostTimeMs = frame.payloadLength >= 20 ? ReadUInt32(buffer, o + 16) : 0u;
            return true;
        }

        public static bool TryReadEventAck(byte[] buffer, Frame frame, out ushort eventId, out byte result)
        {
            eventId = 0;
            result = 0;
            if (frame.type != TypeEventAck || frame.payloadLength < 3)
            {
                return false;
            }

            eventId = ReadUInt16(buffer, frame.payloadOffset);
            result = buffer[frame.payloadOffset + 2];
            return true;
        }

        // ------------------------------------------------------------------
        // 基础读写（小端）
        // ------------------------------------------------------------------

        public static void WriteUInt16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)(value >> 8);
        }

        public static void WriteUInt32(byte[] buffer, int offset, uint value)
        {
            buffer[offset] = (byte)(value & 0xFF);
            buffer[offset + 1] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 3] = (byte)((value >> 24) & 0xFF);
        }

        public static void WriteSingle(byte[] buffer, int offset, float value)
        {
            byte[] raw = BitConverter.GetBytes(value);
            Buffer.BlockCopy(raw, 0, buffer, offset, 4);
        }

        public static ushort ReadUInt16(byte[] buffer, int offset)
        {
            return (ushort)(buffer[offset] | (buffer[offset + 1] << 8));
        }

        public static uint ReadUInt32(byte[] buffer, int offset)
        {
            return (uint)(buffer[offset]
                          | (buffer[offset + 1] << 8)
                          | (buffer[offset + 2] << 16)
                          | (buffer[offset + 3] << 24));
        }

        public static float ReadSingle(byte[] buffer, int offset)
        {
            return BitConverter.ToSingle(buffer, offset);
        }

        public static string DescribeType(byte type)
        {
            switch (type)
            {
                case TypeHello: return "HELLO";
                case TypeHelloAck: return "HELLO_ACK";
                case TypeTelemetry: return "TELEMETRY";
                case TypeEvent: return "EVENT";
                case TypeEventAck: return "EVENT_ACK";
                case TypeHeartbeat: return "HEARTBEAT";
                default: return "UNKNOWN(0x" + type.ToString("X2") + ")";
            }
        }

        public static byte[] Utf8(string text)
        {
            return Encoding.UTF8.GetBytes(text ?? string.Empty);
        }
    }
}
