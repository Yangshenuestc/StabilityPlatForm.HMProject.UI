using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StabilityPlatForm.HMProject.UI.Converters
{
    public class T80BadgeVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // 确保传入了两个值：器件名称(string) 和 预警状态字典(Dictionary)
            if (values != null && values.Length == 2 &&
                values[0] is string deviceName &&
                values[1] is Dictionary<string, bool> states)
            {
                // 如果字典中存在该器件，且状态为 true，则显示角标
                if (states.TryGetValue(deviceName, out bool isT80) && isT80)
                {
                    return Visibility.Visible;
                }
            }
            return Visibility.Collapsed;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}