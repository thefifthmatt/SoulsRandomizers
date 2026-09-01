using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using EldenRingRandomizer.ViewModels;
using RandomizerCommon;
using RandomizerCommon.ViewModels;
using RandomizerCommon.Views;
using ReactiveUI;
using System;
using System.Collections.Immutable;
using System.Reactive;

namespace EldenRingRandomizer.Views
{
    public partial class MainWindow : ReactiveWindow<MainWindowViewModel>
    {
        public MainWindow()
        {
            InitializeComponent();

            if (Design.IsDesignMode) return;

            this.WhenActivated(action => action(RegisterDialog<OptionsWindow, OptionsViewModel, OptionsViewModel.Result?>(ViewModel!.ShowOptionsDialog)));
            // TODO: Convert nonspecific output types to inner result classes
            this.WhenActivated(action => action(RegisterDialog<MergeModWindow, MergeModViewModel, string?>(ViewModel!.ShowMergeModDialog)));
            this.WhenActivated(action => action(RegisterDialog<DllWindow, DllViewModel, ImmutableList<string>?>(ViewModel!.ShowDllDialog)));
            this.WhenActivated(action => action(RegisterDialog<EnemyPresetWindow, EnemyPresetViewModel, EnemyPreset?>(ViewModel!.ShowEnemyPreset)));
            this.WhenActivated(action => action(ViewModel!.ShowHelperWindow.RegisterHandler(DoShowHelperWindow)));
        }

        private IDisposable RegisterDialog<TWindow, TInput, TOutput>(Interaction<TInput, TOutput> interaction) where TWindow : Window, new()
        {
            return interaction.RegisterHandler(async interaction =>
            {
                var dialog = new TWindow();
                dialog.DataContext = interaction.Input;
                var result = await dialog.ShowDialog<TOutput>(this);
                interaction.SetOutput(result);
            });
        }

        // HelperWindow is shown non-modally because it's independent I guess
        private HelperWindow? currentHelperWindow;
        private void DoShowHelperWindow(IInteractionContext<Unit, Unit?> interaction)
        {
            if (currentHelperWindow != null)
            {
                currentHelperWindow.Activate();
                interaction.SetOutput(null);
                return;
            }
            // ViewModel is a bit expensive to set up so do it after prior check
            currentHelperWindow = new HelperWindow()
            {
                DataContext = new HelperViewModel(MainWindowViewModel.HelperConfigPath, MainWindowViewModel.HelperLogPath)
            };
            currentHelperWindow.Closed += (e, sender) => currentHelperWindow = null;
            currentHelperWindow.Show(this);
            interaction.SetOutput(null);
        }

        public bool SavePosition { get; set; } = true;

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            if (SavePosition)
            {
                WindowStorage.Restore(this);
            }
            // Async
            Dispatcher.UIThread.Post(async () => await ViewModel!.StartView());
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            base.OnClosing(e);
            if (SavePosition)
            {
                WindowStorage.Save(this);
            }
        }
    }
}