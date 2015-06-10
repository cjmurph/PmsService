using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Data;

namespace PlexServiceTray.Themes.Metro.Light
{
    internal class CheckBoxTransformConverter:IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is System.Windows.FlowDirection)
            {
                System.Windows.FlowDirection direction = (System.Windows.FlowDirection)value;
                switch (direction)
                {
                    case System.Windows.FlowDirection.LeftToRight:
                        return 1;
                    case System.Windows.FlowDirection.RightToLeft:
                        return -1;
                    default:
                        throw new ArgumentException("Unexpected FlowDirection Value");
                }
            }
            throw new ArgumentException("Expected value of type System.Windows.FlowDirection");
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
