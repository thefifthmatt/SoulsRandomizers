using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SoulsFormats;
using Newtonsoft.Json;
using System.Drawing;
using System.Drawing.Text;
using System.Text.RegularExpressions;
using SoulsIds;

namespace RandomizerCommon
{
    public class Messages
    {
        [AttributeUsage(AttributeTargets.Field)]
        public class Localize : Attribute
        {
            // This prefix can be used in a dictionary to add a suffix of key.ToString()
            // Actually: don't
            public string Prefix { get; }

            // In the future, we can add per-game selection, but for now it's just Elden Ring.
            public Localize(string prefix = null)
            {
                Prefix = prefix;
            }
        }

        public record Text(string Str, string Name, string PrevName = null)
        {
        }

        // For FMGArg copying
        public static readonly Text IdentityMessage = new Text("{0}", "identity");

        public static Dictionary<T, Text> MapText<T>(string prefix, Dictionary<T, string> strs)
        {
            return strs.ToDictionary(e => e.Key, e => new Text(e.Value, prefix + "_" + e.Key));
        }

        [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
        public class LanguageMessages
        {
            [JsonIgnore]
            public CultureInfo Culture { get; set; }

            [JsonProperty(PropertyName = "language")]
            public string Language { get; set; }

            [JsonProperty(PropertyName = "translators")]
            public string Translators { get; set; }

            [JsonProperty(PropertyName = "comment")]
            public string Comment { get; set; }

            [JsonProperty(PropertyName = "instructions")]
            public string Instructions { get; set; }

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

        // Needed for Avalonia until dependency injection is available across the app, if that's even a good idea
        private static Messages instance;
        public static Messages GetInstance() => instance ?? throw new InvalidDataException("Internal error: messages not initialized");

        public Messages(string distDir, bool loadEmpty = false)
        {
            // Access single-threaded
            instance = this;
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
                    // Other files like .enemy.json are also here. Can reconsider this
                    if (name.Contains('.'))
                    {
                        continue;
                    }
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
        
        // This sets a global static variable, managed through this singleton so UI updates occur properly.
        // CurrentCulture is preferred over an instance variable as it affects low-level formatting methods.
        // When the UI is active, set this from UI thread and handle events in UI thread only.
        public void SetCulture(CultureInfo culture)
        {
            Thread.CurrentThread.CurrentCulture = culture;
            RandomizerOptions.SaveLocale(culture.ToString());
            CultureChanged?.Invoke(null, EventArgs.Empty);
        }
        public event EventHandler CultureChanged;

        // Attempts to loads the stored culture, which must be done before UI framework initialization
        public static void LoadCulture()
        {
            string existing = RandomizerOptions.ReadLocale();
            if (!string.IsNullOrWhiteSpace(existing))
            {
                try
                {
                    CultureInfo initial = CultureInfo.GetCultureInfo(existing, true);
                    Thread.CurrentThread.CurrentCulture = initial;
                }
                catch (Exception e)
                {
#if DEBUG
                    Console.WriteLine($"Ignoring stored culture: {e}");
#endif
                }
            }
        }

        public static readonly string DefaultLanguageName = "English";

        public LanguageMessages GetCurrentLanguage()
        {
            return GetAllLanguageMessages(Thread.CurrentThread.CurrentCulture).FirstOrDefault();
        }

        public string GetCurrentLanguageName()
        {
            return GetCurrentLanguage()?.Language ?? DefaultLanguageName;
        }

        public List<(string, CultureInfo)> GetLanguageList()
        {
            List<(string, CultureInfo)> languages = new() { (DefaultLanguageName, CultureInfo.InvariantCulture) };
            languages.AddRange(AllMessages.Values
                .OrderBy(l => l.Language, StringComparer.InvariantCulture)
                .Select(l => (l.Language, l.Culture)));
            return languages;
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
            if (AllMessages.Count == 0 || culture.ThreeLetterISOLanguageName == "eng" || string.IsNullOrEmpty(text.Name))
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
                // Allow one-level recursion for simplifying UI binding
                if (arg is Text subtext) arg = Get(culture, subtext);
                string argText = arg is IConvertible conv ? conv.ToString(culture) : arg.ToString();
                text = text.Replace("{" + i + "}", argText);
            }
            return text;
        }

        // Automatic FMG stuff
        // The reference to GameData makes it inapplicable outside randomizer.
        // An all-languages FMGDictionary dictionary would simplify things here.
        public void SetFMGEntry(
            GameData game,
            FMGCategory category,
            string fmgName, int id,
            Text text, params object[] args)
        {
            // TODO: Can extend this outside of SetFMGEntry for e.g. spoiler logs
            // This requires having access to FMGs in general, and crossing over between item/menu.
            // However, it should not go into Format, as it still needs the FMG language name key
            object formatArg(object arg, FMGDictionary itemFmgs, FMGDictionary menuFmgs)
            {
                if (arg is FMGArg fmgArg)
                {
                    return fmgArg.Format(itemFmgs, menuFmgs);
                }
                return arg;
            }
            FMGDictionary fmgs = category == FMGCategory.Item ? game.ItemFMGs : game.MenuFMGs;
            object[] langArgs = args.Select(a => formatArg(a, game.ItemFMGs, game.MenuFMGs)).ToArray();
            string str = Format(CultureInfo.InvariantCulture, text.Str, langArgs);
            fmgs[fmgName][id] = str;
            foreach (KeyValuePair<string, FMGDictionary> langFmgs in category == FMGCategory.Item ? game.AllItemFMGs : game.AllMenuFMGs)
            {
                string lang = langFmgs.Key;
                // TODO: Could all languages be treated uniformly?
                if (lang == game.EnglishName) continue;
                game.AllItemFMGs.TryGetValue(lang, out FMGDictionary itemFmgs);
                game.AllMenuFMGs.TryGetValue(lang, out FMGDictionary menuFmgs);
                langArgs = args.Select(a => formatArg(a, itemFmgs, menuFmgs)).ToArray();
                str = Get(lang, text, langArgs);
                langFmgs.Value[fmgName][id] = str;
                // Console.WriteLine($"{langFmgs.Key}[{fmgName}][{id}] = {str}");
            }
        }

        public enum FMGCategory
        {
            Item, Menu
        }

        public class FMGArg
        {
            public FMGCategory Category { get; set; }
            public string Name { get; set; }
            public int ID { get; set; }
            public string BaseText { get; set; }
            // Other possible idea: having a preferred Text to use instead of FMG entry, but that requires a no-fallback mode for Get

            public static FMGArg Menu(string Name, int ID, string BaseText = null) => new FMGArg { Category = FMGCategory.Menu, Name = Name, ID = ID, BaseText = BaseText };
            public static FMGArg Item(string Name, int ID, string BaseText = null) => new FMGArg { Category = FMGCategory.Item, Name = Name, ID = ID, BaseText = BaseText };

            internal string Format(FMGDictionary itemFmgs, FMGDictionary menuFmgs)
            {
                FMGDictionary fmgs = Category == FMGCategory.Item ? itemFmgs : menuFmgs;
                if (fmgs == null || !fmgs.ContainsKey(Name)) throw new Exception($"Error: {Category} FMG archive is missing {Name}");
                FMGX fmg = fmgs[Name];
                string text = fmg[ID];
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = BaseText ?? "";
#if DEBUG
                    throw new Exception($"Error: {Category}.{Name}[{ID}] is missing or blank (English: {BaseText})");
#endif
                }
                return text;
            }
        }

        public static readonly Regex IgnoreFormRe;
        static Messages()
        {
            List<string> ignoreNames = new List<string>
            {
                "EldenForm_EldenForm",
                "EldenForm_warningL", "EldenForm_difficultyL", "EldenForm_difficultyAmtL",
                "EldenForm_presetL", "EldenForm_enemyBetaL",
                "PresetEditForm_multiplyBox", "PresetEditForm_classMultiplyBox", "PresetEditForm_newPoolButton",
                "PresetEditForm_poolDesc", "PresetEditForm_poolTitle",
                "PresetEditForm_classMerge2",
                "PresetEditForm_classCustomHelp",
            };
            List<string> ignoreRegexes = new List<string> { @"^PresetEditForm_custom_" };
            IgnoreFormRe = new Regex(string.Join("|", ignoreRegexes.Concat(ignoreNames.Select(n => $"^{n}$"))));
        }

#if DEBUG
        public class ExplainBuilder
        {
            private LanguageMessages ret;
            private Dictionary<string, LanguageMessage> existing = new();
            private HashSet<(string, string)> names = new();

            public ExplainBuilder()
            {
                string existingFile = @"configs\diste\explain.json";
                if (File.Exists(existingFile))
                {
                    LanguageMessages msgs = JsonConvert.DeserializeObject<LanguageMessages>(File.ReadAllText(existingFile));
                    existing = msgs.Messages.ToDictionary(e => e.Name, e => e);
                }
                ret = new LanguageMessages
                {
                    Language = "Debug",
                    Comment = "This file is only used for copying documentation to other files",
                    Messages = new List<LanguageMessage>(),
                };
            }

            // Automatic form stuff, obsolete soon
            private static readonly HashSet<string> autoTrim = new HashSet<string>
            {
                "EldenForm_itemPage", "EldenForm_enemyPage", "EldenForm_dlcPage",
            };

            public void AddMessage(string name, string text)
            {
                if (!names.Add((name, text)))
                {
                    // Many duplicates exist while migrating to Avalonia, and a few are present in the UI itself
                    return;
                }
                LanguageMessage msg = new LanguageMessage
                {
                    Name = name,
                    Explanation = "TODO",
                    EnglishText = autoTrim.Contains(name) ? text.Trim() : text,
                };
                if (existing.TryGetValue(name, out LanguageMessage pre))
                {
                    if (!string.IsNullOrWhiteSpace(pre.Explanation) && pre.EnglishText == msg.EnglishText)
                    {
                        msg.Explanation = pre.Explanation;
                        existing.Remove(name);
                    }
                }
                ret.Messages.Add(msg);
            }

            public void Write()
            {
                ret.Messages.AddRange(existing.Values);
                ret.Messages = ret.Messages.Where(x => !IgnoreFormRe.IsMatch(x.Name)).OrderBy(x => x.Name).ToList();
                string output = JsonConvert.SerializeObject(ret, Formatting.Indented);
                File.WriteAllText("explain.json", output);
            }
        }

        public static void AddExplainTypes(ExplainBuilder explain, IEnumerable<Type> types)
        {
            foreach (Type type in types)
            {
                AddExplainType(explain, type);
            }
        }

        public static void AddExplainType(ExplainBuilder explain, Type type, object instance = null)
        {
            foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
            {
                if (!field.IsDefined(typeof(Localize))) continue;
                object val;
                if (field.IsStatic)
                {
                    val = field.GetValue(null);
                }
                else
                {
                    val = field.GetValue(instance ?? throw new Exception($"Instance required for localized instance field {type.FullName} {field.Name} {field.FieldType.Name}"));
                    // Disallow this until there is a very good reason
                    throw new InvalidOperationException("Non-static Localize field");
                }
                if (val is Text single)
                {
                    explain.AddMessage(single.Name, single.Str);
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
                    if (string.IsNullOrEmpty(text.Str)) continue;
                    explain.AddMessage(text.Name, text.Str);
                }
            }
        }

        // File maintenance. Needs update flows
        public static void CopyExplanations(ICollection<string> args, GameSpec.FromGame gameType)
        {
            // TODO: Option to merge with previous files
            // If an entry exists, keep it around, but add new english_text versions if needed
            LanguageMessages explain = JsonConvert.DeserializeObject<LanguageMessages>(
                File.ReadAllText(@"configs\diste\explain.json"));
            GameEditor game = new GameEditor(gameType);
            Dictionary<string, List<FMGDictionary>> fmgs = new();
            Regex textRegex = new Regex(@"<([a-zA-Z0-9_]+)=([0-9]+)(:[0-9]+)?>");
            Regex lineRegex = new Regex(@"\n+");
            string withGameText(string text, string locName)
            {
                string fmgLang = locName ?? "engus";
                if (!fmgs.TryGetValue(fmgLang, out List<FMGDictionary> fmgDicts))
                {
                    fmgs[fmgLang] = fmgDicts = new();
                    fmgDicts.Add(game.LoadFmgBnd($@"{game.Spec.GameDir}\msg\{fmgLang}\item_dlc02.msgbnd.dcx"));
                    fmgDicts.Add(game.LoadFmgBnd($@"{game.Spec.GameDir}\msg\{fmgLang}\menu_dlc02.msgbnd.dcx"));
                }
                return textRegex.Replace(text, match =>
                {
                    string name = match.Groups[1].Value;
                    int id = int.Parse(match.Groups[2].Value);
                    string text = null;
                    foreach (FMGDictionary dict in fmgDicts)
                    {
                        if (dict.ContainsKey(name)) text = dict[name][id];
                    }
                    if (string.IsNullOrEmpty(text)) throw new Exception($"Could not find {fmgLang} {name} {id}");
                    text = lineRegex.Replace(text, " ");
                    if (!string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        int len = int.Parse(match.Groups[3].Value.Substring(1));
                        text = text.Substring(0, len) + "...";
                    }
                    if (locName == null) text = $"[[{text}]]";
                    return text;
                });
            }
            LanguageMessages copyMessages(string name, string fmgLang)
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
                    msgs.Instructions = "To translate the mod, translate english_text -> text when the text is empty. See https://docs.google.com/spreadsheets/d/1NfW_qniivBZgwP1O6BupFgcqUl1s2hIUliDuUzcgPAQ/edit for more details. Thank you!";
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
                    if (existing == null)
                    {
                        // Update the file nondestructively if there is a new message or new English text for it
                        existing = new LanguageMessage
                        {
                            Name = msg.Name,
                            EnglishText = msg.EnglishText,
                            Text = name == null ? $"[[{msg.EnglishText}]]" : "",
                        };
                        msgs.Messages.Add(existing);
                    }
                    existing.Explanation = msg.Explanation == null ? "TODO" : withGameText(msg.Explanation, fmgLang);
                }
                msgs.Messages = msgs.Messages.Where(x => !IgnoreFormRe.IsMatch(x.Name)).OrderBy(x => x.Name).ToList();
                return msgs;
            }
            SortedDictionary<string, string> output = new SortedDictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> cultures = new();
            foreach ((string fmgLang, string name) in MiscSetup.Langs)
            {
                if (!cultures.Add(name)) continue;
                CultureInfo culture = new CultureInfo(name, false);
                if (culture.ThreeLetterISOLanguageName == "eng") continue;
                LanguageMessages lang = copyMessages(name, fmgLang);
                if (lang.Language == null)
                {
                    lang.Language = culture.NativeName;
                }
                output[$"{name}.json"] = JsonConvert.SerializeObject(lang, Formatting.Indented);
            }

            LanguageMessages lang2 = copyMessages(null, null);
            if (args.Contains("test"))
            {
                output.Clear();
            }
            string debugText = JsonConvert.SerializeObject(lang2, Formatting.Indented);
            output["kk-KZ.json"] = debugText;
            if (args.Contains("diff"))
            {
                Console.WriteLine($"Writing diffbefore.json and diff.json: {string.Join("  ", output.Keys)}");
                File.WriteAllText("diffbefore.json", string.Join("", output.Keys.Select(name => File.ReadAllText($@"diste\Messages\{name}") + Environment.NewLine)));
                File.WriteAllText("diff.json", string.Join("", output.Values.Select(x => x + Environment.NewLine)));
            }
            else
            {
                foreach (KeyValuePair<string, string> entry in output)
                {
                    string path = $@"diste\Messages\{entry.Key}";
                    Console.WriteLine($"Writing {path}");
                    File.WriteAllText(path, entry.Value);
                }
            }
        }
#endif
    }
}
