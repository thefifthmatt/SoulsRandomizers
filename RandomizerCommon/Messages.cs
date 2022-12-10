using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SoulsIds;
using SoulsFormats;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Text;

namespace RandomizerCommon
{
    public class Messages
    {
        [AttributeUsage(AttributeTargets.Field)]
        public class Localize : Attribute
        {
            // In the future, we can add per-game selection, but for now it's just Elden Ring.
            public Localize() { }
        }

        public class Text
        {
            public string Str { get; set; }
            public string Name { get; set; }
            public Text(string Str, string Name)
            {
                this.Str = Str;
                this.Name = Name;
            }
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class LanguageMessages
        {
            [JsonIgnore]
            public CultureInfo Culture { get; set; }

            [JsonProperty(PropertyName = "language")]
            public string Language { get; set; }

            [JsonProperty(PropertyName = "comment")]
            public string Comment { get; set; }

            [JsonProperty(PropertyName = "translators")]
            public string Translators { get; set; }

            [JsonProperty(PropertyName = "messages")]
            public List<LanguageMessage> Messages { get; set; }
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class LanguageMessage
        {
            [JsonProperty(PropertyName = "name")]
            public string Name { get; set; }

            [JsonProperty(PropertyName = "explanation")]
            public string Explanation { get; set; }

            [JsonProperty(PropertyName = "english_text")]
            public string EnglishText { get; set; }

            [JsonProperty(PropertyName = "text")]
            public string Text { get; set; }
        }

        public Dictionary<CultureInfo, LanguageMessages> AllMessages = new Dictionary<CultureInfo, LanguageMessages>();
        public Dictionary<string, CultureInfo> GameCultures = new Dictionary<string, CultureInfo>();
        public List<string> InitErrors = new List<string>();

        public Messages(string distDir, bool loadEmpty = false)
        {
            foreach (KeyValuePair<string, string> entry in MiscSetup.Langs)
            {
                try
                {
                    GameCultures[entry.Key] = new CultureInfo(entry.Value, false);
                }
                catch (CultureNotFoundException ex)
                {
                    InitErrors.Add(ex.ToString());
                    continue;
                }
            }
            string msgDir = $@"{distDir}\Messages";
            if (distDir == null || !Directory.Exists(msgDir)) return;
            foreach (string path in Directory.GetFiles(msgDir, "*.json"))
            {
                CultureInfo culture;
                try
                {
                    string name = Path.GetFileNameWithoutExtension(path);
                    culture = new CultureInfo(name, false);
                }
                catch (CultureNotFoundException ex)
                {
                    InitErrors.Add(ex.ToString());
                    continue;
                }
                string input = File.ReadAllText(path);
                LanguageMessages msgs;
                try
                {
                    msgs = JsonConvert.DeserializeObject<LanguageMessages>(input);
                }
                catch (JsonException ex)
                {
                    InitErrors.Add(ex.ToString());
                    continue;
                }
                if (!loadEmpty)
                {
                    if (string.IsNullOrWhiteSpace(msgs.Language)
                        || msgs.Language == "Debug"
                        || msgs.Messages == null
                        || msgs.Messages.All(m => string.IsNullOrWhiteSpace(m.Text)))
                    {
                        continue;
                    }
                }
                msgs.Culture = culture;
                AllMessages[culture] = msgs;
            }
        }

        public List<LanguageMessages> GetAllLanguageMessages(CultureInfo culture)
        {
            List<LanguageMessages> ret = new List<LanguageMessages>();
            int iters = 0;
            LanguageMessages messages;
            while (!culture.Equals(CultureInfo.InvariantCulture) && iters++ < 10)
            {
                AllMessages.TryGetValue(culture, out messages);
                if (messages != null && !ret.Contains(messages))
                {
                    ret.Add(messages);
                }
                culture = culture.Parent;
            }
            return ret;
        }

        public string Get(Text text, params object[] args)
        {
            if (text == null) return "";
            return Get(Thread.CurrentThread.CurrentCulture, text, args);
        }

        public string Get(string lang, Text text, params object[] args)
        {
            if (text == null) return "";
            if (lang == null || lang == "engus" || !GameCultures.TryGetValue(lang, out CultureInfo culture))
            {
                return Format(CultureInfo.InvariantCulture, text.Str, args);
            }
            // Console.WriteLine($"Culture {culture} for {lang}. messages: {string.Join(", ", GetAllLanguageMessages(culture).Select(m => m.Language))}.");
            return Get(culture, text, args);
        }

        public string Get(CultureInfo culture, Text text, params object[] args)
        {
            if (text == null) return "";
            if (AllMessages.Count == 0 || culture.ThreeLetterISOLanguageName == "eng")
            {
                return Format(CultureInfo.InvariantCulture, text.Str, args);
            }
            foreach (LanguageMessages messages in GetAllLanguageMessages(culture))
            {
                List<LanguageMessage> msgs =
                    messages.Messages.Where(m => text.Name.Equals(m.Name) && !string.IsNullOrWhiteSpace(m.Text)).ToList();
                if (msgs.Count > 0)
                {
                    // Either use a message with exactly matching English text, or the last one
                    LanguageMessage pref = msgs.Find(m => text.Str.Equals(m.EnglishText));
                    string finalText = pref == null ? msgs.Last().Text : pref.Text;
                    // Use the original culture to format, as opposed to LanguageMessages culture
                    return Format(culture, finalText, args);
                }
            }
            return Format(CultureInfo.InvariantCulture, text.Str, args);
        }

        private string Format(CultureInfo culture, string text, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                object arg = args[i];
                if (arg is FMGArg) throw new Exception($"Internal error: FMGArg can only be used in FMGs currently");
                string argText = arg is IConvertible conv ? conv.ToString(culture) : arg.ToString();
                text = text.Replace("{" + i + "}", argText);
            }
            return text;
        }

