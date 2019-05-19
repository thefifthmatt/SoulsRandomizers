using System;
using System.IO;

namespace DS3StaticItemRandomizer
{
    public class Randomizer
    {
        private static Properties.Settings settings = Properties.Settings.Default;

        public void Randomize(RandomizerOptions options, Action<string> notify=null, string outPath = ".", string hintLogPath=null)
        {
            string distDir = "dist";
            if (!Directory.Exists(distDir))
            {
                // From Release/Debug dirs
                distDir = @"..\..\..\dist";
            }
            if (!Directory.Exists(distDir))
            {
                throw new Exception("Missing data directory");
            }
            GameData game = new GameData(distDir);

            Console.WriteLine($"Options and seed: {options}");
            Console.WriteLine();
            int seed = (int)options.Seed;

            notify?.Invoke("Loading game data");
            game.Load();

            // SekiroLocationDataScraper scraper = new SekiroLocationDataScraper();
            LocationDataScraper scraper = new LocationDataScraper(logUnused:false);
            LocationData data = scraper.FindItems(game);
            AnnotationData ann = new AnnotationData(game, data);
            ann.Load(options);
            ann.AddSpecialItems();

            notify?.Invoke("Randomizing");
            Random random = new Random(seed);
            Permutation permutation = new Permutation(game, data, ann, explain:false);
            permutation.Logic(random, options);

            notify?.Invoke("Editing game files");
            random = new Random(seed + 1);
            PermutationWriter writer = new PermutationWriter(game, data, ann);
            writer.Write(random, permutation);
            random = new Random(seed + 2);
            CharacterWriter characters = new CharacterWriter(game, data);
            characters.Write(random, options);

            notify?.Invoke("Writing game files");
            if (!options["dryrun"])
            {
                game.Save(outPath, editMaps:!options["unreliableenemyplacement"]);
            }
        }
    }
}