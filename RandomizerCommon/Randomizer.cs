using System;
using System.IO;
using System.Linq;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.Messages;
using static SoulsIds.GameSpec;
using System.Collections.Generic;
using LightInject;
using System.Drawing;

namespace RandomizerCommon
{
    public class Randomizer
    {
        [Localize]
        private static readonly Text extractPhase = new Text("Extracting game data", "Randomizer_extractPhase");
        [Localize]
        private static readonly Text loadPhase = new Text("Loading game data", "Randomizer_loadPhase");
        [Localize]
        private static readonly Text enemyPhase = new Text("Randomizing enemies", "Randomizer_enemyPhase");
        [Localize]
        private static readonly Text itemPhase = new Text("Randomizing items", "Randomizer_itemPhase");
        // [Localize]
        private static readonly Text mapPhase = new Text("Randomizing map", "Randomizer_mapPhase");
        [Localize]
        private static readonly Text editPhase = new Text("Editing game files", "Randomizer_editPhase");
        [Localize]
        private static readonly Text savePhase = new Text("Writing game files", "Randomizer_savePhase");
        [Localize]
        private static readonly Text saveMapPhase = new Text("Writing map data: {0}%", "Randomizer_saveMapPhase");
        [Localize]
        private static readonly Text restartMsg = new Text(
            "Error: Mismatch between regulation.bin and other randomizer files.\nMake sure all randomizer files are present, the game has been restarted\nafter randomization, and the game and regulation.bin versions are compatible.",
            "GameMenu_restart");

        [Localize]
        private static readonly Text mergeMissingError =
            new Text("Error merging mods: directory {0} not found", "Randomizer_mergeMissingError");
        [Localize]
        private static readonly Text mergeWrongDirError =
            new Text("Error merging mods: already running from {0} directory", "Randomizer_mergeWrongDirError");
        [Localize]
        private static readonly Text mergeFogError =
            new Text("Error: Invalid merge order for Fog Gate Randomizer. Carefully follow the instructions on the Fog Gate Randomizer mod page.", "Randomizer_mergeFogError");

        public static readonly List<Type> CommonMessageTypes = new()
        {
            typeof(Randomizer), typeof(MiscSetup), typeof(FormText), typeof(HelperOptions), typeof(EnemyPreset), typeof(EnemyAnnotations),
            typeof(ItemPreset), typeof(Permutation), typeof(PermutationWriter), typeof(CharacterWriter),
#if ER
            typeof(HintMarker), typeof(EldenMiscSetup), typeof(EldenItemEditor),
#endif
        };

        public static readonly string EldenVersion = "v0.12.1";
        public static readonly string DS1Version = "v0.1.3";
        public static readonly string DS3Version = "v0.4.1";
        public static readonly string NightreignVersion = "v0.1.6";
        public const bool Omit = false;

        private static readonly Dictionary<FromGame, string> distDirs = new Dictionary<FromGame, string>
        {
            [FromGame.DS3] = "dist",
            [FromGame.SDT] = "dists",
            [FromGame.ER] = "diste",
            [FromGame.AC6] = "distac6",
            [FromGame.DS1R] = "dist1",
            [FromGame.NR] = "distnr",
        };

