using System;
using System.IO;
using SoulsIds;
using YamlDotNet.Serialization;
using static SoulsIds.GameSpec;

namespace RandomizerCommon
{
    public class Randomizer
    {
        public void Randomize(RandomizerOptions options, FromGame type, Action<string> notify = null, string outPath = null, Preset preset = null, bool encrypted = true)
        {
            string distDir = type == FromGame.ER ? "diste" : (type == FromGame.SDT ? "dists" : "dist");
            if (!Directory.Exists(distDir))
            {
                // From Release/Debug dirs
                distDir = $@"..\..\..\{distDir}";
                options["dryrun"] = true;
            }
            if (!Directory.Exists(distDir))
            {
                throw new Exception("Missing data directory");
            }
            if (outPath == null)
            {
                outPath = Directory.GetCurrentDirectory();
            }

            Console.WriteLine($"Options and seed: {options}");
            Console.WriteLine();
            int seed = (int)options.Seed;

            notify?.Invoke("Loading game data");
            string modDir = null;
            if (options["mergemods"])
            {
                string modPath = type == FromGame.DS3 ? "mod" : "mods";
                DirectoryInfo modDirInfo = new DirectoryInfo($@"{outPath}\..\{modPath}");
                if (!modDirInfo.Exists) throw new Exception($"Can't merge mods: {modDirInfo.FullName} not found");
                modDir = modDirInfo.FullName;
                if (new DirectoryInfo(outPath).FullName == modDir) throw new Exception($"Can't merge mods: already running from 'mods' directory");
            }
            GameData game = new GameData(distDir, type);
            game.Load(modDir);
            // game.UnDcx(ForGame(FromGame.ER).GameDir + @"\event"); return;
            // game.SearchParamInt(14000800); return;
            // game.DumpMessages(GameSpec.ForGame(GameSpec.FromGame.SDT).GameDir + @"\msg\jpnjp"); return;
            // game.DumpMessages(GameSpec.ForGame(GameSpec.FromGame.DS1R).GameDir + @"\msg\JAPANESE"); return;
            // MiscSetup.CombineSFX(game.Maps.Keys.Concat(new[] { "dlc1", "dlc2" }).ToList(), GameSpec.ForGame(GameSpec.FromGame.DS3).GameDir + @"\randomizer", true); return;
            // MiscSetup.CombineAI(game.Maps.Keys.ToList(), ForGame(FromGame.DS3).GameDir + @"\randomizer", true); return;
            if (modDir != null) Console.WriteLine();

            // Prologue
            if (options["enemy"])
            {
                Console.WriteLine("Ctrl+F 'Boss placements' or 'Miniboss placements' or 'Basic placements' to see enemy placements.");
            }
            if (options["item"])
            {
                Console.WriteLine("Ctrl+F 'Hints' to see item placement hints, or Ctrl+F for a specific item name.");
            }
            Console.WriteLine();
#if !DEBUG
            for (int i = 0; i < 50; i++) Console.WriteLine();
#endif

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
                if (options["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    locations = new EnemyRandomizer(game, events, eventConfig).Run(options, preset);
                    if (!options["enemytoitem"])
                    {
                        locations = null;
                    }
                }
                if (options["item"])
                {
                    notify?.Invoke("Randomizing items");
                    SekiroLocationDataScraper scraper = new SekiroLocationDataScraper();
                    LocationData data = scraper.FindItems(game);
                    AnnotationData anns = new AnnotationData(game, data);
                    anns.Load(options);
                    anns.AddEnemyLocations(locations);

                    SkillSplitter.Assignment split = null;
                    if (!options["norandom_skills"] && options["splitskills"])
                    {
                        split = new SkillSplitter(game, data, anns, events).SplitAll();
                    }

                    Permutation perm = new Permutation(game, data, anns, explain: false);
                    perm.Logic(new Random(seed), options, preset);

                    notify?.Invoke("Editing game files");
                    PermutationWriter write = new PermutationWriter(game, data, anns, events, eventConfig);
                    write.Write(new Random(seed + 1), perm, options);
                    if (!options["norandom_skills"])
                    {
                        SkillWriter skills = new SkillWriter(game, data, anns);
                        skills.RandomizeTrees(new Random(seed + 2), perm, split);
                    }
                    if (options["edittext"])
                    {
                        HintWriter hints = new HintWriter(game, data, anns);
                        hints.Write(options, perm);
                    }
                }
                MiscSetup.SekiroCommonPass(game, events, options);

                notify?.Invoke("Writing game files");
                if (!options["dryrun"])
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
                ann.Load(options);

                if (options["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    new EnemyRandomizer(game, events, eventConfig).Run(options, preset);
                }

                if (options["item"])
                {
                    ann.AddSpecialItems();
                    notify?.Invoke("Randomizing items");
                    Random random = new Random(seed);
                    Permutation permutation = new Permutation(game, data, ann, explain: false);
                    permutation.Logic(random, options, null);

                    notify?.Invoke("Editing game files");
                    random = new Random(seed + 1);
                    PermutationWriter writer = new PermutationWriter(game, data, ann, events, null);
                    writer.Write(random, permutation, options);
                    random = new Random(seed + 2);
                    // TODO maybe randomize other characters no matter what, only do self for item rando
                    CharacterWriter characters = new CharacterWriter(game, data);
                    characters.Write(random, options);
                }
                else if (options["enemychr"])
                {
                    // temp
                    Random random = new Random(seed);
                    CharacterWriter characters = new CharacterWriter(game, data);
                    characters.Write(random, options);
                }
                MiscSetup.DS3CommonPass(game, events, options);

                notify?.Invoke("Writing game files");
                if (!options["dryrun"])
                {
                    game.SaveDS3(outPath, encrypted);
                }
            }
            else if (game.EldenRing)
            {
                EldenLocationDataScraper scraper = new EldenLocationDataScraper();
                LocationData data = scraper.FindItems(game);
            }
        }
    }
}
