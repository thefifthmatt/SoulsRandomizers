using Avalonia;
using Avalonia.Data;
using System;
using System.Linq;

namespace RandomizerCommon.Localization
{
    // This was meant to be a wrapper of IBinding (BindingBase in Avalonia 12) because that cannot appear in axaml as a standalone element normally, but unclear if it works
    public class Param : StyledElement
    {
        public static readonly StyledProperty<IBinding> ValueProperty = AvaloniaProperty.Register<Param, IBinding>(nameof(Value));

        public IBinding Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
    }
}
