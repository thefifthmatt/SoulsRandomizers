using System;
using System.Collections.Generic;
using System.Linq;
using SoulsFormats;
using SoulsIds;
using static SoulsFormats.EMEVD.Instruction;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class GestureRandomizer
    {
        private GameData game;

        public GestureRandomizer(GameData game)
        {
            this.game = game;
        }

        public void Randomize(RandomizerOptions opt)
        {
            // Elden Ring-specific currently.
            // For now, keep this dead simple with a bit of hardcoding, rather than denormalizing it out into a config.
            // The Ring is annoying but it's very noticable if not randomized, so change both versions to the same one.
            // (This could mess stuff up with save file checking etc. or exe hardcoding, but it shouldn't be too bad.)
            // Finger Snap blocks an optional and easily missable quest so keep it the same.
            // Erudition is entirely optional if horse parkour is allowed.
            // Default starting gestures are useful for multiplayer (seamless coop) but it shouldn't be a huge loss to change them.
            bool debug = false;

            // Finger Snap, The Ring 2 (rewritten) handled below
            HashSet<int> norandom = new HashSet<int> { 73, 109 };
            // Simple shuffle randomization
            SortedSet<int> sourceGestures = new SortedSet<int>();
            Dictionary<int, PARAM.Row> gestureGoods = game.Params["EquipParamGoods"].Rows
                .Where(r => r.ID >= 9000 && r.ID < 9100)
                .ToDictionary(r => r.ID, r => r);
            Dictionary<int, string> gestureNames = new Dictionary<int, string>();
            FMG goodsFmg = game.ItemFMGs["GoodsName"];
            foreach (PARAM.Row row in game.Params["GestureParam"].Rows)
            {
                if (norandom.Contains(row.ID)) continue;
                int good = (int)row["itemId"].Value;
                // No-good gestures are typically invalid
                if (!gestureGoods.TryGetValue(good, out PARAM.Row goodRow)) continue;
                // Reset these, only re-disabling them for starting gestures
                goodRow["showLogCondType"].Value = (byte)1;
                goodRow["showDialogCondType"].Value = (byte)2;
                sourceGestures.Add(row.ID);
                string name = goodsFmg[goodRow.ID];
                if (string.IsNullOrEmpty(name)) name = $"Unknown {row.ID}";
                gestureNames[row.ID] = name;
            }
            List<int> targetGestures = sourceGestures.ToList();
            Shuffle(new Random((int)opt.Seed + 14), targetGestures);
            int targetIndex = 0;
            // From source to destination gesture
            Dictionary<int, int> mapping = new Dictionary<int, int>();
            Console.WriteLine("-- Gesture placements");
            foreach (int source in sourceGestures)
            {
                int target = targetGestures[targetIndex++];
                mapping[source] = target;
                Console.WriteLine($"Replacing {gestureNames[target]}: {gestureNames[source]}");
            }
            Console.WriteLine();
            if (mapping.TryGetValue(108, out int ringDest)) mapping[109] = ringDest;

            // For the most part, gestures can be rewritten in-place, aside from init passes.
            // Keep track of popups to hide
            HashSet<int> startingGestures = new HashSet<int>();

            // Starting classes, even the fake display ones
            foreach (PARAM.Row row in game.Params["CharaInitParam"].Rows)
            {
                if (!(row.ID >= 3000 && row.ID < 4000)) continue;
                for (int i = 0; i < 6; i++)
                {
                    int gesture = (sbyte)row[$"gestureId{i}"].Value;
                    if (mapping.TryGetValue(gesture, out int target))
                    {
                        row[$"gestureId{i}"].Value = (sbyte)target;
                        // These should never be given directly, though allow popup if they are, perhaps
                        // startingGestures.Add(target);
                        if (debug) Console.WriteLine($"{row.ID}: chara {gesture} -> {target}");
                    }
                }
            }

            // Map from event id to arg positions of gestures
            Dictionary<int, SortedSet<int>> gestureCalls = new Dictionary<int, SortedSet<int>>();
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event ev in entry.Value.Events)
                {
                    for (int j = 0; j < ev.Instructions.Count; j++)
                    {
                        EMEVD.Instruction ins = ev.Instructions[j];
                        if (ins.Bank == 2003 && (ins.ID == 71 || ins.ID == 81))
                        {
                            EMEVD.Parameter p = ev.Parameters.Find(q => q.InstructionIndex == j);
                            if (p == null)
                            {
                                List<object> args = ins.UnpackArgs(new[] { ArgType.Int32 });
                                int gesture = (int)args[0];
                                if (mapping.TryGetValue(gesture, out int target))
                                {
                                    args[0] = target;
                                    ins.PackArgs(args);
                                    game.WriteEmevds.Add(entry.Key);
                                    if (ev.ID == 701)
                                    {
                                        // These are given at the start of the game here, so make them silent.
                                        // They are the only silent base game gestures.
                                        startingGestures.Add(target);
                                    }
                                    if (debug) Console.WriteLine($"{entry.Key}: evd {gesture} -> {target}");
                                }
                            }
                            else
                            {
                                // Unfortunately, some appear in common_func, so all initializations everywhere must be checked.
                                AddMulti(gestureCalls, (int)ev.ID, (int)p.SourceStartByte / 4);
                            }
                        }
                    }
                }
            }
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event ev in entry.Value.Events)
                {
                    for (int j = 0; j < ev.Instructions.Count; j++)
                    {
                        EMEVD.Instruction ins = ev.Instructions[j];
                        if (ins.Bank == 2000 && (ins.ID == 0 || ins.ID == 6))
                        {
                            // TODO: Put this in a utility to avoid doing UnpackArgs so much everywhere.
                            // The offset logic needs to be general for other games, should generalize it there.
                            int offset = ins.ID == 0 ? 2 : 2;
                            int eventId = BitConverter.ToInt32(ins.ArgData, (offset - 1) * 4);
                            if (gestureCalls.TryGetValue(eventId, out SortedSet<int> indices))
                            {
                                List<object> args = ins.UnpackArgs(Enumerable.Repeat(ArgType.Int32, ins.ArgData.Length / 4));
                                foreach (int index in indices)
                                {
                                    int gesture = (int)args[offset + index];
                                    if (mapping.TryGetValue(gesture, out int target))
                                    {
                                        args[offset + index] = target;
                                        if (debug) Console.WriteLine($"{entry.Key}: param {gesture} -> {target}");
                                    }
                                }
                                ins.PackArgs(args);
                                game.WriteEmevds.Add(entry.Key);
                            }
                        }
                    }
                }
            }
            IEnumerable<ESD.Condition> GetConditions(List<ESD.Condition> condList) => Enumerable.Concat(condList, condList.SelectMany(cond => GetConditions(cond.Subconditions)));
            foreach (KeyValuePair<string, Dictionary<string, ESD>> entry in game.Talk)
            {
                foreach (KeyValuePair<string, ESD> esdEntry in entry.Value)
                {
                    ESD esd = esdEntry.Value;
                    foreach ((int, int, ESD.State) stateDesc in esd.StateGroups.SelectMany(stateGroup => stateGroup.Value.Select(state => (stateGroup.Key, state.Key, state.Value))))
                    {
                        (int groupId, int id, ESD.State state) = stateDesc;
                        // TODO make a general framework to scan these upfront, it's a bit intensive otherwise
                        foreach (ESD.CommandCall cmd in new[] { state.EntryCommands, state.WhileCommands, state.ExitCommands, GetConditions(state.Conditions).SelectMany(c => c.PassCommands) }.SelectMany(c => c))
                        {
                            if (cmd.CommandBank == 1 && cmd.CommandID == 131 && cmd.Arguments.Count == 1)
                            {
                                // Unlike lots, these are non-int gestures used in commands
                                if (AST.DisassembleExpression(cmd.Arguments[0]).TryAsInt(out int gesture) && mapping.TryGetValue(gesture, out int target))
                                {
                                    cmd.Arguments[0] = AST.AssembleExpression(AST.MakeVal(target));
                                    game.WriteESDs.Add(entry.Key);
                                    if (debug) Console.WriteLine($"{entry.Key}: esd {gesture} -> {target}");
                                }
                            }
                        }
                    }
                }
            }

            foreach (PARAM.Row row in game.Params["GestureParam"].Rows)
            {
                if (!startingGestures.Contains(row.ID)) continue;
                int good = (int)row["itemId"].Value;
                if (!gestureGoods.TryGetValue(good, out PARAM.Row goodRow)) continue;
                // No popups on start
                goodRow["showLogCondType"].Value = (byte)0;
                goodRow["showDialogCondType"].Value = (byte)0;
            }
        }
    }
}
