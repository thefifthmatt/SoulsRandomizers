using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Metadata;
using RandomizerCommon;
using System;
using System.Collections.Generic;

namespace RandomizerCommon.Localization;

// Fake TextBlock which can also appear in ContentPresenters.
// This could be a TemplatedControl but it's very handy to style it with :is(TextBlock)
public class Message : TextBlock
{
    protected override Type StyleKeyOverride => typeof(TextBlock);

    public static readonly StyledProperty<string?> KeyProperty =
        AvaloniaProperty.Register<Message, string?>(nameof(Key), defaultBindingMode: BindingMode.OneTime);
    public string? Key
    {
        get => GetValue(KeyProperty);
        set => SetValue(KeyProperty, value);
    }

    public static readonly StyledProperty<Messages.Text?> FromProperty =
        AvaloniaProperty.Register<Message, Messages.Text?>(nameof(Key));
    public Messages.Text? From
    {
        get => GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    // Support one single binding parameter, interpolated from {0}
    public static readonly StyledProperty<object?> ParamProperty = AvaloniaProperty.Register<Message, object?>(nameof(Param));
    public object? Param
    {
        get => GetValue(ParamProperty);
        set => SetValue(ParamProperty, value);
    }

    // Not currently used. Localizing multi-parameter messages requires detecting when any individual param has changed which seems difficult with IList<object>
    public static readonly StyledProperty<IList<Param>> ParamsProperty = AvaloniaProperty.Register<Message, IList<Param>>(nameof(Params));
    public IList<Param> Params
    {
        get => GetValue(ParamsProperty);
        set => SetValue(ParamsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

#if DEBUG
        if (change.Property == FromProperty || change.Property == TextProperty || change.Property == KeyProperty || change.Property == ParamProperty)
        {
            // Console.WriteLine($"For {Key} got {change.Property}: {change.OldValue} -> {change.NewValue}");
        }
#endif

        // Setting Text works as normal until both Key and Text are present, then it always uses From
        // This usually happens on instantiation from xaml
        if ((change.Property == KeyProperty || change.Property == TextProperty) && Key != null && Text != null && From == null)
        {
            From = new Messages.Text(Text, Key);
        }
        // From/Param can change from binding and from the above assignment. Also when From exists, overwrite Text changes
        if (change.Property == FromProperty || change.Property == ParamProperty || (change.Property == TextProperty && From != null))
        {
            UpdateContents();
        }
    }

    protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnAttachedToLogicalTree(e);
        Messages.GetInstance().CultureChanged += CultureChanged;
    }

    protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromLogicalTree(e);
        Messages.GetInstance().CultureChanged -= CultureChanged;
    }

    private void CultureChanged(object? sender, System.EventArgs e)
    {
        UpdateContents();
    }

    private void UpdateContents()
    {
        if (From == null)
        {
            return;
        }
        if (Param == null)
        {
            Text = Messages.GetInstance().Get(From);
        }
        else
        {
            // Support one param here, which is all that's needed in the UI at the moment
            Text = Messages.GetInstance().Get(From, Param);
        }
    }
}
