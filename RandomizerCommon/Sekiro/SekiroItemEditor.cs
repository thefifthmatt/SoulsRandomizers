using LightInject;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static RandomizerCommon.EventConfig;
using static RandomizerCommon.ItemEventConfig;
using static RandomizerCommon.ItemLocEditor;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class SekiroItemEditor : ItemEditor
    {
        private readonly GameData game;
        private readonly LocationData data;
        private readonly ItemLocEditor locEditor;
        private readonly Events events;
        private readonly ItemEventConfig eventConfig;

        public SekiroItemEditor(
            GameData game,
            LocationData data,
            ItemLocEditor locEditor,
            Events events,
            ItemEventConfig eventConfig)
        {
            this.game = game;
            this.data = data;
            this.locEditor = locEditor;
            this.events = events;
            this.eventConfig = eventConfig;
        }

        public override void EditLocations(RandomizerOptions opt, ItemFlagMapping mapping)
        {
            // Sekiro edits
            // This needs to be migrated to AST/ESDEdits
            Dictionary<string, Dictionary<string, ESD>> talks = game.Talk;
            Dictionary<int, ItemEventSpec> talkTemplates = eventConfig.ItemTalks.ToDictionary(e => (int)e.ID, e => e);
            bool parseMachineName(string mIdStr, out int mId)
            {
                if (!int.TryParse(mIdStr, out mId))
                {
                    if (mIdStr.StartsWith("x") && int.TryParse(mIdStr.Substring(1), out int diffpart))
                    {
                        mId = 0x7FFFFFFF - diffpart;
                    }
                    else
                    {
                        return false;
                    }
                }
                return true;
            }
            bool debugEsd = false;
            foreach (KeyValuePair<string, ESD> entry in talks.SelectMany(s => s.Value))
            {
                void rewriteCondition(ESD.Condition cond, Action<byte[]> rewriteExpr)
                {
                    rewriteExpr(cond.Evaluator);
                    cond.PassCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                    cond.Subconditions.ForEach(c => rewriteCondition(c, rewriteExpr));
                }
                void rewriteCommand(ESD.CommandCall cmd, Action<byte[]> rewriteExpr)
                {
                    cmd.Arguments.ForEach(rewriteExpr);
                }
                string esdName = entry.Key;
                if (esdName.StartsWith("t") && int.TryParse(esdName.Substring(1), out int esdId) && talkTemplates.TryGetValue(esdId, out ItemEventSpec spec))
                {
                    ESD esd = entry.Value;
                    Dictionary<int, int> replaceInts = new Dictionary<int, int>();
                    HashSet<int> machines = new HashSet<int>();
                    int tearsMachine = -1;
                    foreach (ItemFlagTemplate t in spec.FlagTemplate)
                    {
                        List<int> templateMachines = new List<int>();
                        foreach (string machineStr in PhraseRe.Split(t.Machine))
                        {
                            int machine = parseMachineName(machineStr, out int mId) ? mId : throw new Exception($"Unknown machine id {t.Machine} of {esdName}");
                            templateMachines.Add(machine);
                        }
                        machines.UnionWith(templateMachines);
                        if (t.EventFlag != null)
                        {
                            int flag = int.Parse(t.EventFlag);
                            if (t.Type == "loc")
                            {
                                if (mapping.RewrittenFlags.TryGetValue(flag, out int newFlag))
                                {
                                    replaceInts[flag] = newFlag;
                                }
                            }
                            else if (t.Type == "isshin")
                            {
                                replaceInts[flag] = int.Parse(t.Replace);
                            }
                            else if (opt["extraitems"] && t.Type == "extraitems")
                            {
                                replaceInts[flag] = int.Parse(t.Replace);
                            }
                            else if (opt["splitskills"] && t.Type == "splitskills")
                            {
                                replaceInts[flag] = int.Parse(t.Replace);
                            }
                        }
                        if (opt["extraitems"] && t.Type == "tears")
                        {
                            tearsMachine = templateMachines[0];
                        }
                    }
                    if (replaceInts.Count == 0 && tearsMachine == -1)
                    {
                        continue;
                    }
                    void rewriteExpr(byte[] b)
                    {
                        // For now, these can be rewritten in place. If AST were used instead, it could not be in place, as lengths may change.
                        foreach (KeyValuePair<int, int> replace in replaceInts)
                        {
                            int search = SearchInt(b, (uint)replace.Key);
                            if (search != -1)
                            {
                                if (debugEsd) Console.WriteLine($"In ESD {esdName}, replacing {replace.Key} -> {replace.Value}");
                                Array.Copy(BitConverter.GetBytes(replace.Value), 0, b, search, 4);
                            }
                        }
                    }
                    foreach (KeyValuePair<long, Dictionary<long, ESD.State>> machine in esd.StateGroups)
                    {
                        if (!machines.Contains((int)machine.Key)) continue;
                        foreach (KeyValuePair<long, ESD.State> stateEntry in machine.Value)
                        {
                            ESD.State state = stateEntry.Value;
                            state.Conditions.ForEach(c => rewriteCondition(c, rewriteExpr));
                            state.EntryCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                            state.ExitCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                            state.WhileCommands.ForEach(c => rewriteCommand(c, rewriteExpr));
                            if ((int)machine.Key == tearsMachine)
                            {
                                // The ESDLang translation of the base game item checks are:
                                //
                                // AddTalkListData(1, 14000100, -1)
                                // c1_19 (41 a1, 82 e4 9f d5 00 a1, 3f a1)
                                //
                                // AddTalkListDataIf(ComparePlayerInventoryNumber(3, 2503, 2, 0, 0) == 1, 2, 14000101, -1)
                                // c5_19 (6f 43 82 c7 09 00 00 42 40 40 89 41 95 a1, 42 a1, 82 e5 9f d5 00 a1, 3f a1)
                                //
                                // AddTalkListDataIf(ComparePlayerInventoryNumber(3, 9091, 2, 0, 0) == 1, 3, 14000102, -1)
                                // c5_19 (6f 43 82 83 23 00 00 42 40 40 89 41 95 a1, 43 a1, 82 e6 9f d5 00 a1, 3f a1)

                                byte[] tearsHave = new byte[] { 0x6f, 0x43, 0x82, 0x28, 0x23, 0x00, 0x00, 0x42, 0x40, 0x40, 0x89, 0x41, 0x95, 0xa1 };
                                byte[] tearsLack = new byte[] { 0x6f, 0x43, 0x82, 0x28, 0x23, 0x00, 0x00, 0x42, 0x40, 0x40, 0x89, 0x40, 0x95, 0xa1 };
                                bool madeEdit = false;
                                foreach (ESD.CommandCall cmd in state.EntryCommands)
                                {
                                    // AddTalkListData
                                    if (cmd.CommandID != 19) continue;
                                    // A bit hacky, but just do this inline. Change every condition to also depend on dragon tears, plus add a default one.
                                    if (debugEsd) Console.WriteLine($"tearsA {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                    if (cmd.CommandBank == 1)
                                    {
                                        cmd.CommandBank = 5;
                                        cmd.Arguments.Insert(0, tearsHave);
                                    }
                                    else
                                    {
                                        List<byte> cond = cmd.Arguments[0].Where(b => b != 0xa1).ToList();
                                        cond.AddRange(tearsHave.Where(b => b != 0xa1));
                                        cond.Add(0x98);  // &&
                                        cond.Add(0xa1);  // end
                                        cmd.Arguments[0] = cond.ToArray();
                                    }
                                    if (debugEsd) Console.WriteLine($"tearsB {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                    madeEdit = true;
                                }
                                if (madeEdit)
                                {
                                    // Use dialogue id 14025002, "Do nothing"
                                    ESD.CommandCall cmd = new ESD.CommandCall(5, 19, tearsLack, new byte[] { 0x48, 0xa1 }, new byte[] { 0x82, 0x2a, 0x01, 0xd6, 0x00, 0xa1 }, new byte[] { 0x3f, 0xa1 });
                                    if (debugEsd) Console.WriteLine($"add {stateEntry.Key} {cmd.CommandBank}:{cmd.CommandID}: {string.Join(", ", cmd.Arguments.Select(a => string.Join(" ", a.Select(b => $"{b:x2}"))))}");
                                    state.EntryCommands.Add(cmd);
                                }
                            }
                        }
                    }
                }
            }

            Dictionary<string, EMEVD> emevds = game.Emevds;
            Dictionary<uint, ItemEventSpec> templates = eventConfig.ItemEvents.ToDictionary(e => e.ID, e => e);

            HashSet<ItemFlagTemplate> completedTemplates = new HashSet<ItemFlagTemplate>();
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                HashSet<long> ids = new HashSet<long>();
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    if (ids.Contains(e.ID)) Console.WriteLine($"Duplicate!!! {e.ID} in {entry.Key}");
                    ids.Add(e.ID);
                }
                Dictionary<uint, EMEVD.Event> fileEvents = entry.Value.Events.ToDictionary(e => (uint)e.ID, e => e);
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    OldParams initOld = OldParams.Preprocess(e);
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr init = events.Parse(e.Instructions[i], initOld);
                        if (!init.Init) continue;
                        uint callee = init.Callee;
                        if (!templates.TryGetValue(callee, out ItemEventSpec ev)) continue;
                        if (ev.FlagTemplate.Count == 0) throw new Exception($"event {callee} has no templates");
                        // Types: item, loc, any, remove
                        if (ev.FlagTemplate[0].Type == "remove")
                        {
                            // Remove action by removing initialization, for now. Can garbage collect later if desired.
                            e.Instructions[i] = new EMEVD.Instruction(1014, 69);
                            continue;
                        }
                        // Source flag and event to edit
                        List<(int, EMEVD.Event, ItemFlagTemplate)> eventCopies = new List<(int, EMEVD.Event, ItemFlagTemplate)>();
                        foreach (ItemFlagTemplate t in ev.FlagTemplate)
                        {
                            if (t.Type != "loc")
                            {
                                continue;
                            }
                            int argFlag = 0;
                            int flag;
                            if (events.ParseArgSpec(t.EventFlag, out int pos))
                            {
                                argFlag = (int)init[init.Offset + pos];
                                if (argFlag == 0) continue;
                                flag = argFlag;
                            }
                            else if (int.TryParse(t.EventFlag, out flag))
                            {
                                // Use parsed value
                            }
                            else throw new Exception($"Internal error: Could not extract item flag {t.EventFlag} from {callee}");
                            if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                            {
                                eventCopies.Add((flag, theEvent, t));
                            }
                            else if (argFlag != 0)
                            {
                                eventCopies.Add((flag, null, t));
                            }
                            else
                            {
                                Console.WriteLine($"{t.Type} - {t.EventFlag} - {flag} - {argFlag}");
                                throw new Exception($"Initialized event {callee} but absent from this file and not specified in args");
                            }
                        }
                        // Do in-place pass
                        foreach (ItemFlagTemplate t in ev.FlagTemplate)
                        {
                            if (t.Type != "loc")
                            {
                                if (t.IsDefault())
                                {
                                    // This is fine, nothing to do if not chr
                                }
                                else if (fileEvents.TryGetValue(callee, out EMEVD.Event theEvent))
                                {
                                    if (completedTemplates.Contains(t)) continue;
                                    completedTemplates.Add(t);
                                    eventCopies.Add((0, theEvent, t));
                                }
                                else if (t.Type == "carp")
                                {
                                    eventCopies.Add((0, null, t));
                                }
                                else throw new Exception($"Can't find event {callee} with {t.Type} template");
                            }
                        }
                        foreach (var copy in eventCopies)
                        {
                            (int flag, EMEVD.Event e2, ItemFlagTemplate t) = copy;
                            // Main entities to replace
                            Dictionary<int, int> reloc = new Dictionary<int, int>();
                            if (flag != 0)
                            {
                                if (mapping.RewrittenFlags.TryGetValue(flag, out int newFlag) && flag != newFlag)
                                {
                                    reloc[flag] = newFlag;
                                }
                                // TODO: Item lot replacement
                            }
                            if (t.Type == "carp")
                            {
                                if (!events.ParseArgSpec(t.Entity, out int entityPos)) throw new Exception($"{callee}");
                                if (!events.ParseArgSpec(t.ItemLot, out int lotPos)) throw new Exception($"{callee}");
                                if (data.NewEntityLots.TryGetValue((uint)(int)init[init.Offset + entityPos], out int newLot))
                                {
                                    reloc[(int)init[init.Offset + lotPos]] = newLot;
                                }
                            }
                            // Add all edits
                            EventEdits edits = new EventEdits();
                            if (t.Remove != null)
                            {
                                foreach (string remove in Regex.Split(t.Remove, @"\s*;\s*"))
                                {
                                    events.RemoveMacro(edits, remove);
                                }
                            }
                            if (t.Replace != null)
                            {
                                foreach (string replace in Regex.Split(t.Replace, @"\s*;\s*"))
                                {
                                    events.ReplaceMacro(edits, replace);
                                }
                            }
                            // Also edit conditional flags present in both the event and the target
                            if (flag > 0)
                            {
                                // TODO: rewrite flags to another value
                            }
                            if (t.RemoveArg != null)
                            {
                                if (!events.ParseArgSpec(t.RemoveArg, out int removePos)) throw new Exception($"{callee} {t.RemoveArg}");
                                init[init.Offset + removePos] = 0;
                            }

                            OldParams pre = e2 == null ? null : OldParams.Preprocess(e2);
                            if (e2 != null)
                            {
                                // TODO: check that all remove/replaces has been activated
                                // Also do startcmds here? Add any additional commands before any other processing
                                if (t.Add != null)
                                {
                                    events.AddMacro(edits, t.Add);
                                }
                                for (int j = 0; j < e2.Instructions.Count; j++)
                                {
                                    Instr instr = events.Parse(e2.Instructions[j], pre);
                                    // Randomized events shouldn't have initializations, although we could probably also ignore them
                                    if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e2.ID}");
                                    // We are either dealing with a copy of the event or the original one. So all edits are in-place
                                    // Remove/replace cases
                                    events.ApplyEdits(edits, instr, j);
                                    instr.Save(pre);
                                    e2.Instructions[j] = instr.Val;
                                }
                                events.ApplyAdds(edits, e2);
                            }
                            if (edits.PendingEdits.Count != 0)
                            {
                                throw new Exception($"{callee} has unapplied edits: {string.Join("; ", edits.PendingEdits)}");
                            }
                            // Also add regions and multichr
                            // How to handle startcmd? Answer: maybe don't...
                            if (reloc.Count > 0)
                            {
                                events.RewriteInts(init, reloc);
                                init.Save(pre);
                                if (e2 != null)
                                {
                                    for (int j = 0; j < e2.Instructions.Count; j++)
                                    {
                                        Instr instr = events.Parse(e2.Instructions[j], pre);
                                        if (instr.Init) throw new Exception($"Unexpected event initialization in template event {e.ID}");
                                        events.RewriteInts(instr, reloc);
                                        instr.Save(pre);
                                    }
                                }
                            }
                            if (pre != null)
                            {
                                pre.Postprocess();
                            }
                        }
                    }
                    initOld.Postprocess();
                }
                if (entry.Key == "common")
                {
                    // Copy event from Ashina Outskirts for getting grapple, to show green circle
                    EMEVD.Event grappleEvent = emevds["m11_00_00_00"].Events.Find(e => e.ID == 11100620);
                    if (grappleEvent != null)
                    {
                        grappleEvent = events.CopyEvent(grappleEvent, 929);
                        entry.Value.Events.Add(grappleEvent);
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 0, 929, 0 }));
                    }

                    // Add custom event and item lot for granting display-only memories
                    // Use unused lot 3440 and unused event flag range 930-950ish
                    ItemKey memory = new ItemKey(ItemType.Goods, 5400);
                    int memoryLot = 3440;  // unused lot
                    LotCells memCells = locEditor.LotCellsForItem(memory);
                    memCells.EventFlag = -1;
                    locEditor.AddLot("ItemLotParam", memoryLot, memCells);

                    EMEVD.Event memEv = new EMEVD.Event(930, EMEVD.Event.RestBehaviorType.Default);
                    memEv.Instructions.Add(new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, 0 }));  // End if self event flag + slot
                    memEv.Instructions.Add(new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)0, (byte)0, 0 }));  // If flag off
                    memEv.Instructions.Add(new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)1, (byte)0, 0 }));  // If flag on
                    memEv.Instructions.Add(new EMEVD.Instruction(2003, 4, new List<object> { memoryLot }));  // Grant item lot
                    memEv.Parameters.Add(new EMEVD.Parameter(1, 4, 0, 4));
                    memEv.Parameters.Add(new EMEVD.Parameter(2, 4, 0, 4));
                    entry.Value.Events.Add(memEv);

                    int slot = 0;
                    foreach (KeyValuePair<int, int> mem in mapping.MemoryFlags)
                    {
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { slot++, (uint)930, (uint)mem.Value }));
                    }

                    // Mibu Breathing Technique item in shops should also grant the skill
                    entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { 6, (uint)460, (uint)11100621, (uint)2420, (uint)610 }));

                    // Add permanent shop placement flags
                    // Don't use this mechanism for the time being, so NG+ works better
                    slot = 0;
                    foreach (KeyValuePair<int, int> shop in mapping.ShopPermanentFlags)
                    {
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { slot++, (uint)750, (uint)shop.Key, (uint)shop.Value }));
                    }
                }
            }
        }
    }
}
