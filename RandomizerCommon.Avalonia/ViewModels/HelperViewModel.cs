using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using Avalonia.Threading;
using DynamicData;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;
using RandomizerCommon.Localization;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.IO;
using static RandomizerCommon.HelperOptions;
using static RandomizerCommon.Messages;

namespace RandomizerCommon.ViewModels
{
    public partial class HelperViewModel : ViewModelBase, IDisposable
    {
        private readonly string configPath;
        private readonly string logPath;
        private readonly FileSystemWatcher watcher;
        private readonly HelperOptions helperOptions;

        [Reactive]
        private string? _activeTime = null;
        [Reactive]
        private AvaloniaList<OptionState> _options = new();

        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);

        [Localize]
        private static readonly Text titleText = new Text("Configure RandomizerHelper.dll", "HelperForm_HelperForm");

        public HelperViewModel(string configPath, string logPath)
        {
            this.configPath = configPath;
            this.logPath = logPath;
            helperOptions = new HelperOptions(configPath);
            watcher = MakeFileSystemWatcher(logPath);

            Dictionary<string, int> indents = new();
            foreach (Option opt in OptionList)
            {
                // Amount also used by indent style
                int indent = opt.Parent != null && indents.TryGetValue(opt.Parent, out int ival) ? ival + 22 : 0;
                indents[opt.Name] = indent;
                OptionState state;
                if (opt.Type == OptionType.Bool)
                {
                    state = new BoolOptionState(OptionNames[opt.Name], opt, indent, helperOptions.GetBool(opt));
                }
                else if (opt.Type == OptionType.Int)
                {
                    state = new IntOptionState(OptionNames[opt.Name], opt, indent, helperOptions.GetInt(opt));
                }
                else throw new Exception($"Internal error: Unexpected RandomizerHelper option type {opt.Type}");
                Options.Add(state);
            }
            UpdateEnabled();
            UpdateActivity();

            Options
                .AsObservableChangeSet()
                .WhenAnyPropertyChanged(nameof(BoolOptionState.Value), nameof(IntOptionState.Value))
                .Subscribe(state =>
                {
                    if (state is null) return;
                    Option opt = state.Info;
                    if (state is BoolOptionState bstate)
                    {
                        helperOptions.Set(state.Info, bstate.Value);
                        UpdateEnabled();
                    }
                    else if (state is IntOptionState istate && istate.Value is not null)
                    {
                        helperOptions.Set(state.Info, istate.Value);
                    }
                });
        }

        public abstract partial class OptionState : ReactiveObject
        {
            [Reactive]
            private bool _enabled = true;

            // Immutable
            public Text Text { get; }
            public Option Info { get; }
            public Thickness Margin { get; }

            public OptionState(Text text, Option info, int indent)
            {
                Text = text;
                Info = info;
                Margin = new Thickness(indent, 0, 0, 0);
            }
        }

        // Abuse inheritance for DataTemplates
        public partial class IntOptionState(Text text, Option info, int indent, int init) : OptionState(text, info, indent)
        {
            [Reactive]
            private int? _value = init;
        }

        public partial class BoolOptionState(Text text, Option info, int indent, bool init) : OptionState(text, info, indent)
        {
            [Reactive]
            private bool _value = init;
        }

        [ReactiveCommand]
        public void Ok()
        {
            // No model changes, trigger close in HelperWindow
        }

        [ReactiveCommand]
        public async void Reset()
        {
            Messages messages = Messages.GetInstance();
            ButtonResult result = await Modal(MessageBoxManager.GetMessageBoxStandard(
                messages.Get(FormText.ConfirmTitleText),
                messages.Get(FormText.ConfirmResetText),
                // Note owner is MainWindow if non-modal
                ButtonEnum.OkCancel, Icon.Question, WindowStartupLocation.CenterOwner));
            if (result != ButtonResult.Ok)
            {
                return;
            }
            // This may cause a bunch of file saves but that's unavoidable with the ini API
            foreach (OptionState state in Options)
            {
                if (state is BoolOptionState bstate)
                {
                    bstate.Value = helperOptions.GetDefaultBool(state.Info);
                }
                else if (state is IntOptionState istate)
                {
                    istate.Value = helperOptions.GetDefaultInt(state.Info);
                }
            }
        }

        private void UpdateEnabled()
        {
            HashSet<string> disabled = new();
            // Rely on nodes being sorted by parent
            foreach (OptionState state in Options)
            {
                Option opt = state.Info;
                // Only checkboxes can disable other options
                bool checkDisabled = state is BoolOptionState bstate && !bstate.Value;
                bool parentDisabled = opt.Parent != null && disabled.Contains(opt.Parent);
                state.Enabled = !parentDisabled;
                if (checkDisabled || parentDisabled)
                {
                    disabled.Add(opt.Name);
                }
            }
        }

        private FileSystemWatcher MakeFileSystemWatcher(string filename)
        {
            // Required to exist, should exist if this form was created
            string dir = Path.GetDirectoryName(filename)!;
            Directory.CreateDirectory(dir);
            FileSystemWatcher watcher = new FileSystemWatcher(dir, "*.txt");
            watcher.Created += OnFileChanged;
            watcher.Deleted += OnFileChanged;
            watcher.Renamed += OnFileChanged;
            watcher.Changed += OnFileChanged;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            Dispatcher.UIThread.Post(UpdateActivity);
        }

        private void UpdateActivity()
        {
            FileInfo info = new FileInfo(logPath);
            if (!info.Exists)
            {
                ActiveTime = null;
            }
            else
            {
                ActiveTime = info.LastWriteTime.ToString("F");
            }
        }

        public void Dispose()
        {
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        }
    }
}
