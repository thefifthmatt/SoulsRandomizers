using RandomizerCommon.Localization;
using RandomizerCommon.ViewModels;
using ReactiveUI;
using ReactiveUI.SourceGenerators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static RandomizerCommon.Messages;

namespace RandomizerCommon.ViewModels
{
    public partial class OptionsViewModel : ViewModelBase
    {
        [Reactive]
        private string _text = "";
        [Reactive]
        private string? _currentVersion;
        public IObservable<string> Title { get; } = AvaloniaMessages.FromText(titleText);

        [Localize]
        private static readonly Text titleText = new Text(
            "Set options string",
            "OptionsForm_OptionsForm");
        [Localize]
        private static readonly Text errorNoOptions = new Text(
            "Invalid options: must contain options",
            "OptionsForm_errorNoOptions");
        [Localize]
        private static readonly Text errorNoVersion = new Text(
            "Invalid options: must contain a version number",
            "OptionsForm_errorNoVersion");

        [ReactiveCommand]
        public Result Ok()
        {
            string prefix = "Options and seed: ";
            string result = Text.Trim();
            string? version = null;
            if (result.StartsWith(prefix))
            {
                result = result.Substring(prefix.Length);
            }
            if (result == "")
            {
                return new Result { };
            }
            if (uint.TryParse(result, out _))
            {
                return new Result { Error = Messages.GetInstance().Get(errorNoOptions) };
            }
            Match match = Regex.Match(result, @"\bv\d+\b");
            if (!match.Success)
            {
                return new Result { Error = Messages.GetInstance().Get(errorNoVersion) };
            }
            return new Result { Options = result, Version = match.Value };
        }

        public class Result
        {
            public string? Options { get; set; }
            public string? Error { get; set; }
            public string? Version { get; set; }
        }
    }
}
