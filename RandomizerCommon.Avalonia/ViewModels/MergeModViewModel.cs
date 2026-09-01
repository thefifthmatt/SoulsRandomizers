using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using ReactiveUI.SourceGenerators;
using System.Threading.Tasks;
using RandomizerCommon;
using RandomizerCommon.ViewModels;
using static RandomizerCommon.Messages;
using RandomizerCommon.Localization;

namespace RandomizerCommon.ViewModels
{
    public partial class MergeModViewModel : ViewModelBase
    {
        public enum MergeType
        {
            Directory,
            Regulation,
            ModEngine2,
        }

        [Reactive]
        private int? _height = null;

        [Reactive]
        private bool _showDirectory = true;
        [Reactive]
        private bool _showRegulation = true;
        [Reactive]
        private bool _showModEngine2 = true;

        [Localize]
        private static readonly Text titleText = new Text(
            "Select mod to merge",
            "MergeModForm_MergeModForm");
        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);

        public MergeModViewModel(params MergeType[] mergeTypes)
        {
            // This could be reactive, but just set here
            ShowDirectory = mergeTypes.Contains(MergeType.Directory);
            ShowRegulation = mergeTypes.Contains(MergeType.Regulation);
            ShowModEngine2 = mergeTypes.Contains(MergeType.ModEngine2);
            // Each element is 30 + 5 spacing, margin is 20 - 5 final spacing
            Height = 15 + 35 * (mergeTypes.Length + 1);
        }

        [ReactiveCommand]
        public async Task<string?> Directory()
        {
            if (MainWindow() is Window window)
            {
                var files = await window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions()
                {
                    Title = $"Select mod directory",
                    AllowMultiple = false,
                });
                if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
                {
                    // TODO: Maybe should explicitly call Close here, to avoid closing without selection
                    return path;
                }
            }
            return null;
        }

        [ReactiveCommand]
        public async Task<string?> Regulation()
        {
            if (MainWindow() is Window window)
            {
                var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = $"Select regulation.bin",
                    FileTypeFilter = [new("regulation.bin") { Patterns = ["regulation.bin"] }, FilePickerFileTypes.All],
                    AllowMultiple = false,
                });
                if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
                {
                    return path;
                }
            }
            return null;
        }

        [ReactiveCommand]
        public async Task<string?> Toml()
        {
            if (MainWindow() is Window window)
            {
                var files = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions()
                {
                    Title = $"Select toml file",
                    FileTypeFilter = [new("Mod Engine toml file") { Patterns = ["*.toml"] }, FilePickerFileTypes.All],
                    AllowMultiple = false,
                });
                if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
                {
                    return path;
                }
            }
            return null;
        }

        [ReactiveCommand]
        public string? Clear()
        {
            return "";
        }
    }
}
