using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Styling;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon.Views
{
    public partial class PopulateListBox : AutoCompleteBox
    {
        protected override Type StyleKeyOverride => typeof(AutoCompleteBox);

        public PopulateListBox()
        {
            TextSelector = (searchText, selectedItem) => searchText;
        }

        static PopulateListBox()
        {
            InnerRightContentProperty.Changed.AddClassHandler<PopulateListBox>((x, e) => x.OnInnerRightContentPropertyChanged(e));
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            AddHandler(PointerPressedEvent, Box_PointerPressed, RoutingStrategies.Tunnel);
            AddHandler(PointerReleasedEvent, Box_PointerReleased, RoutingStrategies.Tunnel);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            RemoveHandler(PointerPressedEvent, Box_PointerPressed);
            RemoveHandler(PointerReleasedEvent, Box_PointerReleased);
        }

        private void OnInnerRightContentPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            // If clear button exists, add handler for it. Anything more fancy should probably use a ControlTemplate
            if (e.OldValue is Button oldButton)
            {
                oldButton.Click -= ClearText_Click;
            }
            if (e.NewValue is Button newButton)
            {
                newButton.Click += ClearText_Click;
            }
        }

        private void ClearText_Click(object? sender, RoutedEventArgs e)
        {
            Text = "";
        }

        // Random interactions to improve repeatedly adding things
        // At some point, may need to roll our own AutoCompleteBox to fix these things properly
        private void Box_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsDropDownOpen)
            {
                IsDropDownOpen = true;
            }
        }

        private void Box_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            // Nothing currently
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);
            IsDropDownOpen = true;
        }

        private bool _ctrlDown = false;
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!e.Handled && e.Key == Key.Return && Text == "" && IsDropDownOpen)
            {
                IsDropDownOpen = true;
            }
            if ((e.KeyModifiers & KeyModifiers.Control) != KeyModifiers.None)
            {
                _ctrlDown = true;
            }
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);
            if (_ctrlDown && (e.KeyModifiers & KeyModifiers.Control) == KeyModifiers.None)
            {
                _ctrlDown = false;
            }
        }

        protected override void OnDropDownClosing(CancelEventArgs e)
        {
            // Console.WriteLine($"Search [{SearchText}] Text [{Text}] Item [{SelectedItem}], {_ctrlDown}");
            base.OnDropDownClosing(e);
            // Ideally we can keep the popup open when using a modifier key (setting Cancel doesn't allow this) and keep the search text (custom TextSelector doesn't allow this).
            // Popup behavior and incomprehensible focus states make this extremely difficult so like whatever.
            if (SelectedItem != null)
            {
                // Only trigger modifications when the dropdown closes with a valid selection
                ChooseItem?.Invoke(this, new ChooseItemEventArgs(SelectedItem));
            }
            if (_ctrlDown)
            {
                e.Cancel = true;
            }
        }

        protected override void OnDropDownClosed(EventArgs e)
        {
            // By this point it's too late to change any behavior
            base.OnDropDownClosed(e);
        }

        public event EventHandler<ChooseItemEventArgs>? ChooseItem;

        public class ChooseItemEventArgs : EventArgs
        {
            public object Item { get; }

            public ChooseItemEventArgs(object item)
            {
                Item = item;
            }
        }
    }
}