        public void Randomize(
            RandomizerOptions opt,
            FromGame type,
            Action<string> notify = null,
            string outPath = null,
            EnemyPreset enemyPreset = null,
            ItemPreset itemPreset = null,
            Messages messages = null,
            string gameExe = null,
            MergedMods modDirs = null)
        {
            messages = messages ?? new Messages(null);
            string distDir = distDirs[type];
            string gameDir = Path.GetDirectoryName(gameExe);
            if (!Directory.Exists(distDir))
            {
                throw new Exception("Missing data directory");
            }
            if ((type == FromGame.ER && opt["uxm"]) || (type == FromGame.DS1R && opt["loose"]))
            {
                outPath = gameDir;
            }
            if (outPath == null)
            {
                outPath = MiscSetup.GetModEngineOutputDirectory();
            }
#if DEBUG
            if (new DirectoryInfo(outPath).Name == "SoulsRandomizers")
            {
                // Don't allow cluttering up the source directory
                opt["dryrun"] = true;
            }
#endif
            bool header = true;
#if DEV
            header = !opt.GetOptions().Any(o => o.StartsWith("dump")) && !opt["configgen"];
#endif
            if (!header)
            {
                notify = null;
            }

            if (header)
            {
                Console.WriteLine($"Options and seed: {opt}");
                if (opt.SeedStr != null)
                {
                    Console.WriteLine($"Real seed: {opt.Seed}");
                }
                Console.WriteLine();
            }
            int seed = (int)opt.Seed;

            if (opt["mergemods"])
            {
                // Previous Elden Ring UXM merge behavior
                string modPath = type == FromGame.SDT ? "mods" : "mod";
                DirectoryInfo modDirInfo = new DirectoryInfo($@"{outPath}\..\{modPath}");
                modDirs = new MergedMods(modDirInfo.FullName);
            }
            else if (opt["mergegame"])
            {
                DirectoryInfo modDirInfo = new DirectoryInfo(gameDir);
                modDirs = new MergedMods(modDirInfo.FullName);
            }
            // To avoid in DS1 just because other mods work this way, and we skip randobak in this case
            if (type == FromGame.DS1R && !opt["loose"] && Directory.GetCurrentDirectory() == new FileInfo(gameExe).Directory.FullName)
            {
                throw new Exception($"Error: Randomizer won't run from inside the game directory. Extract the zip to a different directory and leave the files there.");
            }
            modDirs = modDirs ?? new MergedMods();
            foreach (string modDir in modDirs.Dirs)
            {
                DirectoryInfo modDirInfo = new DirectoryInfo(modDir);
                if (modDirInfo != null)
                {
                    string outModDir = modDirInfo.FullName;
                    if (!modDirInfo.Exists)
                    {
                        throw new Exception(messages.Get(mergeMissingError, outModDir));
                    }
                    if (outModDir != null && new DirectoryInfo(outPath).FullName == outModDir)
                    {
                        // This should be filtered out earlier if merging via toml
                        throw new Exception(messages.Get(mergeWrongDirError, modDirInfo.Name));
                    }
                    if (File.Exists(Path.Combine(outModDir, "FogMod.exe")))
                    {
                        throw new Exception(messages.Get(mergeFogError));
                    }
                }
            }

            if (opt["shrooms"])
            {
                // This flow doesn't fully work anymore, but can be adapted into more generic flows
                string texMod = modDirs.Dirs.Where(MiscSetup.ProcessRandomaniaTextures).FirstOrDefault();
                if (texMod == null) throw new Exception($"Internal error: can't find directory to merge in Randomania (provided dirs: {string.Join("; ", modDirs.Dirs)})");
                if (!MiscSetup.ProcessedRandomaniaTextures(texMod))
                {
                    notify?.Invoke("Processing Randomania items");
                    MiscSetup.CopyRandomaniaTextures(gameDir, texMod);
                    if (!MiscSetup.ProcessedRandomaniaTextures(texMod)) throw new Exception("Finished Randomania processing but texture files weren't created??");
                }
            }

            GameData game = new GameData(distDir, type);
            game.UnpackVanillaFiles(gameDir, opt, () =>
            {
                notify?.Invoke(messages.Get(extractPhase));
            });
            notify?.Invoke(messages.Get(loadPhase));
            game.Load(modDirs);  // This prints all merged mod and overriden files
            MergeModManifest merge = MergeModManifest.FromMods(game, modDirs);

#if DEV
            if (opt["update"])
            {
                MiscDev.UpdateEldenRing(game, opt);
                return;
            }
#endif

            // Prologue
            if (header)
            {
                if (game.HasMods) Console.WriteLine();
                if (opt["enemy"])
                {
                    Console.WriteLine("Ctrl+F 'Boss placements' or 'Miniboss placements' or 'Basic placements' to see enemy placements.");
                }
                if (opt["item"])
                {
                    Console.WriteLine("Ctrl+F 'Hints' to see item placement hints, or Ctrl+F for a specific item name.");
                }
                if (type == FromGame.ER)
                {
                    Console.WriteLine($"Version: {EldenVersion}");
                }
                if (enemyPreset != null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"-- Enemy preset");
                    Console.Write(enemyPreset.ToYamlString());
                }
                if (itemPreset != null)
                {
                    Console.WriteLine();
                    Console.WriteLine($"-- Item preset");
                    Console.Write(itemPreset.ToYamlString());
                }
                if (merge != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("-- Configuration from merged randomizer_merge_config.yml files");
                    foreach (string info in merge.GetDescription()) Console.WriteLine(info);
                }
                Console.WriteLine();
#if !DEBUG
                for (int i = 0; i < 50; i++) Console.WriteLine();
#endif
            }

