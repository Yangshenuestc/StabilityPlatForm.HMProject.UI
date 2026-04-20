namespace StabilityPlatForm.HMProject.BusinessLogicLayer.Services
{
    public class ChannelT80Tracker
    {
        /// <summary>
        /// 历史最高效率 (PCE_max)
        /// </summary>
        public double MaxEfficiency { get; private set; } = 0;

        /// <summary>
        /// 连续低于 T80 阈值的计数器
        /// </summary>
        public int ContinuousDropCount { get; private set; } = 0;

        /// <summary>
        /// 标记当前通道是否已经触发过预警
        /// </summary>
        public bool IsT80Alerted { get; private set; } = false;

        // ---- 算法判定参数 ----
        private readonly double _thresholdRatio = 0.80;        // T80 衰减比例
        private readonly int _requiredConsecutiveDrops = 3;    // 必须连续 3 次跌破阈值才算数（防抖）
        private readonly double _absoluteMinPce = 1.0;         // 绝对效率最小值(%)，低于此值认为是死区，不参与计算

        /// <summary>
        /// 每次得出 IV 曲线的新效率后调用此方法，评估 T80 状态
        /// </summary>
        /// <param name="currentPce">当前最新测出的光电转换效率</param>
        /// <returns>当且仅当此刻【刚刚】满足 T80 衰减条件时返回 true</returns>
        public bool EvaluateT80(double currentPce)
        {
            // 1. 如果该通道已报废（预警过），或者处于起步的死区状态，不进行判定
            if (IsT80Alerted || (currentPce < _absoluteMinPce && MaxEfficiency < _absoluteMinPce))
            {
                return false;
            }

            // 2. 动态寻峰：如果处于爬坡期，刷新最大记录，并重置衰减计数器
            if (currentPce > MaxEfficiency)
            {
                MaxEfficiency = currentPce;
                ContinuousDropCount = 0;
                return false;
            }

            // 3. 计算实时的 T80 门限
            double currentT80Threshold = MaxEfficiency * _thresholdRatio;

            // 4. 判断是否发生衰减
            if (currentPce < currentT80Threshold)
            {
                ContinuousDropCount++; // 记录一次有效跌破

                // 5. 连续跌破次数达标，确认发生真实衰减
                if (ContinuousDropCount >= _requiredConsecutiveDrops)
                {
                    IsT80Alerted = true; // 锁定预警状态
                    return true;         // 返回 true，通知上层业务触发系统警告
                }
            }
            else
            {
                // 如果中间某一次反弹回到了 80% 以上，说明之前的下降是噪声，计数器清零
                ContinuousDropCount = 0;
            }

            return false;
        }
    }
}