        // Automatic form stuff
        private static readonly HashSet<string> ignoreNames = new HashSet<string>
        {
            "EldenForm_EldenForm",
            "EldenForm_warningL", "EldenForm_difficultyL", "EldenForm_difficultyAmtL",
            "EldenForm_presetL", "EldenForm_enemyBetaL",
            "PresetEditForm_multiplyBox", "PresetEditForm_classMultiplyBox", "PresetEditForm_newPoolButton",
            "PresetEditForm_poolDesc", "PresetEditForm_poolTitle",
            "PresetEditForm_classMerge2",
        };
        private static readonly HashSet<string> ignoreReplace = new HashSet<string>
        {
        };
        private static readonly HashSet<string> autoTrim = new HashSet<string>
        {
            "EldenForm_itemPage", "EldenForm_enemyPage",
        };

        // Gets original text. Should only be done once at the start, to preserve English messages.
        public Dictionary<string, string> GetFormText(Form form)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            void setText(string controlName, string text)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    ret[controlName] = text.Replace("\r\n", "\n");
                }
            }
            void recurseForm(Control control)
            {
                setText(control.Name, control.Text);
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        setText(sub.Name, sub.Text);
                    }
                }
            }
            recurseForm(form);
            return ret;
        }

        public void SetFormText(Form form, Dictionary<string, string> original)
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            bool tryReplace(string controlName, out string replace)
            {
                replace = null;
                if (original.TryGetValue(controlName, out string originalText))
                {
                    string locName = $"{form.Name}_{controlName}";
                    if (!ignoreReplace.Contains(locName))
                    {
                        replace = Get(culture, new Text(originalText, locName));
                        return true;
                    }
                }
                return false;
            }
            // Fonts from lowest to highest prioritity
            string defaultFont = "Microsoft Sans Serif";
            List<string> fontOrder = new List<string>();
            // Try to choose fonts aside from MS Gothic
            if (culture.TwoLetterISOLanguageName == "ja")
            {
                fontOrder.AddRange(new[] { "Noto Sans JP", "Yu Gothic UI", "Meiryo UI" });
            }
            else if (culture.TwoLetterISOLanguageName == "zh")
            {
                fontOrder.AddRange(new[] { "Noto Sans SC", "Microsoft YaHei UI" });
            }
            string font = defaultFont;
            FontFamily[] installed = new InstalledFontCollection().Families;
            foreach (string candidate in Enumerable.Reverse(fontOrder))
            {
                if (installed.Any(family => family.Name == candidate))
                {
                    font = candidate;
                    break;
                }
            }
            Font setFont(Font f)
            {
                if (f.Name == font) return f;
                return new Font(font, f.SizeInPoints);
            }
            void recurseForm(Control control)
            {
                control.Font = setFont(control.Font);
                if (tryReplace(control.Name, out string replace))
                {
                    control.Text = replace;
                }
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
                if (control is ToolStrip toolStrip)
                {
                    foreach (ToolStripItem sub in GetItems(toolStrip))
                    {
                        sub.Font = setFont(sub.Font);
                        if (tryReplace(sub.Name, out replace))
                        {
                            sub.Text = replace;
                        }
                    }
                }
            }
            recurseForm(form);
        }

        private static IEnumerable<ToolStripItem> GetItems(ToolStrip strip) =>
            strip.Items.Cast<ToolStripItem>().SelectMany(GetItems);

        // https://stackoverflow.com/questions/15380730/foreach-every-subitem-in-a-menustrip
        private static IEnumerable<ToolStripItem> GetItems(ToolStripItem item)
        {
            if (item is ToolStripMenuItem)
            {
                foreach (ToolStripItem tsi in (item as ToolStripMenuItem).DropDownItems)
                {
                    if (tsi is ToolStripMenuItem)
                    {
                        if ((tsi as ToolStripMenuItem).HasDropDownItems)
                        {
                            foreach (ToolStripItem subItem in GetItems((tsi as ToolStripMenuItem)))
                                yield return subItem;
                        }
                        yield return (tsi as ToolStripMenuItem);
                    }
                    else if (tsi is ToolStripSeparator)
                    {
                        yield return (tsi as ToolStripSeparator);
                    }
                }
            }
            else if (item is ToolStripSeparator)
            {
                yield return (item as ToolStripSeparator);
            }
        }

        // Automatic FMG stuff
        public void SetFMGEntry(
            FMGDictionary fmgs,
            Dictionary<string, FMGDictionary> langsFmgs,
            string fmgName, int id,
            Text text, params object[] args)
        {
            // TODO: Can extend this outside of SetFMGEntry for e.g. spoiler logs
            // This requires having access to FMGs in general, and crossing over between item/menu.
            // However, it should not go into Format, as it still needs the FMG language name key
            object formatArg(object arg, FMGDictionary langFmgs)
            {
                if (arg is FMGArg fmgArg)
                {
                    return fmgArg.Format(langFmgs);
                }
                return arg;
            }
            object[] langArgs = args.Select(a => formatArg(a, fmgs)).ToArray();
            string str = Format(CultureInfo.InvariantCulture, text.Str, langArgs);
            fmgs[fmgName][id] = str;
            foreach (KeyValuePair<string, FMGDictionary> langFmgs in langsFmgs)
            {
                langArgs = args.Select(a => formatArg(a, langFmgs.Value)).ToArray();
                str = Get(langFmgs.Key, text, langArgs);
                langFmgs.Value[fmgName][id] = str;
                // Console.WriteLine($"{langFmgs.Key}[{fmgName}][{id}] = {str}");
            }
        }

        public class FMGArg
        {
            public string FMGName { get; set; }
            public int ID { get; set; }
            public string BaseText { get; set; }
            public string Format(FMGDictionary fmgs)
            {
                if (!fmgs.ContainsKey(FMGName)) throw new Exception($"Error: FMG archive is missing {FMGName}");
                FMG fmg = fmgs[FMGName];
                string text = fmg[ID];
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = BaseText;
#if DEBUG
                    throw new Exception($"Error: {FMGName}[{ID}] is missing or blank (English: {BaseText})");
#endif
                }
                return text;
            }
        }

        // ----------------------------------------------------------------------------------------

        // File maintenance. Needs update flows
        public static void CopyExplanations(ICollection<string> args)
        {
            // TODO: Option to merge with previous files
            // If an entry exists, keep it around, but add new english_text versions if needed
            LanguageMessages explain = JsonConvert.DeserializeObject<LanguageMessages>(
                File.ReadAllText(@"configs\diste\explain.json"));
            LanguageMessages copyMessages(string name)
            {
                LanguageMessages msgs = new LanguageMessages
                {
                    Language = "",
                    Translators = "Your name here",
                    Messages = new List<LanguageMessage>(),
                };
                string existingFile = $@"diste\Messages\{name}.json";
                if (name != null && File.Exists(existingFile))
                {
                    msgs = JsonConvert.DeserializeObject<LanguageMessages>(File.ReadAllText(existingFile));
                    msgs.Messages.RemoveAll(m => m.Text == "");
                }
                if (name == null)
                {
                    msgs.Language = "Debug";
                    msgs.Comment = "This file is unused, only for UI replacement testing";
                }
                foreach (LanguageMessage msg in explain.Messages)
                {
                    LanguageMessage existing = msgs.Messages.Find(m => m.Name == msg.Name && m.EnglishText == msg.EnglishText);
                    if (existing != null) continue;
                    // Update the file nondestructively if there is a new message or new English text for it
                    LanguageMessage copyMsg = new LanguageMessage
                    {
                        Name = msg.Name,
                        Explanation = msg.Explanation ?? "TODO",
                        EnglishText = msg.EnglishText,
                        Text = name == null ? $"[[{msg.EnglishText}]]" : "",
                    };
                    msgs.Messages.Add(copyMsg);
                }
                msgs.Messages = msgs.Messages.Where(x => !ignoreNames.Contains(x.Name)).OrderBy(x => x.Name).ToList();
                return msgs;
            }
            SortedDictionary<string, string> output = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (string name in MiscSetup.Langs.Values.Distinct())
            {
                CultureInfo culture = new CultureInfo(name, false);
                if (culture.ThreeLetterISOLanguageName == "eng") continue;
                LanguageMessages lang = copyMessages(name);
                if (lang.Language == null)
                {
                    lang.Language = culture.NativeName;
                }
                output[$"{name}.json"] = JsonConvert.SerializeObject(lang, Formatting.Indented);
            }

            LanguageMessages lang2 = copyMessages(null);
            string debugText = JsonConvert.SerializeObject(lang2, Formatting.Indented);
            output["kk-KZ.json"] = debugText;
            if (args.Contains("diff"))
            {
                Console.WriteLine($"Writing diff.json: {string.Join("  ", output.Keys)}");
                File.WriteAllText("diff.json", string.Join("", output.Values.Select(x => x + Environment.NewLine)));
            }
            else
            {
                foreach (KeyValuePair<string, string> entry in output)
                {
                    if (args.Contains("test") && entry.Key != "kk-KZ.json") continue;
                    string path = $@"diste\Messages\{entry.Key}";
                    Console.WriteLine($"Writing {path}");
                    File.WriteAllText(path, entry.Value);
                }
            }
        }

        public static void DumpEnglishMessages(List<Form> forms, List<Type> types)
        {
            string existingFile = @"configs\diste\explain.json";
            Dictionary<string, LanguageMessage> existing = new Dictionary<string, LanguageMessage>();
            if (File.Exists(existingFile))
            {
                LanguageMessages msgs = JsonConvert.DeserializeObject<LanguageMessages>(File.ReadAllText(existingFile));
                existing = msgs.Messages.ToDictionary(e => e.Name, e => e);
            }
            LanguageMessages ret = new LanguageMessages
            {
                Language = "Debug",
                Comment = "This file is only used for copying documentation to other files",
                Messages = new List<LanguageMessage>(),
            };
            void addMessage(string name, string text)
            {
                LanguageMessage msg = new LanguageMessage
                {
                    Name = name,
                    Explanation = "TODO",
                    EnglishText = autoTrim.Contains(name) ? text.Trim() : text,
                };
                if (existing.TryGetValue(name, out LanguageMessage pre))
                {
                    if (!string.IsNullOrWhiteSpace(pre.Explanation) && pre.Explanation != "TODO")
                    {
                        msg.Explanation = pre.Explanation;
                    }
                    existing.Remove(name);
                }
                ret.Messages.Add(msg);
            }
            foreach (Form form in forms)
            {
                void setText(string controlName, string text)
                {
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        string name = $"{form.Name}_{controlName}";
                        if (!ignoreNames.Contains(name))
                        {
                            text = text.Replace("\r\n", "\n");
                            // For now, ignoreReplace happens to match options needing trimming
                            if (ignoreReplace.Contains(name))
                            {
                                text = text.Trim();
                            }
                            addMessage(name, text);
                        }
                    }
                }
                void recurseForm(Control control)
                {
                    setText(control.Name, control.Text);
                    foreach (Control sub in control.Controls)
                    {
                        recurseForm(sub);
                    }
                    if (control is ToolStrip toolStrip)
                    {
                        foreach (ToolStripItem sub in GetItems(toolStrip))
                        {
                            setText(sub.Name, sub.Text);
                        }
                    }
                }
                recurseForm(form);
            }
            foreach (Type type in types)
            {
                foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
                {
                    if (!field.IsDefined(typeof(Localize))) continue;
                    object val = field.GetValue(null);
                    if (val is Text single)
                    {
                        addMessage(single.Name, single.Str);
                        continue;
                    }
                    List<Text> texts = null;
                    Type listType = typeof(List<Text>);
                    // https://stackoverflow.com/questions/1043755/c-sharp-generic-list-t-how-to-get-the-type-of-t
                    // https://stackoverflow.com/questions/3843042/invoking-a-method-of-a-generic-class
                    // https://stackoverflow.com/questions/33155101/invoke-tolist-method-using-reflection-at-runtime-in-c-sharp
                    foreach (Type interfaceType in field.FieldType.GetInterfaces())
                    {
                        if (!interfaceType.IsGenericType) continue;
                        if (interfaceType.GetGenericTypeDefinition() == typeof(IDictionary<,>))
                        {
                            Type[] sub = interfaceType.GetGenericArguments();
                            if (sub[1] == typeof(Text))
                            {
                                PropertyInfo valGet = interfaceType.GetProperty("Values");
                                object vals = valGet.GetValue(val);
                                texts = (List<Text>)Activator.CreateInstance(listType, new object[] { vals });
                            }
                            break;
                        }
                        if (interfaceType.GetGenericTypeDefinition() == typeof(ICollection<>))
                        {
                            Type[] sub = interfaceType.GetGenericArguments();
                            if (sub[0] == typeof(Text))
                            {
                                texts = (List<Text>)Activator.CreateInstance(listType, new object[] { val });
                            }
                            break;
                        }
                    }
                    if (texts == null)
                    {
                        throw new Exception($"{type.FullName} {field.Name} {field.FieldType.Name} is not a valid Text type");
                    }
                    foreach (Text text in texts)
                    {
                        addMessage(text.Name, text.Str);
                    }
                }
            }
            ret.Messages.AddRange(existing.Values);
            ret.Messages = ret.Messages.Where(x => !ignoreNames.Contains(x.Name)).OrderBy(x => x.Name).ToList();
            Console.WriteLine(JsonConvert.SerializeObject(ret, Formatting.Indented));
        }
    }
}