            // From here, set up dependency injection used by most thing-doer classes, aside from cases where constructors currently have a lot of logic.
            // This could be implemented as an application-long container with a different scope per run, which the UI can use as well, but keep it simple for now.
            // Also use LightInject for now because .NET DI adds 2 MB to binary size and could be a victim of trimming in the future.
            ServiceContainer container = new ServiceContainer(new ContainerOptions { EnablePropertyInjection = false });
            container.EnableAnnotatedConstructorInjection();

            // Some of these can maybe use type-based registration as DI scope grows, but this is fine for this setup.
            container.RegisterInstance(game);
            container.RegisterInstance(opt);
            container.RegisterInstance(messages);
            // Currently nullable, so instance cannot be registered directly
            container.RegisterSingleton(_ => merge);
            // To be constructed during randomization (shared)
            container.RegisterSingleton<AnnotationData>();
            container.Register<Permutation>();
            container.Register<PermutationWriter>();
            container.Register<ItemLocEditor>();
            container.Register<EnemyRandomizer>();
            if (!game.EldenRing)
            {
                container.RegisterSingleton<EldenCoordinator>(_ => null);
            }

            // Slightly different high-level algorithm for each game
#if SDT
            if (game.Sekiro)
            {
                // Sekiro-specific instances
                Events events = new Events($@"{game.Dir}\Base\sekiro-common.emedf.json");
                container.RegisterInstance(events);
                // TODO: Split into item and enemy config
                EventConfig eventConfig = Util.ReadYamlFile<EventConfig>($@"{game.Dir}\Base\events.txt");
                container.RegisterInstance(eventConfig);
                container.RegisterInstance(ItemEventConfig.FromEnemyConfig(eventConfig));
                container.Register<ItemEditor, SekiroItemEditor>();

                // To be constructed during randomization
                container.RegisterSingleton<LocationData>(factory => factory.GetInstance<SekiroLocationDataScraper>().FindItems());
                container.Register<SekiroLocationDataScraper>();
                container.Register<SkillSplitter>();
                container.Register<SkillWriter>();
                container.Register<HintWriter>();

                EnemyRandomizer.Result locations = null;
                if (opt["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    locations = container.GetInstance<EnemyRandomizer>().Run(opt, enemyPreset);
                    if (!opt["enemytoitem"])
                    {
                        locations = null;
                    }
                }
                if (opt["item"])
                {
                    notify?.Invoke("Randomizing items");
                    AnnotationData anns = container.GetInstance<AnnotationData>();
                    anns.Load(opt);
                    anns.ProcessRestrictions(opt, locations);

                    SkillSplitter.Assignment split = null;
                    if (!opt["norandom_skills"] && opt["splitskills"])
                    {
                        split = container.GetInstance<SkillSplitter>().SplitAll();
                    }

                    Permutation perm = container.GetInstance<Permutation>();
                    perm.LoadLocations(opt);
                    perm.Logic(new Random(seed), opt, enemyPreset);

                    notify?.Invoke("Editing game files");
                    container.GetInstance<PermutationWriter>().Write(new Random(seed + 1), perm, opt);
                    if (!opt["norandom_skills"])
                    {
                        container.GetInstance<SkillWriter>().RandomizeTrees(new Random(seed + 2), perm, split);
                    }
                    if (opt["edittext"])
                    {
                        container.GetInstance<HintWriter>().Write(opt, perm);
                    }
                }
                SekiroMiscSetup.SekiroCommonPass(game, events, opt);

                notify?.Invoke("Writing game files");
                if (!opt["dryrun"])
                {
                    game.SaveSekiro(outPath);
                }
                return;
            }
#endif
#if DS3
            if (game.DS3)
            {
                // DS3-specific instances
                Events events = new Events($@"{game.Dir}\Base\ds3-common.emedf.json", darkScriptMode: true);
                container.RegisterInstance(events);
                EventConfig eventConfig = Util.ReadYamlFile<EventConfig>($@"{game.Dir}\Base\events.txt");
                container.RegisterInstance(eventConfig);
                container.RegisterInstance(new ItemEventConfig());  // Not currently used
                container.Register<ItemEditor, DS3ItemEditor>();

                // To be constructed during randomization
                container.RegisterSingleton<LocationData>(factory => factory.GetInstance<LocationDataScraper>().FindItems());
                container.Register<LocationDataScraper>();
                container.Register<CharacterWriter>();

                // Note this is done before enemy randomizer as it is based on map data which may change.
                AnnotationData ann = container.GetInstance<AnnotationData>();
                ann.Load(opt);

                // This doesn't take presets into account, probably consider that when adding proper preset editing (in Avalonia)
                int trueSeed = (int)Util.JavaStringHash(opt.LogicString());

                if (opt["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    container.GetInstance<EnemyRandomizer>().Run(opt, enemyPreset);
                }

                if (opt["item"])
                {
                    ann.AddSpecialItems();
                    if (opt["copydrops"])
                    {
                        ann.AddFiniteEquipmentCopies();
                    }
                    notify?.Invoke("Randomizing items");
                    Random random = new Random(seed);
                    Permutation permutation = container.GetInstance<Permutation>();
                    permutation.LoadLocations(opt);
                    permutation.Logic(random, opt, null);

                    notify?.Invoke("Editing game files");
                    random = new Random(seed + 1);
                    container.GetInstance<PermutationWriter>().Write(random, permutation, opt);
                    // We could add character rando without item rando, but right now they are coupled
                    container.GetInstance<CharacterWriter>().Write(trueSeed, opt);
                }
                DS3MiscSetup.DS3CommonPass(game, events, opt);

                notify?.Invoke("Writing game files");
                if (!opt["dryrun"])
                {
                    game.SaveDS3(outPath, gameDir);
                }
                return;
            }
#endif
#if ER
            if (game.EldenRing)
            {
                // Base character data on a few things: seed, logic options, logical preset contents.
                // In theory, could also look at loaded params/maps, but try this for now.
                int trueSeed = (int)Util.JavaStringHash(opt.LogicString() + "&&" + (enemyPreset == null ? "" : enemyPreset.ToStableString()));

                // Elden Ring-specific instances
                string emedfPath = @"diste\Base\er-common.emedf.json";
                Events events = new Events(emedfPath, darkScriptMode: true, paramAwareMode: true, skipAwareMode: true);
                container.RegisterInstance(events);

                // To be constructed during randomization
                container.RegisterSingleton<EldenCoordinator>();
                container.RegisterSingleton<ItemEventConfig>(_ => Util.ReadYamlFile<ItemEventConfig>($@"{game.Dir}\Base\itemevents.txt"));
                string enemyEventPath = $@"{game.Dir}\Base\events.txt";
#if DEV
                if (opt["full"] || opt["configgen"])
                {
                    // Over 10x bigger than trimmed version
                    enemyEventPath = @"configs\diste\events.txt";
                }
#endif
                container.RegisterSingleton<EventConfig>(_ => Util.ReadYamlFile<EventConfig>(enemyEventPath));
                container.Register<ItemEditor, EldenItemEditor>();

                container.RegisterSingleton<LocationData>(factory => factory.GetInstance<EldenLocationDataScraper>().FindItems());
                container.Register<EldenLocationDataScraper>();
                container.Register<HintMarker>();
                container.Register<EldenMaterialRandomizer>();
                // TODO: Split heavy ESD editing out of CharacterWriter into Elden Ring directory with common utils
                container.RegisterSingleton<CharacterWriter>();

                PermutationWriter.Result permResult = null;
                if (opt["item"])
                {
                    notify?.Invoke(messages.Get(itemPhase));

                    AnnotationData ann = container.GetInstance<AnnotationData>();
                    ann.Load(opt, itemPreset: itemPreset, merge: merge);
#if DEV
                    container.Register<EldenItemDump>();
                    if (container.GetInstance<EldenItemDump>().Run())
                    {
                        return;
                    }
#endif
                    ann.ProcessRestrictions(opt, null);
                    ann.AddSpecialItems();
                    ann.AddMaterialItems(opt["mats"]);
                    // This isn't very useful normally
                    if (Omit && opt["fog"] && opt["crawl"]) ann.WeaponSpam(game);
                    if (opt["copydrops"])
                    {
                        ann.AddFiniteEquipmentCopies();
                    }
                    if (opt["shrooms"])
                    {
                        ann.AddRandomaniaItems();
                    }

                    Random random = new Random(seed);
                    Permutation perm = container.GetInstance<Permutation>();
                    perm.LoadLocations(opt, merge);
                    perm.Logic(random, opt, enemyPreset);

                    notify?.Invoke(messages.Get(editPhase));
                    random = new Random(seed + 1);
                    permResult = container.GetInstance<PermutationWriter>().Write(random, perm, opt);

                    if (opt["markareas"])
                    {
                        container.GetInstance<HintMarker>().Write(opt, perm, permResult);
                    }
                    if (opt["mats"])
                    {
                        container.GetInstance<EldenMaterialRandomizer>().Randomize(opt, perm);
                    }
                }
                else
                {
                    // Still have to load for CharacterWriter
                    AnnotationData ann = container.GetInstance<AnnotationData>();
                    ann.Load(opt);
                }

                // Handling for misc options
                container.GetInstance<CharacterWriter>().Write(trueSeed, opt);
                // Parallel logic for DLC full-weapon upgrade which doesn't go through PermutationWriter.Write and doesn't use item configs
                if (!opt["item"] && opt["dlc"] && opt["maxweapon"])
                {
                    container.GetInstance<PermutationWriter>().AutoUpgradeDlc();
                }

                if (opt["enemy"])
                {
                    notify?.Invoke(messages.Get(enemyPhase));

                    EnemyRandomizer.Result enemyLocs = container.GetInstance<EnemyRandomizer>().Run(opt, enemyPreset);

                    if (enemyLocs != null && !opt["nooutfits"])
                    {
                        // This doesn't strictly need to be part of CharacterWriter, but makes similar edits
                        container.GetInstance<CharacterWriter>().SetSpecialOutfits(opt, enemyLocs);
                    }
                }
#if DEV
                if (!header) return;
#endif

                if (!opt["nogestures"])
                {
                    new GestureRandomizer(game).Randomize(opt);
                }
                EldenMiscSetup.EldenCommonPass(game, opt, messages, events, permResult);

                if (!opt["dryrun"])
                {
                    notify?.Invoke(messages.Get(savePhase));
                    object notifyLock = new object();
                    string options = $"Produced by Elden Ring Randomizer {EldenVersion} by thefifthmatt. Do not distribute. Options and seed: {opt}";
                    int mapPercent = -1;
                    void notifyMap(double val)
                    {
                        lock (notifyLock)
                        {
                            int percent = (int)Math.Floor(val * 100);
                            if (percent > mapPercent && percent <= 100)
                            {
#if !DEBUG
                                notify?.Invoke(messages.Get(saveMapPhase, percent));
#endif
                                mapPercent = percent;
                            }
                        }
                    }
                    game.WriteFMGs = true;
                    messages.SetFMGEntry(
                        game, FMGCategory.Menu, "EventTextForMap",
                        RuntimeParamChecker.RestartMessageId, restartMsg);

                    game.SaveEldenRing(outPath, opt["uxm"], options, events, notifyMap);
                }
                return;
            }
#endif
            // Experimental randomizers
#if DS1
            if (game.DS1)
            {
                // DS1 basically always needs to be scaled due to Asylum, so force this here. noscale turns off most scaling aside from earlyscale
                opt["scale"] = true;

                notify?.Invoke(messages.Get(enemyPhase));

                string emedfPath = @"dist1\Base\ds1-common.emedf.json";
                Events events = new Events(emedfPath, darkScriptMode: true, paramAwareMode: true, skipAwareMode: true);
                container.RegisterInstance(events);
                EventConfig enemyConfig = Util.ReadYamlFile<EventConfig>($@"{game.Dir}\Base\events.txt");
                container.RegisterInstance(enemyConfig);

                container.GetInstance<EnemyRandomizer>().Run(opt, enemyPreset);
                DS1MiscSetup.DS1CommonPass(game, opt, messages);
#if DEV
                if (!header) return;
#endif
                if (!opt["dryrun"])
                {
                    notify?.Invoke(messages.Get(savePhase));
                    game.SaveDS1(outPath, gameDir, opt, events);
                }
                return;
            }
#endif
#if NR
            if (game.Nightreign)
            {
                container.Register<NightreignRandomizer>();
                if (opt["map"])
                {
                    notify?.Invoke(messages.Get(mapPhase));
                    container.GetInstance<NightreignRandomizer>().Randomize(opt);
                }
                NightreignMiscSetup.NightreignCommonPass(game, opt, messages);
                if (!opt["dryrun"])
                {
                    notify?.Invoke(messages.Get(savePhase));
                    game.SaveNightreign(outPath);
                }
                return;
            }
#endif
#if AC6
            if (game.AC6)
            {
#if DEV
                LocationData data = null;
                if (opt["item"])
                {
                    notify?.Invoke(messages.Get(itemPhase));

                    AC6LocationDataScraper scraper = new AC6LocationDataScraper();
                    data = scraper.FindItems(game, opt);
                }
                return;
#endif
            }
#endif
            throw new Exception($"Internal error: {type}");
        }
    }
}
