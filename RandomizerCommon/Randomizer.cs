using System;
using System.IO;
using System.Linq;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsIds.GameSpec;
using static RandomizerCommon.Messages;

namespace RandomizerCommon
{
    public class Randomizer
    {
        [Localize]
        private static readonly Text loadPhase = new Text("Loading game data", "Randomizer_loadPhase");
        [Localize]
        private static readonly Text enemyPhase = new Text("Randomizing enemies", "Randomizer_enemyPhase");
        [Localize]
        private static readonly Text itemPhase = new Text("Randomizing items", "Randomizer_itemPhase");
        [Localize]
        private static readonly Text editPhase = new Text("Editing game files", "Randomizer_editPhase");
        [Localize]
        private static readonly Text writePhase = new Text("Writing game files", "Randomizer_savePhase");

        // TODO: There are way too many arguments here
        // And should we be using... ugh... dependency injection
        public void Randomize(RandomizerOptions opt, FromGame type, Action<string> notify = null, string outPath = null, Preset preset = null, Messages messages = null, bool encrypted = true, string gameExe = null)
        {
            messages = messages ?? new Messages(null);
            string distDir = type == FromGame.ER ? "diste" : (type == FromGame.SDT ? "dists" : "dist");
            if (!Directory.Exists(distDir))
            {
                // From Release/Debug dirs
                distDir = $@"..\..\..\{distDir}";
                opt["dryrun"] = true;
            }
            if (!Directory.Exists(distDir))
            {
                throw new Exception("Missing data directory");
            }
            if (outPath == null)
            {
                if (type == FromGame.ER && opt["uxm"])
                {
                    outPath = Path.GetDirectoryName(gameExe);
                }
                else
                {
                    outPath = Directory.GetCurrentDirectory();
                }
            }
            bool header = !opt.GetOptions().Any(o => o.StartsWith("dump"));
            if (!header)
            {
                notify = null;
            }

            if (header)
            {
                Console.WriteLine($"Options and seed: {opt}");
                Console.WriteLine();
            }
            int seed = (int)opt.Seed;

            notify?.Invoke(messages.Get(loadPhase));
            string modDir = null;
            if (opt["mergemods"])
            {
                if (type == FromGame.ER)
                {
                    modDir = Path.GetDirectoryName(gameExe);
                }
                else
                {
                    string modPath = type == FromGame.DS3 ? "mod" : "mods";
                    DirectoryInfo modDirInfo = new DirectoryInfo($@"{outPath}\..\{modPath}");
                    if (!modDirInfo.Exists) throw new Exception($"Can't merge mods: {modDirInfo.FullName} not found");
                    modDir = modDirInfo.FullName;
                    if (new DirectoryInfo(outPath).FullName == modDir) throw new Exception($"Can't merge mods: already running from 'mods' directory");
                }
            }
            GameData game = new GameData(distDir, type);
            game.Load(modDir);
            // MiscSetup.UpdateEldenRing(game, opt); return;
            // game.UnDcx(ForGame(FromGame.ER).GameDir + @"\map\mapstudio"); return;
            // game.SearchParamInt(14000800); return;
            // foreach (string lang in MiscSetup.Langs) game.DumpMessages(GameSpec.ForGame(GameSpec.FromGame.ER).GameDir + $@"\msg\{lang}"); return;
            // game.DumpMessages(GameSpec.ForGame(GameSpec.FromGame.ER).GameDir + @"\msg\engus"); return;
            // game.DumpMessages(GameSpec.ForGame(GameSpec.FromGame.DS1R).GameDir + @"\msg\ENGLISH"); return;
            // MiscSetup.CombineSFX(game.Maps.Keys.Concat(new[] { "dlc1", "dlc2" }).ToList(), GameSpec.ForGame(GameSpec.FromGame.DS3).GameDir + @"\combinedai", true); return;
            // MiscSetup.CombineAI(game.Maps.Keys.ToList(), ForGame(FromGame.DS3).GameDir + @"\combinedai", true); return;

            // Prologue
            if (header)
            {
                if (modDir != null) Console.WriteLine();
                if (opt["enemy"])
                {
                    Console.WriteLine("Ctrl+F 'Boss placements' or 'Miniboss placements' or 'Basic placements' to see enemy placements.");
                }
                if (opt["item"])
                {
                    Console.WriteLine("Ctrl+F 'Hints' to see item placement hints, or Ctrl+F for a specific item name.");
                }
                Console.WriteLine();
#if !DEBUG
                for (int i = 0; i < 50; i++) Console.WriteLine();
#endif
            }

            // Slightly different high-level algorithm for each game.
            if (game.Sekiro)
            {
                Events events = new Events($@"{game.Dir}\Base\sekiro-common.emedf.json");
                EventConfig eventConfig;
                using (var reader = File.OpenText($@"{game.Dir}\Base\events.txt"))
                {
                    IDeserializer deserializer = new DeserializerBuilder().Build();
                    eventConfig = deserializer.Deserialize<EventConfig>(reader);
                }

                EnemyLocations locations = null;
                if (opt["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    locations = new EnemyRandomizer(game, events, eventConfig).Run(opt, preset);
                    if (!opt["enemytoitem"])
                    {
                        locations = null;
                    }
                }
                if (opt["item"])
                {
                    notify?.Invoke("Randomizing items");
                    SekiroLocationDataScraper scraper = new SekiroLocationDataScraper();
                    LocationData data = scraper.FindItems(game);
                    AnnotationData anns = new AnnotationData(game, data);
                    anns.Load(opt);
                    anns.AddEnemyLocations(locations);

                    SkillSplitter.Assignment split = null;
                    if (!opt["norandom_skills"] && opt["splitskills"])
                    {
                        split = new SkillSplitter(game, data, anns, events).SplitAll();
                    }

                    Permutation perm = new Permutation(game, data, anns, messages, explain: false);
                    perm.Logic(new Random(seed), opt, preset);

                    notify?.Invoke("Editing game files");
                    PermutationWriter write = new PermutationWriter(game, data, anns, events, eventConfig);
                    write.Write(new Random(seed + 1), perm, opt);
                    if (!opt["norandom_skills"])
                    {
                        SkillWriter skills = new SkillWriter(game, data, anns);
                        skills.RandomizeTrees(new Random(seed + 2), perm, split);
                    }
                    if (opt["edittext"])
                    {
                        HintWriter hints = new HintWriter(game, data, anns);
                        hints.Write(opt, perm);
                    }
                }
                MiscSetup.SekiroCommonPass(game, events, opt);

                notify?.Invoke("Writing game files");
                if (!opt["dryrun"])
                {
                    game.SaveSekiro(outPath);
                }
                return;
            }
            else if (game.DS3)
            {
                Events events = new Events($@"{game.Dir}\Base\ds3-common.emedf.json", true);
                EventConfig eventConfig;
                using (var reader = File.OpenText($@"{game.Dir}\Base\events.txt"))
                {
                    IDeserializer deserializer = new DeserializerBuilder().Build();
                    eventConfig = deserializer.Deserialize<EventConfig>(reader);
                }

                LocationDataScraper scraper = new LocationDataScraper(logUnused: false);
                LocationData data = scraper.FindItems(game);
                AnnotationData ann = new AnnotationData(game, data);
                ann.Load(opt);

                if (opt["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    new EnemyRandomizer(game, events, eventConfig).Run(opt, preset);
                }

                if (opt["item"])
                {
                    ann.AddSpecialItems();
                    notify?.Invoke("Randomizing items");
                    Random random = new Random(seed);
                    Permutation permutation = new Permutation(game, data, ann, messages, explain: false);
                    permutation.Logic(random, opt, null);

                    notify?.Invoke("Editing game files");
                    random = new Random(seed + 1);
                    PermutationWriter writer = new PermutationWriter(game, data, ann, events, null);
                    writer.Write(random, permutation, opt);
                    random = new Random(seed + 2);
                    // TODO maybe randomize other characters no matter what, only do self for item rando
                    CharacterWriter characters = new CharacterWriter(game, data);
                    characters.Write(random, opt);
                }
                else if (opt["enemychr"])
                {
                    // temp
                    Random random = new Random(seed);
                    CharacterWriter characters = new CharacterWriter(game, data);
                    characters.Write(random, opt);
                }
                MiscSetup.DS3CommonPass(game, events, opt);

                notify?.Invoke("Writing game files");
                if (!opt["dryrun"])
                {
                    game.SaveDS3(outPath, encrypted);
                }
            }
            // Locations for bosses with weird second phases (Rennala 2, Elden Beast, Deeproot Dragon? lichdragon - )
            // Clean up all aaaaaaaaaaa log entries
            // For enemy drop in logs, don't show map
            // Unlock all maps upon entering Limgrave
            else if (game.EldenRing)
            {
                if (opt["noitem"])
                {
                    new EldenDataPrinter().PrintData(game, opt);
                    return;
                }
                if (opt["item"])
                {
                    notify?.Invoke(messages.Get(itemPhase));
                    EventConfig itemEventConfig;
                    using (var reader = File.OpenText($@"{game.Dir}\Base\itemevents.txt"))
                    {
                        IDeserializer deserializer = new DeserializerBuilder().Build();
                        itemEventConfig = deserializer.Deserialize<EventConfig>(reader);
                    }

                    EldenCoordinator coord = new EldenCoordinator(game, opt["debugcoords"]);
                    EldenLocationDataScraper scraper = new EldenLocationDataScraper();
                    LocationData data = scraper.FindItems(game, coord, opt);
                    if (data == null || opt["dumplot"] || opt["dumpitemflag"])
                    {
                        return;
                    }
                    AnnotationData ann = new AnnotationData(game, data);
                    ann.Load(opt);
                    if (opt["dumpann"])
                    {
                        ann.Save(initial: false, filter: opt["annfilter"], coord: coord);
                        return;
                    }
                    ann.AddSpecialItems();
                    // new HintMarker(game, data, ann, coord).Write(opt, null); return;

                    Random random = new Random(seed);
                    Permutation perm = new Permutation(game, data, ann, messages, explain: opt["explain"]);
                    perm.Logic(random, opt, preset);

                    notify?.Invoke(messages.Get(editPhase));
                    random = new Random(seed + 1);
                    PermutationWriter writer = new PermutationWriter(game, data, ann, null, itemEventConfig, coord);
                    PermutationWriter.Result permResult = writer.Write(random, perm, opt);

                    if (opt["markareas"])
                    {
                        new HintMarker(game, data, ann, messages, coord).Write(opt, perm, permResult);
                    }

                    if (opt["mats"])
                    {
                        random = new Random(seed + 1);
                        new EldenMaterialRandomizer(game, data).Randomize(random);
                    }

                    random = new Random(seed + 2);
                    CharacterWriter characters = new CharacterWriter(game, data);
                    characters.Write(random, opt);
                }

                MiscSetup.EldenCommonPass(game, opt);

                if (!opt["dryrun"])
                {
                    notify?.Invoke(messages.Get(writePhase));
                    game.SaveEldenRing(outPath, opt["uxm"]);
                }
            }
        }
    }
}
