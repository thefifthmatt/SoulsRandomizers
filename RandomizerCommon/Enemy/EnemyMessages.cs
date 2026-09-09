using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using SoulsIds;
using static RandomizerCommon.EnemyAnnotations;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Text.Encodings.Web;
using System.Text;

namespace RandomizerCommon
{
    public static class EnemyMessages
    {
        public class EnemyNamesConfig
        {
            [JsonPropertyName("instructions")]
            public string Instructions { get; set; } = "https://docs.google.com/document/d/1KTB5vIQIkYo65fMJr3pvKbL1dovn8cB2-CD_02YGDZI/edit";
            [JsonPropertyName("status")]
            public string Status { get; set; }
            [JsonPropertyName("capitalize_first_letter_in_custom_name")]
            public bool CapitalizeCustomName { get; set; }
            [JsonPropertyName("boss_names")]
            public List<NameTemplatesMessage> BossNames { get; set; } = new();
            [JsonPropertyName("special_replacements")]
            public List<CustomCleverName> SpecialReplacements { get; set; } = new();
            [JsonPropertyName("enemy_config_names")]
            public List<EnemyPresetName> EnemyConfigNames { get; set; } = new();

            [JsonIgnore]
            public CustomCleverNames CleverNames { get; set; }
        }

        // Slightly awkward interface in advance of proper integration
        public static bool TryReadCurrentConfig(GameData game, string lang, out string gameLang, out EnemyNamesConfig config)
        {
            gameLang = MiscSetup.Langs.Where(e => e.Value == lang).Select(e => e.Key).FirstOrDefault();
            string configPath = $"{game.Dir}/Messages/{lang}.enemy.json";
            if (gameLang != null && File.Exists(configPath))
            {
                try
                {
                    config = JsonSerializer.Deserialize<EnemyNamesConfig>(File.ReadAllText(configPath));
                    Console.WriteLine($"Using enemy names config from {configPath}");
                    Console.WriteLine();
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Couldn't read {configPath}: {ex}");
                }
            }
            config = null;
            return false;
        }

        public class EnemyPresetName
        {
            [JsonPropertyName("english_text")]
            public string EnglishText { get; set; }
            [JsonPropertyName("text")]
            public string Text { get; set; }
            [JsonPropertyName("info")]
            public string Info { get; set; }
        }

        public class NameTemplatesMessage
        {
            [JsonPropertyName("base")]
            public NameTemplates Base { get; set; }
            [JsonPropertyName("translated")]
            public NameTemplates Translated { get; set; }
            [JsonPropertyName("other_translated")]
            public List<NameTemplates> OtherTranslated { get; set; } = new();
            [JsonPropertyName("enemy_config_names")]
            public List<EnemyPresetName> EnemyConfigNames { get; set; } = new();
            // Just for preview, for the time being
            internal List<NameTemplates> AllBase() => new() { Base };
            internal List<NameTemplates> AllTranslated() => new[] { Translated }.Concat(OtherTranslated ?? new()).ToList();

            public bool IsFilledIn() => Translated != null && Translated.IsFilledIn();
        }

#if DEV
        internal static void Operation(GameData game, EnemyAnnotations ann, RandomizerOptions opt)
        {
            List<string> langs = MiscSetup.Langs.Keys.Except(new[] { "engus" }).ToList();
            if (opt.GetStr("lang", out string optLang))
            {
                langs = new() { optLang };
            }
            foreach (string lang in langs)
            {
                string langFile = $@"diste\Messages\{MiscSetup.Langs[lang]}.enemy.json";
                EnemyNamesConfig exist = JsonSerializer.Deserialize<EnemyNamesConfig>(File.ReadAllText(langFile));
                Console.WriteLine("// -- " + lang);
                if (opt["print"])
                {
                    WriteConfig(lang, game, ann, opt, Console.Out, exist);
                }
                else if (opt["write"])
                {
                    string outFile = opt["overwrite"] ? langFile : "diff.json";
                    Console.WriteLine($"Writing {outFile}");
                    using (TextWriter w = File.CreateText(outFile))
                    {
                        WriteConfig(lang, game, ann, opt, w, exist);
                    }
                }
                else if (opt["validate"])
                {
                    string input = opt["example"] ? "Godskin Noble" : "*";
                    string result = Validate(langFile, ann, input, opt["english"]);
                    Console.WriteLine(result);
                }
                else if (opt["generate"])
                {
                    break;
                }
            }
        }
#endif

