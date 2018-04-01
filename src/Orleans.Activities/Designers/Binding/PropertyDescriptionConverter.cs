using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Activities.Presentation.Model;
using System.ComponentModel;
using System.Windows.Data;

namespace Orleans.Activities.Designers.Binding
{
    /// <summary>
    /// Makes DescriptionAttribute on activity properties and arguments available as tooltip in the designer.
    /// </summary>
    public sealed class PropertyDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var modelItem = value as ModelItem;
            if (modelItem == null)
                return System.Windows.Data.Binding.DoNothing;

            AttributeCollection attributes;
            if (parameter != null)
            {
                var modelProperty = modelItem.Properties[parameter.ToString()];
                if (modelProperty == null)
                    return System.Windows.Data.Binding.DoNothing;
                attributes = modelProperty.Attributes;
            }
            else
                attributes = modelItem.Source.Attributes;

            var descriptionAttribute = attributes[typeof(DescriptionAttribute)] as DescriptionAttribute;
            if (descriptionAttribute == null)
                return System.Windows.Data.Binding.DoNothing;
            
            return descriptionAttribute.Description;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
