using StabilityPlatForm.HMProject.Models.DataStructure;
using StabilityPlatForm.HMProject.Models.Hardwcare;
using StabilityPlatForm.HMProject.Models.Interfaces;
using StabilityPlatForm.HMProject.Utility;

namespace StabilityPlatForm.HMProject.DataAccessLayer.HardwareDriverImplementation
{
    public class SourceTable : ISourceTable
    {
        private static SourceTable _instance = null;
        private static readonly object _locker = new object();
        private bool _isConnected = true;

        //用于记录测试模拟开始的时间
        private DateTime _startTime = DateTime.MinValue;

        public bool IsConnected => _isConnected;

        /// <summary>
        /// 私有化构造函数,防止外部new新对象破坏单例
        /// </summary>
        private SourceTable() { }
        /// <summary>
        /// 线程安全的双检锁单例模式
        /// </summary>
        /// <returns></returns>
        public static SourceTable GetInstance()
        {
            if (_instance == null)
            {
                lock (_locker)
                {
                    if (_instance == null)
                        _instance = new SourceTable();
                }
            }
            return _instance;
        }
        public MethodResult<bool> Close() => MethodResult<bool>.Success(true);
        public MethodResult<bool> Start()
        {
            _startTime = DateTime.Now;
            return MethodResult<bool>.Success(true);
        }
        public MethodResult<bool> StopTest()
        {
            _startTime = DateTime.MinValue;
            return MethodResult<bool>.Success(true);
        }

        public IVData IVMode(ElectricalInfo electricalInfo)
        {
            // 计算步数与电压数组
            double step = electricalInfo.VoltageStep > 0 ? electricalInfo.VoltageStep : 0.01;
            int points = (int)(Math.Abs(electricalInfo.MaxVoltage - electricalInfo.MinVoltage) / step) + 1;
            if (points <= 0) points = 100;

            double[] v = new double[points];
            double[] c = new double[points];

            double startV = electricalInfo.MinVoltage;
            double endV = electricalInfo.MaxVoltage;

            // 确定真实的步长正负号
            step = (endV >= startV) ? Math.Abs(step) : -Math.Abs(step);

            // 判断扫描方向：电压从高到低为反扫(Reverse)，反之为正扫(Forward)
            // 钙钛矿器件通常在反扫时表现出更高的 PCE
            bool isReverseScan = step < 0;

            Random rnd = new Random();

            //时间衰减逻辑：大概10分钟衰减到 60%
            if (_startTime == DateTime.MinValue) _startTime = DateTime.Now;
            // 计算距离第一次扫描已经过去了多少分钟 (真实时间)
            double elapsedMinutes = (DateTime.Now - _startTime).TotalMinutes;
            // 采用指数衰减公式模拟老化：exp(-k * t)
            double decayFactor = Math.Exp(-0.051 * elapsedMinutes);
            // 限制衰减极限，防止一直挂机导致电流变成负数引发系统错误（保底保留5%的性能）
            decayFactor = Math.Max(0.05, decayFactor);

            // =========================================================================
            // 真实单结钙钛矿太阳能电池 单二极管模型参数 
            // =========================================================================
            double IL = 0.00144 * decayFactor;        // 光生电流 (A)
            double n = 1.5;             // 二极管理想因子 (钙钛矿缺陷复合通常在 1.3~2.0 之间)
            double Vt = 0.02585;        // 常温 (300K) 下的热电压 (V)
            double I0 = 2e-16;          // 反向饱和漏电流 (A)
            double Rs = 35.0 + 80.0 * (1.0 - decayFactor);           // 串联电阻 (欧姆) - 影响曲线在 Voc 处的弯曲度 (FF)
            double Rsh = 20000.0 * decayFactor;       // 并联电阻 (欧姆) - 影响曲线在 Jsc 处的倾斜度

            // 【引入迟滞效应模拟】
            // 模拟离子迁移或界面电荷堆积导致的迟滞：正扫时复合加剧、串阻变大
            if (!isReverseScan)
            {
                Rs += 15.0;             // 正扫时表观串联电阻增大，导致填充因子(FF)明显降低
                I0 *= 2.5;              // 正扫时复合增加，导致开路电压(Voc)略微降低
            }

            for (int j = 0; j < points; j++)
            {
                v[j] = startV + j * step;
                double V_current = v[j];

                // 1. 修复：采用标准正向电流假设（光生电流为正，物理意义正确）
                double I_guess = IL;

                for (int iter = 0; iter < 6; iter++)
                {
                    // 此时 I_guess 为正数，V + I*Rs 完美符合物理电压降
                    double exponent = (V_current + I_guess * Rs) / (n * Vt);

                    // 防御性编程
                    if (exponent > 80) exponent = 80;
                    double expTerm = Math.Exp(exponent);

                    // 标准光伏单二极管模型: f(I) = IL - I_diode - I_shunt - I = 0
                    double I_diode = I0 * (expTerm - 1);
                    double I_shunt = (V_current + I_guess * Rs) / Rsh;

                    double f_I = IL - I_diode - I_shunt - I_guess;

                    // 导数全为负数，彻底消灭正反馈，保证牛顿迭代法绝对稳定收敛
                    double f_prime_I = -I0 * (Rs / (n * Vt)) * expTerm - (Rs / Rsh) - 1.0;

                    I_guess = I_guess - f_I / f_prime_I;
                }

                // 模拟源表底噪
                double noise = (rnd.NextDouble() - 0.5) * 8e-8;

                // 2. 恢复原系统的符号习惯：将算出的正向电流取反，变为系统需要的第四象限负电流
                c[j] = -I_guess + noise;
            }

            return new IVData { Voltage = v, Current = c };
        }
    }
}