        // Issues fields are null if not present, empty if no issues, non-empty if issues
        private record ValidationResult(string Key, string OtherKey, List<string> SourceIssues, List<string> TargetIssues)
        {
            public bool IsComplete => SourceIssues != null && TargetIssues != null;
            public bool HasIssues => (SourceIssues != null && SourceIssues.Count > 0) || (TargetIssues != null && TargetIssues.Count > 0);
            public IEnumerable<string> AllIssues => Enumerable.Concat(SourceIssues ?? Enumerable.Empty<string>(), TargetIssues ?? Enumerable.Empty<string>());
            public bool IsSourceValid => SourceIssues != null && SourceIssues.Count == 0;
            public bool IsTargetValid => TargetIssues != null && TargetIssues.Count == 0;
        }

        public static string Validate(string path, EnemyAnnotations ann, string input, bool englishMode)
        {
            if (!File.Exists(path))
            {
                return $"Error: File not found: {path}";
            }
            string fileName = Path.GetFileName(path);
            string suffix = ".enemy.json";
            if (!fileName.EndsWith(suffix))
            {
                return $"Error: File name does not end with {suffix}: {fileName}";
            }
            string cultureLang = fileName.Substring(0, fileName.Length - suffix.Length);
            string lang = MiscSetup.Langs.Where(e => e.Value == cultureLang).FirstOrDefault().Key;
            if (lang == null)
            {
                return $"Error: Unrecognized language '{cultureLang}' in {fileName}";
            }
            EnemyNamesConfig config;
            try
            {
                config = JsonSerializer.Deserialize<EnemyNamesConfig>(File.ReadAllText(path));
            }
            catch (Exception ex)
            {
                return $"Parse error: {ex}";
            }

            // Get basic info from enemy annotations
            // Get all English custom name keys, mainly to check they haven't been accidentally modified
            // Exclude HostileNPC for now for stability (mainly included for other mods), maybe transplant them later
            SortedSet<string> englishKeys = new(ann.Enemies
                .Where(i => i.Important?.Names?.Key != null && i.Class != EnemyClass.HostileNPC)
                .Select(i => i.Important.Names.Key));
            SortedSet<string> presetNames = new() { "Patches" };
            foreach (EnemyCategory parent in ann.AllCategories)
            {
                if (parent.Classes != null || parent.Contains != null) continue;
                presetNames.Add(parent.Name);
                parent.Instances?.ForEach(cat => presetNames.Add(cat.Name));
                parent.Partitions?.ForEach(cat => presetNames.Add(cat.Name));
            }

            // Do basic validation and status gathering, and gather custom keys
            // No input: Show overall status and all warnings
            // Key input: Show overall status, specific warning, and all combined names
            // * input: Same as no input, but also validate all custom names (maybe do this by default?)
            List<ValidationResult> results = new();

            // Map from English config names to translated, null if not present
            List<(string, string)> translatedKeys = new();
            List<(string, string)> translatedConfigNames = new();
            List<NameTemplatesMessage> sources = new();
            List<NameTemplatesMessage> targets = new();
            static string emptyToNull(string s) => string.IsNullOrEmpty(s) ? null : s;
            static bool namesFilledIn(params string[] names) => names.All(n => n == null || n != "");

            void countConfigName(EnemyPresetName name)
            {
                translatedConfigNames.Add((emptyToNull(name?.EnglishText), emptyToNull(name?.Text)));
            }
            void validate(List<string> issues, bool cond, string err)
            {
                if (!cond)
                {
                    issues.Add(err);
                }
            }
            config.BossNames.ForEach(boss =>
            {
                translatedKeys.Add((emptyToNull(boss?.Base?.Key), emptyToNull(boss?.Translated?.Key)));
                boss.EnemyConfigNames.ForEach(countConfigName);
                List<NameTemplates> templates = new() { boss?.Translated };
                if (boss.OtherTranslated != null)
                {
                    templates.AddRange(boss.OtherTranslated);
                }
                // Can test original
                if (englishMode)
                {
                    templates = new() { boss.Base };
                }
                bool first = true;
                bool validSource = true, validTarget = true;
                foreach (NameTemplates names in templates)
                {
                    bool sourceComplete = !string.IsNullOrEmpty(names?.SourceFullName)
                        && namesFilledIn(names.FullName, names.PartialName, names.ProperName);
                    bool targetComplete = !string.IsNullOrEmpty(names?.MainTemplate)
                        && namesFilledIn(names.FullTemplate, names.PartialTemplate, names.ProperTemplate, names.SourceTemplate);
                    List<string> sourceIssues = null, targetIssues = null;
                    if (sourceComplete && first)
                    {
                        sourceIssues = new();
                        if (names.ProperName != null)
                        {
                            validate(sourceIssues, names.FullName == null && names.PartialName == null, "proper_name cannot be used with full_name or partial_name");
                        }
                        else
                        {
                            validate(sourceIssues, names.FullName != null, "full_name must be present");
                            validate(sourceIssues, names.FullName != names.PartialName, "full_name and partial_name should be different (remove partial_name if they're the same)");
                        }
                        foreach (string item in new[] { names.FullName, names.PartialName, names.ProperName })
                        {
                            if (item != null)
                            {
                                validate(sourceIssues, !item.Contains("$1"), "name contains $1 (not a template)");
                            }
                        }
                    }
                    if (targetComplete)
                    {
                        targetIssues = new();
                        validate(targetIssues, names.FullTemplate == null || names.PartialTemplate == null, "Cannot set both full_template and partial_template");
                        foreach (string item in new[] { names.FullTemplate, names.PartialTemplate, names.ProperTemplate, names.SourceTemplate })
                        {
                            if (item != null)
                            {
                                string[] parts = item.Split("$1");
                                validate(targetIssues, parts.Length == 2, $"template must contain exactly one $1, found {parts.Length - 1}");
                            }
                        }
                        if (!first)
                        {
                            // Consider source done if empty
                            sourceIssues = new();
                            validate(sourceIssues, names.SourcePartialName == null, "name fields cannot be specified in other_translated, only template fields");
                            validate(targetIssues, names.Category != null, "category field must be specified in other_translated");
                            if (boss?.Translated?.Key != null)
                            {
                                validate(targetIssues, boss.Translated.Key == names.Key, "key does not match main section");
                            }
                        }
                    }
                    ValidationResult result = new ValidationResult(boss?.Base?.Key, boss?.Translated?.Key, sourceIssues, targetIssues);
                    results.Add(result);
                    first = false;
                    validSource &= result.IsSourceValid;
                    validTarget &= result.IsTargetValid;
                }
                if (validSource)
                {
                    sources.Add(boss);
                }
                if (validTarget)
                {
                    targets.Add(boss);
                }
            });
            config.EnemyConfigNames.ForEach(countConfigName);

            // Validate mappings are correct
            List<string> structureErrors = new();
            void checkEnglishMapping(List<(string, string)> mapping, SortedSet<string> englishNames, string type)
            {
                // English names should always be present
                SortedSet<string> fromNames = new(mapping.Select(e => e.Item1));
                validate(structureErrors, !fromNames.Contains(null), $"found {type} without English key: [{string.Join("; ", mapping.Where(e => e.Item1 == null).Select(e => e.Item2))}]");
                fromNames.Remove(null);
                // English names should exactly match expected names
                List<string> englishOnly = englishNames.Except(fromNames).ToList();
                List<string> translatedOnly = fromNames.Except(englishNames).ToList();
                validate(structureErrors, englishOnly.Count == 0, $"English {type} missing from config: [{string.Join("; ", englishOnly)}]");
                validate(structureErrors, translatedOnly.Count == 0, $"found unknown English {type} in config: [{string.Join("; ", translatedOnly)}]");
                // English names should always be unique
                List<string> duplicateEnglish = new(mapping
                    .GroupBy(e => e.Item1)
                    .Where(g => g.Key != null && g.Count() > 1)
                    .Select(g => g.Key));
                validate(structureErrors, duplicateEnglish.Count == 0, $"found duplicate English {type} in config: [{string.Join("; ", duplicateEnglish)}]");
                // Translated names should be unique, except for known cases based on English name
                List<string> duplicateTranslated = new(mapping
                    .Where(e => e.Item1 != null && IsUniqueName(lang, e.Item1))
                    .GroupBy(e => e.Item2)
                    .Where(g => g.Key != null && g.Count() > 1)
                    .Select(g => g.Key));
                validate(structureErrors, duplicateTranslated.Count == 0, $"found duplicate {type} in config (should be unique for each English name): [{string.Join("; ", duplicateTranslated)}]");
            }
            checkEnglishMapping(translatedKeys, englishKeys, "boss names");
            checkEnglishMapping(translatedConfigNames, presetNames, "enemy config names");

            // Tally overall stats and add specific errors
            StringBuilder sb = new();

            int completeNameCount = translatedConfigNames.Count(x => !string.IsNullOrEmpty(x.Item2));
            int completeKeyCount = results.Count(r => r.IsComplete);
            int issueKeyCount = results.Count(r => r.HasIssues);
            string summary = $"{completeNameCount}/{translatedConfigNames.Count} config names complete, {completeKeyCount}/{results.Count} boss entries complete";
            if (issueKeyCount > 0)
            {
                summary += $", {issueKeyCount}/{results.Count} boss entry errors found";
            }
            if (structureErrors.Count > 0)
            {
                summary += $", {structureErrors.Count} mapping error{(structureErrors.Count == 1 ? "" : "s")}";
            }
            sb.AppendLine(summary);
            sb.AppendLine();

            bool anyError = false;
            string bossInput = null;
            if (englishKeys.Contains(input))
            {
                bossInput = input;
            }
            else if (translatedKeys.Any(e => e.Item2 == input))
            {
                bossInput = input;
            }
            else if (!string.IsNullOrEmpty(input))
            {
                sb.AppendLine($"Input error: unknown boss name \"{input}\"");
                anyError = true;
            }
            foreach (ValidationResult result in results)
            {
                if (bossInput != null && result.Key != bossInput && result.OtherKey != bossInput)
                {
                    continue;
                }
                foreach (string error in result.AllIssues)
                {
                    string loc = string.IsNullOrEmpty(result.Key) ? "" : $" in {result.Key}";
                    sb.AppendLine($"Error{loc}: {error}");
                    anyError = true;
                }
            }
            foreach (string error in structureErrors)
            {
                if (bossInput != null)
                {
                    break;
                }
                sb.AppendLine($"Mapping error: {error}");
                anyError = true;
            }
            if (anyError)
            {
                sb.AppendLine();
            }

            // Generate names, first in separate passes to avoid interleaving
            // (quadratic with potentially a lot of filtering)
            CustomCleverNames specialNames = CustomCleverNames.FromConfig(config.SpecialReplacements);
            if (englishMode)
            {
                specialNames = CustomCleverNames.FromDictionary(ann.CustomNames);
            }
            string fallback = "MISSING(ERROR)";
            string space = lang.StartsWith("jpn") || lang.StartsWith("zho") ? "　" : " ";
            if (englishMode) space = " ";
            if (bossInput != null)
            {
                foreach (bool useSource in new[] { true, false })
                {
                    // messages can be null if input is not configured at all, but it is valid if present
                    NameTemplatesMessage message = (useSource ? sources : targets).Find(s => s.Base?.Key == bossInput || s.Translated?.Key == bossInput);
                    List<NameTemplatesMessage> others = useSource ? targets : sources;
                    List<NameTemplates> inputNamesList = englishMode ? message?.AllBase() : message?.AllTranslated();
                    NameTemplates inputNames = inputNamesList?.FirstOrDefault();
                    sb.AppendLine($"\"{inputNames?.Key ?? bossInput}\" as {(useSource ? "source" : "target")}:");
                    if (message == null)
                    {
                        sb.AppendLine($"(valid config not found)");
                        sb.AppendLine();
                        continue;
                    }
                    foreach (NameTemplatesMessage other in others)
                    {
                        List<NameTemplates> otherNamesList = englishMode ? other.AllBase() : other.AllTranslated();
                        NameTemplates otherNames = otherNamesList.First();
                        NameTemplates source = useSource ? inputNames : otherNames;
                        List<NameTemplates> catTargets = useSource ? otherNamesList : inputNamesList;
                        NameTemplates target = null;
                        if (source.Category != null)
                        {
                            target = catTargets.Find(t => t.Category == source.Category);
                        }
                        target ??= catTargets.First();
                        // Do check here to avoid output
                        if (source.UniqueTemplate == target.UniqueTemplate)
                        {
                            sb.AppendLine($"{otherNames.Key}{space}→{space}(same)");
                            continue;
                        }
                        string name = CalculateCleverName(source, target, fallback);
                        sb.Append($"{otherNames.Key}{space}→{space}{name}");
                        if (specialNames.GetReplacement(source, target, name, out string replace) && name != replace)
                        {
                            sb.AppendLine($"{space}→{space}{replace}");
                        }
                        else
                        {
                            sb.AppendLine();
                        }
                    }
                    sb.AppendLine();
                }
            }

            // Check stray custom names
            if (!specialNames.Empty())
            {
                Dictionary<string, SortedSet<(string, string)>> nameSources = new();
            }

            return sb.ToString();
        }

#if DEV
        private record NameBase(string Key, string OtherKey, NameTemplates Base, bool IsMain);

