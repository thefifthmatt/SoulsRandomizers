using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class DS3MiscSetup
    {
        // This process includes reading from BHDs, just write/done requires info about game directory and mods
        public static void CombineDS3SFX(
            IEnumerable<string> maps,
            MergedMods mods,
            string gameDir,
            string outDir,
            Action<string, BND4> write = null)
        {
            List<string> mapPrefixes = maps.SelectMany(m => new[] { m.Substring(0, 3) }).Distinct().Concat(new[] { "dlc1", "dlc2" }).ToList();
            List<string> outFiles = new() { "frpg_sfxbnd_commoneffects_effect.ffxbnd.dcx", "frpg_sfxbnd_commoneffects_resource.ffxbnd.dcx" };
            outFiles.AddRange(mapPrefixes.Select(map => $"frpg_sfxbnd_{map}_effect.ffxbnd.dcx"));

            List<string> missingFiles = outFiles.Where(name => !File.Exists($@"{outDir}\{name}")).ToList();
            if (missingFiles.Count == 0)
            {
                Console.WriteLine($"Missing {missingFiles.Count}/{outFiles.Count} sfx files, so skipping sfx merge");
                return;
            }
            else
            {
                Console.WriteLine($"Missing {missingFiles.Count}/{outFiles.Count} sfx files, so running sfx merge");
            }

            // First gather all required files, use mapPrefixes to merge all
            List<string> resourceMaps = new() { "m31", "m51" };
            HashSet<string> resourceFiles = new()
            {
                // Greatwood balls
                "s15810.flver",
                // Judicator summons (resources not in commoneffects)
                "s08240.anibnd", "s08240.flver", "s11657.tpf", "s11664.tpf",
            };
            List<string> bdtReadFiles = outFiles.Select(name => $@"/sfx/{name}").ToList();
            bdtReadFiles.AddRange(resourceMaps.Select(map => $"/sfx/frpg_sfxbnd_{map}_resource.ffxbnd.dcx"));
            Dictionary<string, string> bdtArchivePaths = bdtReadFiles.ToDictionary(f => f, _ => "Data1");
            Dictionary<string, byte[]> bdtFiles = BhdExtractor.ReadBdtFiles(GameSpec.FromGame.DS3, gameDir, bdtArchivePaths);
            byte[] read(string name)
            {
                if (mods.Resolve($@"sfx\{name}", out string modPath))
                {
                    return File.ReadAllBytes(modPath);
                }
                else if (bdtFiles.TryGetValue($"/sfx/{name}", out byte[] data))
                {
                    return data;
                }
                else throw new Exception($"Internal error: cannot find {name} to merge sfx");
            }
            // DS3 files are 6 MB and 295 MB respectively, so, we need a more selective strategy for resources.
            foreach (bool effect in new[] { true, false })
            {
                string suffix = effect ? "_effect" : "_resource";
                string commonName = $"frpg_sfxbnd_commoneffects{suffix}.ffxbnd.dcx";
                BND4 sfxCommon = BND4.Read(read(commonName));
                HashSet<string> sfxFiles = new HashSet<string>(sfxCommon.Files.Select(f => f.Name));
                foreach (string map in effect ? mapPrefixes : resourceMaps)
                {
                    string path = $"frpg_sfxbnd_{map}{suffix}.ffxbnd.dcx";
                    BND4 sfx = BND4.Read(read(path));
                    if (effect)
                    {
                        sfx.Files = sfx.Files.Where(file =>
                        {
                            if (!sfxFiles.Contains(file.Name))
                            {
                                sfxCommon.Files.Add(file);
                                sfxFiles.Add(file.Name);
                                return false;
                            }
                            else
                            {
                                return false;
                            }
                        }).ToList();
                        write($"frpg_sfxbnd_{map}{suffix}.ffxbnd.dcx", sfx);
                    }
                    else
                    {
                        sfx.Files.ForEach(file =>
                        {
                            string fileName = Path.GetFileName(file.Name);
                            // Just merge everything, the game can probably handle it
                            // if (!resourceFiles.Contains(fileName)) return;
                            if (!sfxFiles.Contains(file.Name))
                            {
                                sfxCommon.Files.Add(file);
                                sfxFiles.Add(file.Name);
                                resourceFiles.Remove(fileName);
                            }
                        });
                    }
                }
                if (!effect && resourceFiles.Count > 0)
                {
                    throw new Exception($@"Required resource files not found for sfx merge: {string.Join(", ", resourceFiles)}");
                }
                int startId = 0;
                foreach (BinderFile file in sfxCommon.Files)
                {
                    // Ignore prefixes here
                    file.ID = startId++;
                }
                sfxCommon.Files.Sort((a, b) => a.ID.CompareTo(b.ID));
                write($"frpg_sfxbnd_commoneffects{suffix}.ffxbnd.dcx", sfxCommon);
            }
            return;
        }

        public static void DS3CommonPass(GameData game, Events events, RandomizerOptions opt)
        {
            EMEVD.Event fogEvent = game.Emevds["m40_00_00_00"].Events.Find(e => e.ID == 14005102);
            if (fogEvent != null)
            {
                // Small convenience: Shorten the Firelink Shrine fog gate wait times significantly
                foreach (EMEVD.Instruction i in fogEvent.Instructions)
                {
                    // TODO: This depends on paramAwareMode being off
                    Instr instr = events.Parse(i);
                    if (instr.Name == "IfElapsedSeconds" && instr[1] is float wait)
                    {
                        instr[1] = Math.Min(wait, 2f);
                        instr.Save();
                    }
                }
            }
#if DEBUG
            // Easier verification for enemy randomizer stuff
            if (opt["cheat_shortcut"])
            {
                // Various shortcut flags
                List<int> flags = new List<int>
                {
                    63100420, // Settlement Greatwood
                    63100470, // Settlement sewers
                    13010461, // Lothric->Archives elevator
                    63300432, // Farron Keep shortcut
                    13410451, // Archives elevator
                    13500431, // Cathedral elevator
                    63500210, 63500211, 63500212, 63500213, // Cathedral wooden doors
                    63500221, // Cathedral gate 1
                    63500220, // Cathedral gate 2
                    63700460, // Irithyll Pontiff shortcut
                    63900440, // Dungeon->Archdragon door 1
                    63900430, // Dungeon->Archdragon door 2
                    13900401, // Dungeon->Archdragon warp elevator
                    64500572, // Ariandel basement
                    65100610, // Ringed City
                };
                game.Emevds["common"].Events[0].Instructions.AddRange(
                    flags.Select(f => events.ParseAdd($"SetEventFlag({f}, ON)")));
                EMEVD.Event warpEvent = game.Emevds["m39_00_00_00"].Events.Find(e => e.ID == 13905900);
                if (warpEvent != null)
                {
                    EventEdits edits = new EventEdits();
                    events.RemoveMacro(edits, "4400");
                    events.ApplyAllEdits(warpEvent, edits);
                }
            }
            if (opt["cheatinv"])
            {
                AddSimpleEvent(game.Emevds["common"], 19003107, new List<EMEVD.Instruction>
                {
                    // Disable damage
                    new EMEVD.Instruction(2004, 39, new List<object> { 10000, 0 }),
                    // WaitFixedTimeSeconds
                    new EMEVD.Instruction(1001, 0, new List<object> { (float)2 }),
                    // EndUnconditionally(EventEndType.Restart)
                    new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                });
            }
            if (opt["cheatimm"])
            {
                AddSimpleEvent(game.Emevds["common"], 19003107, new List<EMEVD.Instruction>
                {
                    new EMEVD.Instruction(2004, 12, new List<object> { 10000, 1 }),
                    // WaitFixedTimeSeconds
                    new EMEVD.Instruction(1001, 0, new List<object> { (float)2 }),
                    // EndUnconditionally(EventEndType.Restart)
                    new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                });
            }
#endif
        }
    }
}