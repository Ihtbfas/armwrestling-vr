using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>下位机报告的工作状态，与通信协议中的 deviceState 字段一一对应。</summary>
    public enum DeviceState : byte
    {
        Init = 0,
        Idle = 1,
        Ready = 2,
        Running = 3,
        Fault = 4,
        EmergencyStop = 5
    }

    /// <summary>上位机报告的对战阶段，仅用于显示与一致性校验。</summary>
    public enum MatchPhase : byte
    {
        None = 0,
        Prepare = 1,
        Countdown = 2,
        Fighting = 3,
        Paused = 4,
        Finished = 5
    }

    /// <summary>虚拟对手角色。数值即角色编号，会随“角色选择”事件发给上位机。</summary>
    public enum OpponentRole : byte
    {
        Balanced = 1,
        Defensive = 2,
        Rhythmic = 3
    }

    /// <summary>VR 端自身的流程状态。</summary>
    public enum AppState
    {
        Boot = 0,
        DeviceCheck = 1,
        Calibration = 2,
        Lobby = 3,
        Ready = 4,
        Countdown = 5,
        Fighting = 6,
        Paused = 7,
        Finished = 8
    }

    /// <summary>VR 发往控制端的交互事件编号。</summary>
    public enum SessionEventCode : byte
    {
        RoleSelect = 1,
        CalibrationDone = 2,
        MatchStart = 3,
        Pause = 4,
        Resume = 5,
        MatchEnd = 6,
        Reset = 7,
        EmergencyStopRequest = 8,
        Ready = 9
    }

    /// <summary>胜负判定结果。</summary>
    public enum MatchOutcome
    {
        None = 0,
        Draw = 1,
        PlayerWins = 2,
        OpponentWins = 3
    }

    /// <summary>角度数据来源。开发阶段用模拟数据，联调后切换到网络数据。</summary>
    public enum DataSourceMode
    {
        Simulated = 0,
        Network = 1,
        /// <summary>用 VR 手柄直接驱动虚拟手臂（机械装置未就绪时的临时输入）。</summary>
        Controller = 2,
        /// <summary>用 PICO 手部追踪（真实的双手）驱动虚拟手臂。</summary>
        Hand = 3
    }

    /// <summary>连接可用性，供界面显示。</summary>
    public enum LinkStatus
    {
        Disconnected = 0,
        Connecting = 1,
        Connected = 2,
        Stale = 3,
        Fault = 4
    }

    public static class DisplayNames
    {
        public static string Of(DeviceState state)
        {
            switch (state)
            {
                case DeviceState.Init: return "初始化";
                case DeviceState.Idle: return "待机";
                case DeviceState.Ready: return "就绪";
                case DeviceState.Running: return "运行";
                case DeviceState.Fault: return "故障";
                case DeviceState.EmergencyStop: return "急停";
                default: return "未知";
            }
        }

        public static string Of(OpponentRole role)
        {
            switch (role)
            {
                case OpponentRole.Balanced: return "均衡型";
                case OpponentRole.Defensive: return "防守型";
                case OpponentRole.Rhythmic: return "节奏型";
                default: return "未知";
            }
        }

        public static string Of(MatchOutcome outcome)
        {
            switch (outcome)
            {
                case MatchOutcome.Draw: return "平局";
                case MatchOutcome.PlayerWins: return "你赢了";
                case MatchOutcome.OpponentWins: return "对手获胜";
                default: return "未判定";
            }
        }

        public static string Of(LinkStatus status)
        {
            switch (status)
            {
                case LinkStatus.Connected: return "已连接";
                case LinkStatus.Connecting: return "连接中";
                case LinkStatus.Stale: return "数据超时";
                case LinkStatus.Fault: return "通信故障";
                case LinkStatus.Disconnected: return "未连接";
                default: return "未知";
            }
        }
    }
}