        public static void WriteConfig(string lang, GameData game, EnemyAnnotations ann, RandomizerOptions opt, TextWriter writer, EnemyNamesConfig exist)
        {
            // TODO
            // DONE: Maybe sort by English name for consistency
            // DONE: Allow duplicate names (unfortunately Misbegotten Warrior is fake)
            // DONE: Weird things happening like Mad Pumpkin Head (Hammer) being the main boss there
            // DONE: Try to do autotranslation of NpcName stuff, check reverse mapping is unique as well
            // DONE: Include status reports, also in main messages
            // Make Demi-Human boss-basic groupings better
            Dictionary<uint, EnemyInfo> infos = ann.Enemies
                .Where(i => (i.Important != null || i.ExtraName != null) && i.Class != EnemyClass.HostileNPC)
                .ToDictionary(e => e.ID, e => e);
            Dictionary<string, EnemyInfo> englishInfo = infos.Values
                .Where(i => i.Important?.Names?.Key != null)
                .GroupBy(i => i.Important.Names.Key)
                // Select standalone where possible as Helper falls back to OwnedBy's category, and ExtraName isn't always valid there
                .Select(g => g.MinBy(i => i.Class == EnemyClass.Helper ? 1 : 0))
                .ToDictionary(i => i.Important.Names.Key, i => i);

            // Try to group things by top-level category
            Dictionary<string, List<string>> parentPresetNames = new();
            List<EnemyCategory> allCats = new();
            foreach (EnemyCategory parent in ann.AllCategories)
            {
                if (parent.Classes != null || parent.Contains != null) continue;
                List<string> names = new() { parent.Name };
                parent.Instances?.ForEach(cat => names.Add(cat.Name));
                parent.Partitions?.ForEach(cat => names.Add(cat.Name));
                allCats.Add(parent);
                parent.Instances?.ForEach(cat => allCats.Add(cat));
                parent.Partitions?.ForEach(cat => allCats.Add(cat));
                // A bit too different here probably
                if (parent.Name == "Human Bosses")
                {
                    foreach (string name in names)
                    {
                        parentPresetNames[name] = new() { name };
                    }
                }
                else
                {
                    parentPresetNames[parent.Name] = names;
                }
            }
            // Localize not-randomized boss as well
            parentPresetNames["Patches"] = new() { "Patches" };
            // foreach ((string name, List<string> names) in parentPresetNames) Console.WriteLine($"{name}: {string.Join("; ", names)}");
            Dictionary<string, string> nameParents = parentPresetNames
                .SelectMany(e => e.Value.Select(name => (name, e.Key)))
                .ToDictionary(e => e.name, e => e.Key);

            FMGDictionary langFmgs = game.AllItemFMGs[lang];
            Dictionary<string, string> bossKeyTranslations = new();
            HashSet<int> bossNpcIds = new(infos.Values.Where(e => e.Important != null).Select(e => e.Important.NpcName));
            Dictionary<int, string> getBossNames(FMGX fmg) => fmg.Entries.Where(e => bossNpcIds.Contains(e.ID) && !string.IsNullOrEmpty(e.Text)).ToDictionary(e => e.ID, e => e.Text);
            Dictionary<int, string> originalNames = getBossNames(game.ItemFMGs["NpcName"]);
            Dictionary<int, string> langNames = getBossNames(langFmgs["NpcName"]);
            foreach (var grouping in originalNames.GroupBy(e => e.Value))
            {
                string name = grouping.Key;
                SortedSet<string> translatedNames = new(grouping.Select(e => langNames.TryGetValue(e.Key, out string n) ? n : "?"));
                string translatedName = translatedNames.FirstOrDefault();
                if (translatedNames.Count != 1 || translatedNames.Contains("?"))
                {
                    // This case is losing some information (which was not present in other languages anyway)
                    if (lang == "porbr" && name == "Crucible Knight")
                    {
                        // Cavaleiro do Crisol (Lança Grande)
                    }
                    else if (lang == "rusru" && name == "Tree Sentinel")
                    {
                        // Capitalized in base game only (Страж древа; Страж Древа)
                        name = translatedNames.Last();
                    }
                    else throw new Exception($"No mapping for {name}: [{string.Join("; ", translatedNames)}]");
                }
                bossKeyTranslations[name] = translatedName;
            }
            bossKeyTranslations.Where(e => IsUniqueName(lang, e.Key)).ToDictionary(e => e.Value, e => e.Key);

            // Group all bosses by (parent, key) and find the 'main' boss.
            SortedDictionary<string, SortedDictionary<string, NameBase>> bossesByParent = new();
            // Then distribute all names among the bosses
            Dictionary<string, List<string>> bossConfigNames = new();
            string getParent(EnemyInfo info)
            {
                uint otherId = 0;
                nameParents.TryGetValue(info.ExtraName, out string parent);
                if (info.Class == EnemyClass.Helper)
                {
                    otherId = info.OwnedBy;
                }
                else if (info.NextPhase > 0 && info.ExtraName != parent)
                {
                    // This is annoying for major bosses with distinct parents
                    // otherId = info.NextPhase;
                }
                if (otherId > 0)
                {
                    if (!infos.TryGetValue(otherId, out EnemyInfo owner)) throw new Exception($"{info.ID} ({info.ExtraName}) has id {otherId} not found with names");
                    return getParent(owner);
                }
                // Combining only just for config names, to add avoiding bosses to the aux section
                if (info.ExtraName == null) throw new Exception($"No ExtraName for {info.ID}");
                if (parent == null) throw new Exception($"No parent found for {info.ExtraName}");
                return parent;
            }
            foreach (var grouping in englishInfo.Values.GroupBy(getParent))
            {
                string parent = grouping.Key;
                List<string> configNames = parentPresetNames[parent];
                if (parent == "Divine Beast Dancing Lion")
                {
                    // This does not have a unique boss representing it
                    configNames.AddRange(parentPresetNames["Divine Beast Dancing Lion and Basilisks"]);
                }
                // Prioritize either parent name or shortest name most like the parent
                int getMainPriority(string name)
                {
                    if (name == parent) return 0;
                    if (name == parent + " Boss") return 1;
                    if (name.StartsWith(parent)) return 2;
                    return 3;
                }
                (int, int) getMainKey(string name) => (getMainPriority(name), name.Length);
                string mainName = configNames.OrderBy(getMainKey).Where(name => grouping.Any(info => info.ExtraName == name)).FirstOrDefault();
                if (mainName == null) throw new Exception($"No boss found for {parent}: [{string.Join(", ", configNames)}] has no [{string.Join(", ", grouping.Select(info => info.ExtraName))}]");
                EnemyInfo mainInfo = grouping.Where(info => info.ExtraName == mainName).First();
                string mainKeyName = mainInfo.Important?.Names?.Key ?? mainName;
                // if (parent.StartsWith("Night's Cavalry"))
                // Console.WriteLine($"Parent {parent} -> [{string.Join("; ", configNames)}], main {mainName}, for {string.Join(",", grouping.Select(i => i.ID))}");
                // As for the actual ordering of config names, always prefer main name, then main name prefix, each sorted alphabetically
                (int, string) getOrderKey(string name) => (name == mainKeyName ? 0 : (name.StartsWith(mainKeyName) ? 1 : 2), name);
                SortedDictionary<string, NameBase> bossesMap = new();
                HashSet<string> addedConfigNames = new();
                foreach (EnemyInfo info in grouping)
                {
                    NameTemplates names = info.Important.Names;
                    // TODO: Verify is the same for all templates. Above DistinctBy would need to be a GroupBy
                    int npcName = info.Important.NpcName;
                    string otherKey = langFmgs["NpcName"][npcName];
                    if (string.IsNullOrEmpty(otherKey)) throw new Exception($"Missing in {lang}: {npcName} {names.Key}");
                    if (bossesMap.TryGetValue(otherKey, out NameBase dupe)) throw new Exception($"{otherKey} used for two different keys: {names.Key}; {dupe.Key}");
                    List<string> ownedConfigNames = new();
                    bool isMain = false;
                    string extraName = info.ExtraName;
                    // Exclude these Night's Cavs as they're combined into the main category despite having unique names
                    // Another approach would be ignoring the extra bits, but it's basically harmless
                    if (info.ID == 1248550800 || info.ID == 1248550801) extraName += " (duplicate)";
                    if (configNames.Contains(extraName))
                    {
                        ownedConfigNames.Add(extraName);
                    }
                    if (extraName == mainName)
                    {
                        isMain = true;
                        ownedConfigNames.AddRange(configNames.Except(grouping.Select(i => i.ExtraName)));
                    }
                    bossesMap.Add(names.Key, new NameBase(names.Key, otherKey, names, isMain));
                    ownedConfigNames = ownedConfigNames.OrderBy(getOrderKey).ToList();
                    foreach (string basicName in Enumerable.Reverse(ownedConfigNames).Where(n => n.StartsWith("Basic ")).ToList())
                    {
                        // This is pretty messy but helps with issues of alphabetical sort not being helpful
                        string primaryName = basicName.Substring(6);
                        int index = ownedConfigNames.FindLastIndex(n => n.StartsWith(primaryName));
                        if (index != -1)
                        {
                            int baseIndex = ownedConfigNames.IndexOf(basicName);
                            ownedConfigNames.Remove(basicName);
                            if (index < baseIndex) index++;
                            ownedConfigNames.Insert(index, basicName);
                        }
                    }
                    bossConfigNames.Add(names.Key, ownedConfigNames);
                }
                bossesByParent.Add(mainName, bossesMap);
            }

            // By English key
            Dictionary<string, EnemyPresetName> existNames = new();
            Dictionary<string, NameTemplatesMessage> existTemplates = new();
            if (exist != null)
            {
                existNames = exist.EnemyConfigNames.Concat(exist.BossNames.SelectMany(b => b.EnemyConfigNames ?? new()))
                    .ToDictionary(n => n.EnglishText, n => n);
                existTemplates = exist.BossNames.ToDictionary(b => b.Base.Key, b => b);
            }

            EnemyNamesConfig config = new();
            HashSet<string> addedPresetNames = new();
            Dictionary<string, EnemyCategory> cats = allCats.ToDictionary(c => c.Name, c => c);
            // More hardcoding
            cats["Patches"] = new EnemyCategory { Count = 2, ParentClasses = new() { EnemyClass.MinorBoss } };
            // Very unlocalized
            string getClassName(EnemyClass cl) => ClassNames[cl].Str;
            EnemyPresetName getPresetName(string name)
            {
                string translatedName = null;
                if (existNames.TryGetValue(name, out EnemyPresetName existName) && !string.IsNullOrEmpty(existName.Text))
                {
                    translatedName = existName.Text;
                }
                else
                {
                    bossKeyTranslations.TryGetValue(name, out translatedName);
                }
                if (!cats.TryGetValue(name, out EnemyCategory cat)) throw new Exception($"Unknown {name}");
                if (cat.ParentClasses == null) throw new Exception($"No ParentClasses for {name}");
                nameParents.TryGetValue(name, out string parent);
                string suffix = "";
                if (parent != null && parent != name) suffix += $", part of {parent}";
                // if (cat.Keywords != null) suffix += $". Search keywords: {cat.Keywords}";
                return new EnemyPresetName
                {
                    EnglishText = name,
                    // Info = $"{cat.Count} instance{(cat.Count == 1 ? "" : "s")} including {string.Join(", ", cat.ParentClasses.Select(getClassName))}",
                    Info = $"{cat.Count}x in {string.Join(" + ", cat.ParentClasses.Select(getClassName))}{suffix}",
                    Text = translatedName ?? "",
                };
            }
            foreach ((string mainKey, SortedDictionary<string, NameBase> nameGroup) in bossesByParent)
            {
                foreach ((string key, NameBase nameBase) in nameGroup.OrderBy(e => e.Value.IsMain ? 0 : 1))
                {
                    existTemplates.TryGetValue(key, out NameTemplatesMessage existOther);
                    NameTemplates names = nameBase.Base;
                    string otherKey = nameBase.OtherKey;
                    NameTemplates otherNames = existOther?.Translated ?? new() { Key = otherKey };
                    if (string.IsNullOrEmpty(otherNames.MainTemplate))
                    {
                        if (names.FullName != null)
                        {
                            if (names.PartialName != null || names.Key == names.FullName) otherNames.FullName = otherKey;
                            else otherNames.FullName = "";
                        }
                        if (names.PartialName != null) otherNames.PartialName = "";
                        if (names.ProperName != null) otherNames.ProperName = "";
                        if (names.FullTemplate != null) otherNames.FullTemplate = "";
                        if (names.PartialTemplate != null) otherNames.PartialTemplate = "";
                        if (names.ProperTemplate != null) otherNames.ProperTemplate = "";
                        if (names.SourceTemplate != null) otherNames.SourceTemplate = "";
                        if (key == "Necromancer Garris")
                        {
                            // Yeah just rewrite this
                            otherNames.ProperName = "";
                            otherNames.FullName = null;
                        }
                    }
                    List<string> configNames = bossConfigNames[names.Key];
                    addedPresetNames.UnionWith(configNames);
                    config.BossNames.Add(new NameTemplatesMessage
                    {
                        Base = names,
                        Translated = otherNames,
                        OtherTranslated = existOther?.OtherTranslated,
                        EnemyConfigNames = configNames.Select(getPresetName).ToList(),
                    });
                }
            }
            config.EnemyConfigNames = parentPresetNames.SelectMany(e => e.Value).Except(addedPresetNames).Select(getPresetName).ToList();
            List<bool> templateStatus = new();
            List<bool> configNameStatus = new();
            void countConfigName(EnemyPresetName name)
            {
                configNameStatus.Add(!string.IsNullOrEmpty(name.Text));
            }
            config.BossNames.ForEach(boss =>
            {
                templateStatus.Add(!string.IsNullOrEmpty(boss?.Translated?.MainTemplate) && !string.IsNullOrEmpty(boss?.Translated?.SourceFullName));
                boss.EnemyConfigNames.ForEach(countConfigName);
            });
            config.EnemyConfigNames.ForEach(countConfigName);
            config.Status = $"{templateStatus.Count(x => x)}/{templateStatus.Count} custom names complete, {configNameStatus.Count(x => x)}/{configNameStatus.Count} config names complete";
            if (exist != null)
            {
                config.SpecialReplacements = exist.SpecialReplacements;
                config.CapitalizeCustomName = exist.CapitalizeCustomName;
            }
            if (!opt["outjson"] && !opt["write"])
            {
                return;
            }
            writer.WriteLine(JsonSerializer.Serialize(config, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            }));
        }
#endif

        private static readonly List<string> IncorrectLeonineLangs = new() { "deude", "frafr", "spaar" };
        private static bool IsUniqueName(string lang, string englishName)
        {
            if (IncorrectLeonineLangs.Contains(lang))
            {
                // Translated as Leonine Misbegotten
                return englishName != "Misbegotten Warrior";
            }
            if (lang == "polpl")
            {
                // Translated as Mimic Tear
                return englishName != "Stray Mimic Tear";
            }
            if (lang == "porbr")
            {
                // Translated as Dragonkin Soldier of Nokstella
                return englishName != "Dragonkin Soldier";
            }
            return true;
        }
    }
}
