using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using EldenRingRandomizer.ViewModels;
using EldenRingRandomizer.Views;
using RandomizerCommon;
using RandomizerCommon.Localization;
using ReactiveUI;
using System;
using System.Linq;
using System.Reactive;
using static SoulsIds.GameSpec;

namespace EldenRingRandomizer
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop && desktop.Args != null)
            {
                if (CommandRunner.Run(desktop.Args, FromGame.ER))
                {
                    // TODO: Run in special modes once those exist
                    Environment.Exit(0);
                }
                // Some awkwardness: Initialized here for singleton, but should be done after CommandRunner
                Messages messages = new Messages("diste", desktop.Args.Contains("loadempty"));
#if DEBUG
                if (desktop.Args.Contains("dumpmessages"))
                {
                    DumpMessages();
                    Environment.Exit(0);
                }
#endif
                desktop.MainWindow = new MainWindow()
                {
                    DataContext = new MainWindowViewModel(),
#if DEBUG
                    SavePosition = false,
#endif
                };
            }
            else
            {
                throw new NotSupportedException($"Invalid {ApplicationLifetime}");
            }

            Dispatcher.UIThread.UnhandledException += OnUnhandledException;
            RxApp.DefaultExceptionHandler = Observer.Create<Exception>(HandleException);

            base.OnFrameworkInitializationCompleted();
        }
        
        private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            HandleException(e.Exception);
        }

        public static void HandleException(Exception ex)
        {
            // TODO: Show dialog without spamming it (only needs to be done for RxApp dispatcher?)
            Console.WriteLine($"Unhandled exception: {ex}");
        }

#if DEBUG
        public void DumpMessages()
        {
            Messages.ExplainBuilder explain = new();
            Messages.AddExplainTypes(
                explain,
                Randomizer.CommonMessageTypes
#if WINFORMS
                    .Concat(FormMessages.WinFormsMessageTypes)
#endif
                    .Concat(AvaloniaMessages.AvaloniaMessageTypes)
                    .Concat([typeof(MainWindowViewModel)]));
            AvaloniaMessages.AddExplainXamls(explain, "EldenRingRandomizer.Avalonia/Views");
            AvaloniaMessages.AddExplainXamls(explain, "RandomizerCommon.Avalonia/Views");
#if WINFORMS
            FormMessages.AddExplainForms(explain, new MainWindowViewModel().GetDebugEnemyPresetForm(), new HelperForm(new Messages(null)));
#endif
            explain.Write();
        }
#endif
        }
}