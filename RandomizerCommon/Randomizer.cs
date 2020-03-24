using System;
using System.IO;

namespace RandomizerCommon
{
    public class Randomizer
    {
        private static Properties.Settings settings = Properties.Settings.Default;

        public void Randomize(RandomizerOptions options, Action<string> notify=null, string outPath = null, bool sekiro=false, Preset preset=null)
        {
            // sekiro = false;
            string distDir = sekiro ? "dists" : "dist";
            if (!Directory.Exists(distDir))
            {
                // From Release/Debug dirs
                distDir = $@"..\..\..\{distDir}";
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
                DirectoryInfo modDirInfo = new DirectoryInfo($@"{outPath}\..\mods");
                if (!modDirInfo.Exists) throw new Exception($"Can't merge mods: {modDirInfo.FullName} not found");
                modDir = modDirInfo.FullName;
            }
            GameData game = new GameData(distDir, sekiro);
            game.Load(modDir);
            // game.SearchParamInt(20000); return;
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

            // Slightly different high-level algorithm for each game. As always, can try to merge more in the future.
            if (sekiro)
            {
                Events events = new Events();

                EnemyLocations locations = null;
                if (options["enemy"])
                {
                    notify?.Invoke("Randomizing enemies");
                    locations = new EnemyRandomizer(game, events).Run(options, preset);
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
                    Permutation perm = new Permutation(game, data, anns, explain: false);
                    perm.Logic(new Random(seed), options, preset);

                    notify?.Invoke("Editing game files");
                    PermutationWriter write = new PermutationWriter(game, data, anns, events);
                    write.Write(new Random(seed + 1), perm, options);
                    if (!options["norandom_skills"])
                    {
                        SkillWriter skills = new SkillWriter(game, data, anns);
                        skills.RandomizeTrees(new Random(seed + 2), perm);
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
            else
            {
                LocationDataScraper scraper = new LocationDataScraper(logUnused: false);
                LocationData data = scraper.FindItems(game);
                AnnotationData ann = new AnnotationData(game, data);
                ann.Load(options);
                ann.AddSpecialItems();

                notify?.Invoke("Randomizing");
                Random random = new Random(seed);
                Permutation permutation = new Permutation(game, data, ann, explain: false);
                permutation.Logic(random, options, null);

                notify?.Invoke("Editing game files");
                random = new Random(seed + 1);
                PermutationWriter writer = new PermutationWriter(game, data, ann, null);
                writer.Write(random, permutation, options);
                random = new Random(seed + 2);
                CharacterWriter characters = new CharacterWriter(game, data);
                characters.Write(random, options);

                notify?.Invoke("Writing game files");
                if (!options["dryrun"])
                {
                    game.SaveDS3(outPath, editMaps: !options["unreliableenemyplacement"]);
                }
            }
        }
    }
}
