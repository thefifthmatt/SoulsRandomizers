using RandomizerCommon.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using static RandomizerCommon.Messages;

namespace RandomizerCommon.Localization
{
    public class AvaloniaMessages
    {
        // To avoid accessing Messages too early, do not use FromText from static fields, but instance fields should be fine
        public static IObservable<string> FromText(Text text)
        {
            Messages messages = Messages.GetInstance();
            return Observable.FromEventPattern(e => messages.CultureChanged += e, e => messages.CultureChanged -= e)
                .Select(_ => messages.Get(text))
                .StartWith(messages.Get(text));
        }

        // Only gets value when culture changes. This can be used with Observable.Merge but should not be used with CombineLatest in most cases.
        public static IObservable<Unit> OnCultureChange()
        {
            Messages messages = Messages.GetInstance();
            return Observable.FromEventPattern(e => messages.CultureChanged += e, e => messages.CultureChanged -= e).Select(_ => Unit.Default);
        }

        // Like FromCultureChange but also gets initial value, which is required to get CombineLatest to produce anything at all.
        public static IObservable<Unit> OnCultureState() => OnCultureChange().StartWith(Unit.Default);

#if DEBUG
        public static List<Type> AvaloniaMessageTypes =
        [
            typeof(DllViewModel), typeof(HelperViewModel), typeof(MergeModViewModel), typeof(OptionsViewModel),
            typeof(ItemPresetViewModel), typeof(ItemPanelViewModel),
            typeof(EnemyPresetViewModel), typeof(EnemyPanelViewModel),
        ];

        public static void AddExplainXamls(ExplainBuilder explain, string dir)
        {
            foreach (string path in Directory.GetFiles(dir, "*.axaml"))
            {
                AddExplainXaml(explain, path);
            }
        }

        public static void AddExplainXaml(ExplainBuilder explain, string file)
        {
            List<string> errors = [];
            // Fully traversing relevant xaml at runtime is basically infeasible so do it statically
            XDocument doc = XDocument.Load(file, LoadOptions.SetLineInfo);
            Regex whitespace = new Regex(@"\s+");
            Regex escape = new Regex(@"^\{\}");
            foreach (XElement elem in doc.Descendants())
            {
                if (elem.Name.LocalName == "Message" && elem.Name.NamespaceName == "using:RandomizerCommon.Localization")
                {
                    if (elem.Attribute("From") != null)
                    {
                        continue;
                    }
                    string? key = elem.Attribute("Key")?.Value;
                    string text = elem.Attribute("Text")?.Value ?? elem.Value ?? "";
                    if (string.IsNullOrWhiteSpace(key))
                    {
                        IXmlLineInfo lineInfo = elem;
                        errors.Add($"No key for message {elem} at {lineInfo.LineNumber}:{lineInfo.LinePosition}");
                        continue;
                    }
                    text = escape.Replace(whitespace.Replace(text, " "), "").Trim();
                    explain.AddMessage(key, text);
                }
            }
            if (errors.Count > 0)
            {
                throw new Exception($"Invalid messages in {file}:\n{string.Join("\n", errors)}");
            }
        }
#endif
    }
}