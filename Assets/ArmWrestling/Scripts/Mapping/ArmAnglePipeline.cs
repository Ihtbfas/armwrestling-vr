using UnityEngine;

namespace ArmWrestlingVR
{
    /// <summary>
    /// 机械角度 → 虚拟手臂角度的处理管线。
    ///
    /// 依次完成（对应开题报告 4.1.4 (4)、4.2.4 (2)）：
    /// 1. 有效性检查：非有限值、超出机械量程、序号倒退的采样直接丢弃；
    /// 2. 跳变抑制：短时间内角度突变超过物理可能值的数据视为异常，连续出现两次才采纳；
    /// 3. 低通滤波：一阶惯性滤波抑制角噪声，时间常数可调，兼顾稳定性与延迟；
    /// 4. 短时外推：两次采样之间用角速度外推，降低低刷新率带来的台阶感；
    /// 5. 超时处理：超过 dataTimeoutSeconds 没有新数据则冻结姿态并标记 stale，
    ///    上层据此暂停对战并提示通信异常，而不是继续使用过期数据。
    /// </summary>
    public class ArmAnglePipeline
    {
        private readonly ArmWrestlingConfig config;

        private bool hasSample;
        private double lastSampleTime;
        private float rawAngleDeg;
        private float rawAngularVelocity;
        private float filteredVisualAngleDeg;
        private bool filterInitialised;

        private float lastAcceptedAngleDeg;
        private double lastAcceptedTime;
        private int suspectJumpCount;

        public ArmAnglePipeline(ArmWrestlingConfig config)
        {
            this.config = config;
        }

        public bool HasSample
        {
            get { return hasSample; }
        }

        public float MechanicalAngleDeg
        {
            get { return rawAngleDeg; }
        }

        public float AngularVelocityDegPerSecond
        {
            get { return rawAngularVelocity; }
        }

        public float VisualAngleDeg
        {
            get { return filteredVisualAngleDeg; }
        }

        public double LastSampleTime
        {
            get { return lastSampleTime; }
        }

        public int RejectedSamples { get; private set; }

        public int SuppressedJumps { get; private set; }

        public bool IsStale(double now)
        {
            return !hasSample || (now - lastSampleTime) > config.dataTimeoutSeconds;
        }

        public float SecondsSinceSample(double now)
        {
            return hasSample ? (float)(now - lastSampleTime) : 999f;
        }

        public void Reset()
        {
            hasSample = false;
            filterInitialised = false;
            suspectJumpCount = 0;
            rawAngleDeg = 0f;
            rawAngularVelocity = 0f;
            filteredVisualAngleDeg = 0f;
            RejectedSamples = 0;
            SuppressedJumps = 0;
        }

        /// <summary>推入一帧新采样。返回 false 表示该采样被丢弃。</summary>
        public bool Push(AngleSample sample)
        {
            if (!sample.valid || !config.IsMechanicalAngleValid(sample.angleDeg))
            {
                RejectedSamples++;
                return false;
            }

            if (hasSample)
            {
                double dt = sample.localTime - lastAcceptedTime;
                if (dt > 0.0005 && dt < 0.5)
                {
                    float impliedSpeed = Mathf.Abs(sample.angleDeg - lastAcceptedAngleDeg) / (float)dt;
                    if (impliedSpeed > 900f)
                    {
                        // 认为是单帧跳变；连续两次同样跳变才接受，避免数据源异常时永久锁死。
                        suspectJumpCount++;
                        if (suspectJumpCount < 2)
                        {
                            SuppressedJumps++;
                            return false;
                        }
                    }
                }
            }

            suspectJumpCount = 0;
            hasSample = true;
            lastSampleTime = sample.localTime;
            lastAcceptedTime = sample.localTime;
            lastAcceptedAngleDeg = sample.angleDeg;
            rawAngleDeg = sample.angleDeg;
            rawAngularVelocity = sample.angularVelocityDegPerSecond;
            return true;
        }

        /// <summary>计算本帧的视觉角度，并推进滤波状态。每帧只调用一次。</summary>
        public float Evaluate(double now, float deltaTime)
        {
            if (!hasSample)
            {
                return filteredVisualAngleDeg;
            }

            float target = ComputeInstantaneousVisualAngle(now);

            if (!filterInitialised || config.filterTimeConstant <= 0.0001f)
            {
                filteredVisualAngleDeg = target;
                filterInitialised = true;
                return filteredVisualAngleDeg;
            }

            float alpha = 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / config.filterTimeConstant);
            filteredVisualAngleDeg = Mathf.Lerp(filteredVisualAngleDeg, target, Mathf.Clamp01(alpha));
            return filteredVisualAngleDeg;
        }

        private float ComputeInstantaneousVisualAngle(double now)
        {
            double age = now - lastSampleTime;

            // 超时后不再外推，冻结在最后姿态，等待上层判断是否暂停。
            if (age > config.dataTimeoutSeconds)
            {
                return filteredVisualAngleDeg;
            }

            float angle = rawAngleDeg;
            if (config.enableExtrapolation && age > 0.0005 && age <= config.maxExtrapolationSeconds)
            {
                angle += rawAngularVelocity * (float)age;
            }

            return config.ToVisualDegrees(angle);
        }
    }
}
