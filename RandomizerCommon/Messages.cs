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
using SoulsFormats;
using Newtonsoft.Json;

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
            return Get(culture, text);
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
                string argText = args[i] is IConvertible conv ? conv.ToString(culture) : args[i].ToString();
                text = text.Replace("{" + i + "}", argText);
            }
            return text;
        }

        // Automatic form stuff
        private static readonly HashSet<string> ignoreNames = new HashSet<string>
        {
            "EldenForm_EldenForm", "EldenForm_itemPage", "EldenForm_enemyPage",
            "EldenForm_warningL", "EldenForm_difficultyL", "EldenForm_difficultyAmtL",
            "EldenForm_presetL",
        };

        // Gets original text. Should only be done once at the start, to preserve English messages.
        public Dictionary<string, string> GetFormText(Form form)
        {
            Dictionary<string, string> ret = new Dictionary<string, string>();
            void recurseForm(Control control)
            {
                if (!string.IsNullOrWhiteSpace(control.Text))
                {
                    ret[control.Name] = control.Text.Replace("\r\n", "\n");
                }
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
            }
            recurseForm(form);
            return ret;
        }

        public void SetFormText(Form form, Dictionary<string, string> original)
        {
            CultureInfo culture = Thread.CurrentThread.CurrentCulture;
            void recurseForm(Control control)
            {
                if (original.TryGetValue(control.Name, out string originalText))
                {
                    string locName = $"{form.Name}_{control.Name}";
                    control.Text = Get(culture, new Text(originalText, locName));
                }
                foreach (Control sub in control.Controls)
                {
                    recurseForm(sub);
                }
            }
            recurseForm(form);
        }

        // Automatic FMG stuff
        public void SetFMGEntry(
            Dictionary<string, FMG> fmgs,
            Dictionary<string, Dictionary<string, FMG>> langsFmgs,
            string fmgName, int id,
            Text text, params object[] args)
        {
            string str = Format(CultureInfo.InvariantCulture, text.Str, args);
            fmgs[fmgName][id] = str;
            foreach (KeyValuePair<string, Dictionary<string, FMG>> langFmgs in langsFmgs)
            {
                string lang = langFmgs.Key;
                str = Get(lang, text, args);
                langFmgs.Value[fmgName][id] = str;
            }
        }

        // File maintenance. Needs update flows
        public static void CopyExplanations()
        {
            // TODO: Option to merge with previous files
            // If an entry exists, keep it around, but add new english_text versions if needed
            LanguageMessages explain = JsonConvert.DeserializeObject<LanguageMessages>(
                File.ReadAllText(@"configs\diste\explain.json"));
            LanguageMessages copyMessages(bool debug)
            {
                LanguageMessages copy = new LanguageMessages
                {
                    Language = "",
                    Translators = "Your name here",
                    Messages = new List<LanguageMessage>(),
                };
                if (debug)
                {
                    copy.Language = "Debug";
                    copy.Comment = "This file is unused, only for UI replacement testing";
                }
                foreach (LanguageMessage msg in explain.Messages)
                {
                    LanguageMessage copyMsg = new LanguageMessage
                    {
                        Name = msg.Name,
                        Explanation = msg.Explanation ?? "TODO",
                        EnglishText = msg.EnglishText,
                        Text = debug ? $"[[{msg.EnglishText}]]" : "",
                    };
                    copy.Messages.Add(copyMsg);
                }
                return copy;
            }
            foreach (string name in MiscSetup.Langs.Values.Distinct())
            {
                CultureInfo culture = new CultureInfo(name, false);
                if (culture.ThreeLetterISOLanguageName == "eng") continue;
                LanguageMessages lang = copyMessages(false);
                lang.Language = culture.NativeName;
                File.WriteAllText($@"diste\Messages\{name}.json", JsonConvert.SerializeObject(lang, Formatting.Indented));
            }
            LanguageMessages lang2 = copyMessages(true);
            File.WriteAllText($@"diste\Messages\kk-KZ.json", JsonConvert.SerializeObject(lang2, Formatting.Indented));
        }

        public static void DumpEnglishMessages(List<Form> forms, List<Type> types)
        {
            LanguageMessages ret = new LanguageMessages
            {
                Language = "Debug",
                Comment = "This file is only used for copying documentation to other files",
                Messages = new List<LanguageMessage>(),
            };
            foreach (Form form in forms)
            {
                void recurseForm(Control control)
                {
                    if (!string.IsNullOrWhiteSpace(control.Text))
                    {
                        string name = $"{form.Name}_{control.Name}";
                        if (!ignoreNames.Contains(name))
                        {
                            ret.Messages.Add(new LanguageMessage
                            {
                                Name = name,
                                Explanation = "TODO",
                                EnglishText = control.Text.Replace("\r\n", "\n"),
                            });
                        }
                    }
                    foreach (Control sub in control.Controls)
                    {
                        recurseForm(sub);
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
                    if (!(val is Text text)) throw new Exception($"{type.FullName} {field.Name} is not a Text");
                    ret.Messages.Add(new LanguageMessage
                    {
                        Name = text.Name,
                        Explanation = "TODO",
                        EnglishText = text.Str,
                    });
                }
            }
            ret.Messages = ret.Messages.Where(x => !ignoreNames.Contains(x.Name)).OrderBy(x => x.Name).ToList();
            Console.WriteLine(JsonConvert.SerializeObject(ret, Formatting.Indented));
        }
    }
}
