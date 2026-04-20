using System.Globalization;
using System.Windows.Data;

namespace StabilityPlatForm.HMProject.UI.Converters
{
    public class DeviceEnabledBoolConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && values[0] is string deviceName && values[1] is Dictionary<int, bool> dict)
            {
                var parts = deviceName.Split('-');
                if (parts.Length == 2 && int.TryParse(parts[1].Replace("Device", "").Trim(), out int devNum))
                {
                    if (dict.TryGetValue(devNum, out bool isEnabled))
                    {
                        return isEnabled; // 返回器件是否被启用
                    }
                }
            }
            return true; // 默认启用
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}