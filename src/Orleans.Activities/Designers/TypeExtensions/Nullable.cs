using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Windows.Markup;

namespace Orleans.Activities.Designers.TypeExtensions
{
    /// <summary>
    /// Makes nullable activity properties and arguments accessible to the designer.
    /// </summary>
    public class Nullable : TypeExtension
    {
        public Nullable()
        { }

        public Nullable(string type)
            : base(type)
        { }

        public Nullable(Type type)
            : base(type)
        { }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var basis = (Type)base.ProvideValue(serviceProvider);
            return typeof(Nullable<>).MakeGenericType(basis);
        }
    }
}
