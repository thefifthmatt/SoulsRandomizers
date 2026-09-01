using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Base;
using MsBox.Avalonia.Enums;
using ReactiveUI;
using System.Threading.Tasks;

namespace RandomizerCommon.ViewModels
{
    public class ViewModelBase : ReactiveObject
    {
        // Hacky utilities
        // TODO: Because this can only retrieve the main window, make this a generic interaction after all with a generic view-side utility
        protected Task<ButtonResult> Modal(IMsBox<ButtonResult> dialog)
        {
            if (MainWindow() is Window window)
            {
                return dialog.ShowWindowDialogAsync(window);
            }
            else
            {
                return dialog.ShowAsync();
            }
        }

        protected Task<ButtonResult> ModalError(string message, string? title = null)
        {
            return Modal(MessageBoxManager.GetMessageBoxStandard(
                title ?? "Error",
                message,
                ButtonEnum.Ok,
                Icon.Error,
                WindowStartupLocation.CenterOwner));
        }

        protected static Window? MainWindow() =>
            Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop ? desktop.MainWindow : null;
    }
}
