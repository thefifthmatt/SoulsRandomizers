using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using DynamicData;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using RandomizerCommon;
using static RandomizerCommon.Messages;
using RandomizerCommon.Localization;

namespace RandomizerCommon.ViewModels
{
    public partial class DllViewModel : ViewModelBase
    {
        private readonly string? RequireDll;
        private ImmutableList<string> SavedDlls { get; set; }
        public ImmutableList<string> ResultDlls { get; private set; }

        [Reactive]
        private ObservableCollection<string> _dlls;
        [Reactive]
        private string? _item;
        [Reactive]
        private int _index = -1;
        [Reactive]
        private bool _canRemove;
        // This text is not localized currently and probably should be a required initial parameter moved to individual randomizers
        [Reactive]
        private Text _launchText = new("These dll mods are automatically loaded when using the launcher, in the order below.", "DllForm_infoL");
        [Localize]
        private static readonly Text titleText = new("Dll mods", "DllForm_DllForm");
        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);

        public DllViewModel(ImmutableList<string> dlls, string? requireDll = null)
        {
            SavedDlls = dlls;
            // Add..., Remove, OK
            Dlls = new(dlls);
            // Just for preventing removal in UI, actual validation should happen elsewhere
            RequireDll = requireDll;
            this.WhenAnyValue(x => x.Item)
                .Subscribe(_ =>
                {
                    CanRemove = Item != null && (RequireDll == null || !RequireDll.Equals(Item, StringComparison.OrdinalIgnoreCase));
                });
        }

        private bool Contains(string dll)
        {
            return Dlls.Any(f => f.Equals(dll, StringComparison.OrdinalIgnoreCase));
        }

        public bool IsModified()
        {
            return !Enumerable.SequenceEqual(SavedDlls, Dlls);
        }

        private static readonly FilePickerFileType DllType = new("dll file") { Patterns = ["*.dll"] };
        [ReactiveCommand]
        public async void Add()
        {
            if (MainWindow() is not Window window)
            {
                return;
            }
            var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
            {
                Title = $"Select dll",
                FileTypeFilter = [DllType, FilePickerFileTypes.All],
                AllowMultiple = true,
            });
            List<string> locs = files.Select(f => f.TryGetLocalPath()).Where(f => f != null).ToList();
            if (locs.Count > 0)
            {
                List<string> newLocs = locs.Where(f => !Contains(f)).ToList();
                if (newLocs.Count > 0)
                {
                    Dlls.AddOrInsertRange(newLocs, 0);
                }
                Item = newLocs.FirstOrDefault() ?? locs.First();
            }
        }

        [ReactiveCommand]
        public void Remove()
        {
            if (Item is string item)
            {
                int itemIndex = Dlls.IndexOf(item);
                if (itemIndex == -1)
                {
                    return;
                }
                if (RequireDll != null && RequireDll.Equals(Item, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                Dlls.RemoveAt(itemIndex);
                if (Dlls.Count > 0)
                {
                    int index = Math.Min(itemIndex, Dlls.Count - 1);
                    Item = Dlls[index];
                }
                else
                {
                    Item = null;
                }
            }
        }

        [ReactiveCommand]
        public void Ok()
        {
            SavedDlls = ResultDlls = ImmutableList.CreateRange(Dlls);
        }
    }
}
