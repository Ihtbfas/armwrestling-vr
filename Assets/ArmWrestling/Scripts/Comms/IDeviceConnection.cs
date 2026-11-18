using System;

namespace ArmWrestlingVR
{
    /// <summary>一次角度采样。无论来自网络还是模拟器，都统一成这个结构交给上层。</summary>
    public struct AngleSample
    {
        /// <summary>收到该采样的本地时刻（Time.unscaledTimeAsDouble）。</summary>
        public double localTime;
        public float angleDeg;
        public float angularVelocityDegPerSecond;
        public float torqueNm;
        public DeviceState deviceState;
        public bool valid;
        public ushort faultCode;
        public ulong frameCounter;
    }

    /// <summary>
    /// 角度与设备状态的数据源抽象。
    ///
    /// 模拟数据源与真实 UDP 数据源实现同一接口，因此“设备未完成时先用模拟数据完成软件开发，
    /// 完成后再替换为上位机数据”只需要在界面或配置里切一个枚举，不需要改任何场景或游戏逻辑代码。
    /// </summary>
    public interface IDeviceConnection
    {
        string Name { get; }
        LinkStatus Status { get; }
        string Detail { get; }
        bool IsDataAvailable { get; }
        float SecondsSinceLastData { get; }
        float LastTorqueNm { get; }
        DeviceState LastDeviceState { get; }
        MatchPhase HostMatchPhase { get; }

        /// <summary>每帧调用，处理收发、超时判断。</summary>
        void Tick(float deltaTime, bool matchActive);

        /// <summary>取出最近一次新采样。返回 false 表示本帧没有新数据（上层保持上次姿态）。</summary>
        bool TryConsumeSample(out AngleSample sample);

        /// <summary>向控制端发送一个交互事件（已带序号与重发机制）。</summary>
        void SendEvent(SessionEventCode code, byte parameter, float value);

        /// <summary>主动重新连接 / 重新开始握手。</summary>
        void Reconnect();

        void Shutdown();
    }

    /// <summary>模拟数据源的可调参数，只有模拟实现会用到。</summary>
    public interface ISimulationControl
    {
        void SetActiveMatch(bool active, OpponentRole role, float opponentScale, bool autoPlayer);
        string SimulationHint { get; }
    }

    public delegate void SampleReceivedHandler(AngleSample sample);
}
