using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SoulsFormats;
using SoulsIds;
using YamlDotNet.Serialization;
using static RandomizerCommon.Util;
using static SoulsFormats.EMEVD.Instruction;

namespace RandomizerCommon
{
    public class Events
    {
        private static Dictionary<int, int> ArgLength = new Dictionary<int, int>
        {
            [0] = 1,
            [1] = 2,
            [2] = 4,
            [3] = 1,
            [4] = 2,
            [5] = 4,
            [6] = 4,
            [8] = 4,
        };
        // Normally 'out object' would be preferable, but this is only for internal config values, so skipping the boilerplate should be fine
        private static object ParseArg(string arg, ArgType type)
        {
            switch (type)
            {
                case ArgType.Byte:
                    return byte.Parse(arg);
                case ArgType.UInt16:
                    return ushort.Parse(arg);
                case ArgType.UInt32:
                    return uint.Parse(arg);
                case ArgType.SByte:
                    return sbyte.Parse(arg);
                case ArgType.Int16:
                    return short.Parse(arg);
                case ArgType.Int32:
                    return int.Parse(arg);
                case ArgType.Single:
                    return float.Parse(arg, CultureInfo.InvariantCulture);
                default:
                    throw new Exception($"Unrecognized arg type {type}");
            }
        }

        private EMEDF doc;
        private Dictionary<string, (int, int)> docByName;
        private Dictionary<EMEDF.InstrDoc, List<int>> funcBytePositions;
        // Take free event flags from Abandoned Dungeon
        private int tmpBase = 11315000; // 11305750;  // until 6000, then it's not tmp anymore
        private int maxTmp = 11396000;
        private int permBase = 11306000;  // until at least 7000
        private int maxPerm = 11307000;

        public readonly EventConfig Config;

        public Events()
        {
            doc = EMEDF.ReadFile(@"dists\Base\sekiro-common.emedf.json");
            docByName = doc.Classes.SelectMany(c => c.Instructions.Select(i => (i, (int)c.Index))).ToDictionary(i => i.Item1.Name, i => (i.Item2, (int)i.Item1.Index));
            funcBytePositions = new Dictionary<EMEDF.InstrDoc, List<int>>();
            foreach (EMEDF.ClassDoc bank in doc.Classes)
            {
                foreach (EMEDF.InstrDoc instr in bank.Instructions)
                {
                    int bytePos = 0;
                    foreach (EMEDF.ArgDoc arg in instr.Arguments)
                    {
                        int len = ArgLength[(int)arg.Type];
                        if (bytePos % len > 0) bytePos += len - (bytePos % len);
                        AddMulti(funcBytePositions, instr, bytePos);
                        bytePos += len;
                    }
                }
            }
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = File.OpenText("dists/Base/events.txt"))
            {
                Config = deserializer.Deserialize<EventConfig>(reader);
            }
        }

        // Instruction metadata
        public Instr Parse(EMEVD.Instruction instr, bool onlyCmd = false, bool onlyInit = false)
        {
            bool isInit = instr.Bank == 2000 && (instr.ID == 0 || instr.ID == 6);
            if (onlyCmd && isInit) return null;
            if (onlyInit && !isInit) return null;
            EMEDF.InstrDoc instrDoc = doc[instr.Bank][instr.ID];
            List<ArgType> argTypes = isInit
                ? Enumerable.Repeat(ArgType.Int32, instr.ArgData.Length / 4).ToList()
                : instrDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
            List<object> args = instr.UnpackArgs(argTypes);
            Instr ret = new Instr
            {
                Val = instr,
                Doc = instrDoc,
                Types = argTypes,
                Args = args,
                Init = isInit
            };
            if (isInit)
            {
                ret.Offset = instr.ID == 0 ? 2 : 1;
                ret.Callee = (int)args[instr.ID == 0 ? 1 : 0];
            }
            return ret;
        }

        public class Instr
        {
            // Actual instruction
            public EMEVD.Instruction Val { get; set; }
            public EMEDF.InstrDoc Doc { get; set; }
            public string Name => Doc?.Name;
            public List<ArgType> Types { get; set; }
            public List<object> Args { get; set; }
            // Whether an event initialization or not
            public bool Init { get; set; }
            // If an event initialization,
            public int Callee { get; set; }
            // If an event initialization, the index start of actual event arguments
            public int Offset { get; set; }
            // Dirty bit
            public bool Modified { get; set; }

            public void Save()
            {
                if (Modified)
                {
                    Val.PackArgs(Args);
                    Modified = false;
                }
            }

            public object this[int i]
            {
                get => Args[i];
                set {
                    if (value is string s)
                    {
                        if (s.StartsWith("X"))
                        {
                            // Allow this in the case of psuedo-variables representing event args. This instruction cannot be repacked in this case.
                            Args[i] = value;
                        }
                        else
                        {
                            Args[i] = ParseArg(s, Types[i]);
                        }
                    }
                    else
                    {
                        Args[i] = value;
                    }
                    Modified = true;
                }
            }

            public override string ToString() => $"{Name} ({string.Join(",", Args)})";
        }

        public int IndexFromByteOffset(Instr instr, int offset)
        {
            int paramIndex = funcBytePositions[instr.Doc].IndexOf(offset);
            if (paramIndex == -1) throw new Exception($"Finding {instr.Name}, target {offset}, available {string.Join(",", funcBytePositions[instr.Doc])}");
            return paramIndex;
        }

        // Copying
        public int NewID(bool temp)
        {
            int newId = temp ? tmpBase++ : permBase++;
            if (!IsTemp(tmpBase) && tmpBase % 10000 == 6000)
            {
                tmpBase -= 1000;
                tmpBase += 10000;
            }
            if (tmpBase > maxTmp || permBase > maxPerm) throw new Exception($"event {newId} hit event limit");
            return newId;
        }

        public bool IsTemp(int flag)
        {
            return (flag / 1000) % 10 == 5;
        }

        public EMEVD.Instruction CopyInstruction(EMEVD.Instruction i)
        {
            return i.Layer.HasValue ? new EMEVD.Instruction(i.Bank, i.ID, i.Layer.Value, i.ArgData) : new EMEVD.Instruction(i.Bank, i.ID, i.ArgData);
        }

        public EMEVD.Event CopyEvent(EMEVD.Event src)
        {
            int newId = NewID(IsTemp((int)src.ID));
            EMEVD.Event newEvent = new EMEVD.Event(newId, src.RestBehavior);
            if (src.Parameters.Count > 0)
            {
                newEvent.Parameters = src.Parameters.Select(p => new EMEVD.Parameter(p.InstructionIndex, p.TargetStartByte, p.SourceStartByte, p.ByteCount)).ToList();
            }
            newEvent.Instructions = src.Instructions.Select(i => CopyInstruction(i)).ToList();
            return newEvent;
        }

        public Instr CopyInit(Instr instr, EMEVD.Event newEvent)
        {
            Instr newInstr = Parse(CopyInstruction(instr.Val));
            if (newInstr.Val.Bank == 2000 && newInstr.Val.ID == 0)
            {
                if (newEvent == null) throw new Exception($"Internal error: Event not provided for copying {string.Join(",", instr.Args)}");
                newInstr[0] = 0;
                newInstr[1] = (uint)newEvent.ID;
            }
            return newInstr;
        }

        // Preserving parameters after adding/removing instructions
        public class OldParams
        {
            public EMEVD.Event Event { get; set; }
            public List<EMEVD.Instruction> Original { get; set; }

            public static OldParams Preprocess(EMEVD.Event e)
            {
                if (e.Parameters.Count == 0) return new OldParams();
                return new OldParams
                {
                    Event = e,
                    Original = e.Instructions.ToList(),
                };
            }

            public void Postprocess()
            {
                if (Event == null || Event.Parameters.Count == 0) return;
                Dictionary<EMEVD.Instruction, int> indices = Event.Instructions.Select((a, i) => (a, i)).ToDictionary(p => p.Item1, p => p.Item2);
                Event.Parameters = Event.Parameters.Select(p =>
                {
                    if (indices.TryGetValue(Original[(int)p.InstructionIndex], out int currentIndex))
                    {
                        p.InstructionIndex = currentIndex;
                        return p;
                    }
                    return null;
                }).Where(p => p != null).ToList();
            }
        }

        // Editing macros
        public class EventEdits
        {
            public Dictionary<string, InstrEdit> NameEdits { get; set; }
            public Dictionary<string, InstrEdit> ArgEdits { get; set; }
            public Dictionary<string, Dictionary<string, InstrEdit>> NameArgEdits { get; set; }
            public HashSet<InstrEdit> PendingEdits = new HashSet<InstrEdit>();
            public Dictionary<int, List<InstrEdit>> PendingAdds = new Dictionary<int, List<InstrEdit>>();

            // Returns all applicable edits
            public List<InstrEdit> GetMatches(Instr instr)
            {
                InstrEdit nameEdit = null;
                if (NameEdits != null && !NameEdits.TryGetValue(instr.Name, out nameEdit) && ArgEdits == null && NameArgEdits == null) return null;
                List<string> strArgs = instr.Args.Select(a => a.ToString()).ToList();
                List<InstrEdit> edits = new List<InstrEdit>();
                if (ArgEdits != null)
                {
                    edits.AddRange(strArgs.SelectMany(s => ArgEdits.TryGetValue(s, out InstrEdit edit) ? new[] { edit } : new InstrEdit[] { }));
                }
                if (nameEdit != null)
                {
                    edits.Add(nameEdit);
                }
                if (NameArgEdits != null && NameArgEdits.TryGetValue(instr.Name, out var args) && args.TryGetValue(string.Join(",", strArgs), out InstrEdit nameArgEdit))
                {
                    edits.Add(nameArgEdit);
                }
                return edits;
            }

            // Applies all edits that can be applied in place, and adds others later
            public void ApplyEdits(Instr instr, int index)
            {
                List<InstrEdit> edits = GetMatches(instr);
                if (edits == null) return;
                // Either apply edits or return them back
                bool removed = false;
                edits.ForEach(edit =>
                {
                    if (edit.Remove)
                    {
                        // For now, use inplace remove, a bit less messy
                        instr.Val = new EMEVD.Instruction(1014, 69);
                        instr.Init = false;
                        instr.Doc = null;
                        instr.Args.Clear();
                        instr.Types.Clear();
                        removed = true;
                    }
                    if (edit.Add != null)
                    {
                        AddMulti(PendingAdds, index, edit);
                    }
                    if (!removed)
                    {
                        if (edit.PosEdit != null)
                        {
                            foreach (KeyValuePair<int, string> pos in edit.PosEdit)
                            {
                                instr[pos.Key] = pos.Value;
                            }
                        }
                        if (edit.ValEdit != null)
                        {
                            for (int i = 0; i < instr.Args.Count; i++)
                            {
                                if (edit.ValEdit.TryGetValue(instr[i].ToString(), out string replace))
                                {
                                    instr[i] = replace;
                                }
                            }
                        }
                    }
                    if (edit.Add == null) PendingEdits.Remove(edit);
                });
            }

            public void AddEdit(string toFind, Predicate<string> docName, InstrEdit edit)
            {
                if (int.TryParse(toFind, out var _))
                {
                    if (ArgEdits == null) ArgEdits = new Dictionary<string, InstrEdit>();
                    ArgEdits[toFind] = edit;
                }
                else if (docName(toFind))
                {
                    // If this isn't a name, it will come up later as an unused pending edit
                    if (NameEdits == null) NameEdits = new Dictionary<string, InstrEdit>();
                    NameEdits[toFind] = edit;
                }
                else
                {
                    (string cmd, List<string> addArgs) = ParseCommandString(toFind);
                    if (NameArgEdits == null) NameArgEdits = new Dictionary<string, Dictionary<string, InstrEdit>>();
                    AddMulti(NameArgEdits, cmd, string.Join(",", addArgs), edit);
                }
                PendingEdits.Add(edit);
            }

            public void AddReplace(string toFind, string toVal = null)
            {
                InstrEdit edit = new InstrEdit();
                if (toVal != null || Regex.IsMatch(toFind, @"^\d+\s*->\s*\d+$"))
                {
                    string[] parts = toVal == null ? Regex.Split(toFind, @"\s*->\s*") : new[] { toFind, toVal };
                    edit.ValEdit = new Dictionary<string, string>();
                    edit.ValEdit[parts[0]] = parts[1];
                    if (ArgEdits == null) ArgEdits = new Dictionary<string, InstrEdit>();
                    ArgEdits[parts[0]] = edit;
                }
                else
                {
                    if (toVal != null) throw new Exception();
                    (string cmd, List<string> addArgs) = ParseCommandString(toFind);
                    edit.PosEdit = new Dictionary<int, string>();
                    for (int i = 0; i < addArgs.Count; i++)
                    {
                        if (addArgs[i].Contains("->"))
                        {
                            string[] parts = Regex.Split(addArgs[i], @"\s*->\s*");
                            addArgs[i] = parts[0];
                            edit.PosEdit[i] = parts[1];
                        }
                    }
                    if (NameArgEdits == null) NameArgEdits = new Dictionary<string, Dictionary<string, InstrEdit>>();
                    AddMulti(NameArgEdits, cmd, string.Join(",", addArgs), edit);
                }
                PendingEdits.Add(edit);
            }
        }

        public class InstrEdit
        {
            public EMEVD.Instruction Add { get; set; }
            public bool AddAfter { get; set; }
            public bool Remove { get; set; }
            public Dictionary<int, string> PosEdit { get; set; }
            public Dictionary<string, string> ValEdit { get; set; }
            public override string ToString() => $"Edit (Add {Add}, Remove {Remove}, PosEdit {(PosEdit == null ? "" : string.Join(",", PosEdit))}, ValEdit {(ValEdit == null ? "" : string.Join(",", ValEdit))})";
        }

        public void AddMacro(EventEdits edits, List<EventAddCommand> adds)
        {
            foreach (EventAddCommand add in adds)
            {
                if (add.Before == null && add.After == null)
                {
                    AddMacro(edits, null, true, add.Cmd);
                }
                else
                {
                    AddMacro(edits, add.After ?? add.Before, add.After != null, add.Cmd);
                }
            }
        }

        public void ApplyAdds(EventEdits edits, EMEVD.Event e)
        {
            // Add all commands in reverse order, to preserve indices
            foreach (KeyValuePair<int, List<InstrEdit>> lineEdit in edits.PendingAdds.OrderByDescending(item => item.Key))
            {
                if (lineEdit.Key == -1)
                {
                    foreach (InstrEdit addEdit in lineEdit.Value)
                    {
                        e.Instructions.Add(addEdit.Add);
                        edits.PendingEdits.Remove(addEdit);
                    }
                    continue;
                }
                foreach (InstrEdit addEdit in lineEdit.Value)
                {
                    if (addEdit.Add != null && addEdit.AddAfter)
                    {
                        e.Instructions.Insert(lineEdit.Key + 1, addEdit.Add);
                        edits.PendingEdits.Remove(addEdit);
                    }
                }
                foreach (InstrEdit addEdit in lineEdit.Value)
                {
                    if (addEdit.Add != null && !addEdit.AddAfter)
                    {
                        e.Instructions.Insert(lineEdit.Key, addEdit.Add);
                        edits.PendingEdits.Remove(addEdit);
                    }
                }
            }
        }

        public void AddMacro(EventEdits edits, string toFind, bool addAfter, string add)
        {
            InstrEdit edit = new InstrEdit
            {
                Add = ParseAdd(add),
                AddAfter = addAfter
            };
            if (toFind == null)
            {
                edits.PendingEdits.Add(edit);
                AddMulti(edits.PendingAdds, -1, edit);
            }
            else
            {
                edits.AddEdit(toFind, n => docByName.ContainsKey(n), edit);
            }
        }

        public void RemoveMacro(EventEdits edits, string toFind)
        {
            edits.AddEdit(toFind, n => docByName.ContainsKey(n), new InstrEdit
            {
                Remove = true
            });
        }

        public void ReplaceMacro(EventEdits edits, string toFind, string toVal = null)
        {
            edits.AddReplace(toFind, toVal);
        }

        // Simpler mass rewrite: just int replacements, for entity ids which are generally unambiguous
        public void RewriteInts(Instr instr, Dictionary<int, int> changes)
        {
            for (int i = 0; i < instr.Args.Count; i++)
            {
                if (instr.Args[i] is int ik && changes.TryGetValue(ik, out int val))
                {
                    instr[i] = val;
                }
            }
        }

        private static (string, List<string>) ParseCommandString(string add)
        {
            int sparen = add.LastIndexOf('(');
            int eparen = add.LastIndexOf(')');
            if (sparen == -1 || eparen == -1) throw new Exception($"Bad command string {add}");
            string cmd = add.Substring(0, sparen).Trim();
            return (cmd, add.Substring(sparen + 1, eparen - sparen - 1).Split(',').ToList());
        }

        public EMEVD.Instruction ParseAdd(string add)
        {
            (string cmd, List<string> addArgs) = ParseCommandString(add);
            if (!docByName.TryGetValue(cmd, out (int, int) docId)) throw new Exception($"Unrecognized command '{cmd}'");
            EMEDF.InstrDoc addDoc = doc[docId.Item1][docId.Item2];
            List<ArgType> argTypes = addDoc.Arguments.Select(arg => arg.Type == 8 ? ArgType.UInt32 : (ArgType)arg.Type).ToList();
            if (addArgs.Count != argTypes.Count) throw new Exception($"Expected {argTypes.Count} arguments for {cmd}, given {add}");
            return new EMEVD.Instruction(docId.Item1, docId.Item2, addArgs.Select((a, j) => ParseArg(a, argTypes[j])));
        }

        // All the various config classes
        public class EventConfig
        {
            public List<EventSpec> ItemTalks { get; set; }
            public List<EventSpec> ItemEvents { get; set; }
            public List<EventSpec> EnemyEvents { get; set; }
        }

        public class EventSpec
        {
            public int ID { get; set; }
            public string Comment { get; set; }
            public List<EnemyTemplate> Template { get; set; }
            public List<ItemTemplate> ItemTemplate { get; set; }
            public List<string> DebugInfo { get; set; }
            public List<string> DebugInit { get; set; }
            public List<string> DebugCommands { get; set; }
        }

        public class EnemyTemplate
        {
            // chr, multichr, loc, start, end, startphase, endphase, remove, replace
            // chr and multichr create copies
            public string Type { get; set; }
            // The affected entities, if a chr command or if conds/cmds are used below which need to be transplanted
            public int Entity { get; set; }
            // A flag which ends this event when on, if chr
            public int DefeatFlag { get; set; }
            // A flag which ends this event when off, if chr
            public int AppearFlag { get; set; }
            // A 5xxx flag which this event waits for (phase change or boss fight), or the flag itself if start event
            public int StartFlag { get; set; }
            // The condition groups used to end a boss fight, first for music flag and second for permanent flag. Either a group or a command name (with cond group 0)
            public string EndCond { get; set; }
            public string EndCond2 { get; set; }
            // Commands used when starting a boss fight for this entity, especially related to disp mask or lockon points.
            // Usually not needed for minibosses and other enemies, when those are standalone chr events.
            // Maybe should automatically remove (or transplant for boss starts) Set Lock On Point, Force Animation Playback, Set Dispmask, Set AI ID
            public string StartCmd { get; set; }
            // Commands to unconditionally remove.
            public string Remove { get; set; }
            // Args to replace
            public string Replace { get; set; }
            // Commands to add to an event, before randomizing it
            public List<EventAddCommand> Add { get; set; }
            // What to do with regions if a chr command - chrpoint (exact), arenapoint (center/random), arenabox10 (random), arena (bgm), arenasfx (center), or dist10.
            public List<string> Regions { get; set; }
            // Check for doing nothing
            public bool IsDefault() => Entity == 0 && DefeatFlag == 0 && AppearFlag == 0 && StartFlag == 0 && EndCond == null && EndCond2 == null && StartCmd == null && Remove == null && Replace == null && Add == null && Regions == null;
            [YamlIgnore]
            public EMEVD.Event Inner { get; set; }
        }

        public class ItemTemplate
        {
            // item, any, loc, carp
            public string Type { get; set; }
            // The event flag to potentially rewrite
            public string EventFlag { get; set; }
            // The item lot to use for the event flag replacement. TODO: implement
            public string ItemLot { get; set; }
            // The shop slot qwc to use for the event flag replacement. May not be needed, since shop event flags are unambiguous
            // public string ShopQwc { get; set; }
            // An arg to blank out, in the case of alternate drops
            public string RemoveArg { get; set; }
            // An entity to use to identity item lots, mainly for making carp drops unique
            public string Entity { get; set; }
            // For ESD edits, the machine with the flag usage
            public string Machine { get; set; }
            // Commands to unconditionally remove.
            public string Remove { get; set; }
            // Args to replace
            public string Replace { get; set; }
            // Commands to add to an event, before randomizing it
            public List<EventAddCommand> Add { get; set; }
            // Check for doing nothing
            public bool IsDefault() => EventFlag == null && ItemLot == null && RemoveArg == null && Entity == null && Remove == null && Add == null;
        }

        public class EventAddCommand
        {
            public string Cmd { get; set; }
            public string Before { get; set; }
            public string After { get; set; }
        }

        public class EventDebug
        {
            public int Event { get; set; }
            public bool Highlight { get; set; }
            public List<int> IDs = new List<int>();
            public List<InstructionDebug> Callers = new List<InstructionDebug>();
            public List<InstructionDebug> Instructions = new List<InstructionDebug>();
        }

        public class InstructionDebug
        {
            public int Event { get; set; }
            public string Name { get; set; }
            public List<string> Args = new List<string>();
            public HashSet<int> HighlightArgs = new HashSet<int>();
            // Deprecated
            public InstructionDebug Caller { get; set; }
            public InstructionDebug Copy()
            {
                return (InstructionDebug)MemberwiseClone();
            }
            public string CallString() => $"{Name} ({string.Join(", ", Args.Select((a, i) => HighlightArgs.Contains(i) ? $"{a}*" : a))})";
            public override string ToString() => Caller == null ? $"[Event {Event}] {CallString()}" : $"{Caller.CallString()} - {CallString()}";
        }

        // Code for producing event configs
        public SortedDictionary<int, EventDebug> GetHighlightedEvents(Dictionary<string, EMEVD> emevds, HashSet<int> ids)
        {
            Dictionary<(int, int), List<InstructionDebug>> argCommands = new Dictionary<(int, int), List<InstructionDebug>>();
            SortedDictionary<int, EventDebug> eventInfos = new SortedDictionary<int, EventDebug>();
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    EventDebug eventInfo = new EventDebug { Event = (int)e.ID };
                    eventInfos[eventInfo.Event] = eventInfo;
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = Parse(e.Instructions[i]);
                        // Save event initialization for next pass
                        List<(int, int)> usedParams = new List<(int, int)>();
                        foreach (EMEVD.Parameter param in e.Parameters)
                        {
                            if (param.InstructionIndex == i)
                            {
                                int paramIndex = IndexFromByteOffset(instr, (int)param.TargetStartByte);
                                instr[paramIndex] = $"X{param.SourceStartByte}_{param.ByteCount}";
                                usedParams.Add(((int)e.ID, (int)param.SourceStartByte));
                            }
                        }
                        HashSet<int> highlightArgs = new HashSet<int>();
                        List<int> usedIds = new List<int>();
                        for (int j = 0; j < instr.Args.Count; j++)
                        {
                            object arg = instr[j];
                            if (arg is int argint && ids.Contains(argint))
                            {
                                usedIds.Add(argint);
                                highlightArgs.Add(j);
                                eventInfo.Highlight = true;
                                eventInfo.IDs.Add(argint);
                            }
                        }
                        InstructionDebug info = new InstructionDebug
                        {
                            Event = (int)e.ID,
                            Name = instr.Name,
                            Args = instr.Doc.Arguments.Select((arg, j) => $"{arg.Name} = {instr[j]}").ToList(),
                            HighlightArgs = highlightArgs,
                        };
                        foreach ((int, int) id in usedParams) AddMulti(argCommands, id, info);
                        eventInfo.Instructions.Add(info);
                    }
                }
            }
            foreach (KeyValuePair<string, EMEVD> entry in emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    EventDebug eventInfo = eventInfos[(int)e.ID];
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = Parse(e.Instructions[i]);
                        // Find event initialization
                        if (!instr.Init) continue;

                        // New system - detect if event is interesting due to params
                        List<int> entityParams = Enumerable.Range(0, instr.Args.Count - instr.Offset).Where(j => instr[instr.Offset + j] is int argint && ids.Contains(argint)).ToList();
                        if (entityParams.Count > 0)
                        {
                            EventDebug calleeInfo = eventInfos[instr.Callee];
                            calleeInfo.Highlight = true;
                            InstructionDebug caller = new InstructionDebug
                            {
                                Event = (int)e.ID,
                                Name = instr.Name,
                                Args = instr.Args.Select((a, pos) => pos >= instr.Offset ? $"X{(pos - instr.Offset) * 4}_4 = {a}" : $"{a}").ToList()
                            };
                            calleeInfo.Callers.Add(caller);
                            calleeInfo.IDs.AddRange(entityParams.Select(j => instr[instr.Offset + j] is int argint ? argint : 0));
                            List<string> paramStrings = entityParams.Select(j => $"X{j * 4}_4").ToList();
                            foreach (int j in entityParams)
                            {
                                caller.HighlightArgs.Add(instr.Offset + j);
                                argCommands.TryGetValue((instr.Callee, j * 4), out List<InstructionDebug> usages);
                                if (usages != null)
                                {
                                    foreach (InstructionDebug usage in usages)
                                    {
                                        for (int k = 0; k < usage.Args.Count; k++)
                                        {
                                            if (paramStrings.Contains(usage.Args[k].Split(' ').Last()))
                                            {
                                                usage.HighlightArgs.Add(k);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return eventInfos;
        }
        private HashSet<int> processEventsOverride = new HashSet<int> { };
        // Old Dragons: 2500810, 2500811, 2500812, 2500813, 2500814, 2500815, 2500816, 2500817, 2500818, 2500819, 2500820, 2500821, 2500822, 2500823, 2500824, 2500825
        private HashSet<int> processEntitiesOverride = new HashSet<int> { };
        public void WriteEventConfig(string fileName, SortedDictionary<int, EventDebug> eventInfos, Predicate<int> eligibleFilter, Func<int, string> quickId, bool enemies)
        {
            List<EventSpec> toWrite = new List<EventSpec>();
            foreach (KeyValuePair<int, EventDebug> entry in eventInfos.OrderBy(e => e.Key))
            {
                EventDebug info = entry.Value;
                if (entry.Key == 0 || entry.Key == 50) continue;
                bool process = info.Highlight;
                process = process && info.IDs.Any(id => eligibleFilter(id));
                if (processEventsOverride.Count > 0) process = processEventsOverride.Contains(entry.Key);
                else if (processEntitiesOverride.Count > 0) process = processEntitiesOverride.Intersect(info.IDs).Count() > 0;
                if (!process) continue;
                EventSpec spec = new EventSpec
                {
                    ID = entry.Key,
                    Comment = "none",
                    DebugInfo = info.IDs.Select(id => quickId(id)).Distinct().ToList(),
                    DebugInit = info.Callers.Count > 0 ? info.Callers.Select(c => c.CallString()).ToList() : null,
                    DebugCommands = info.Instructions.Select(c => $"{(c.HighlightArgs.Count > 0 ? "+ " : "")}{c.CallString()}").ToList(),
                };
                if (enemies)
                {
                    spec.Template = new List<EnemyTemplate>
                    {
                        new EnemyTemplate
                        {
                            Type = "chr loc start end remove xx",
                            Entity = -1,
                            DefeatFlag = -1,
                        }
                    };
                }
                else
                {
                    spec.ItemTemplate = new List<ItemTemplate>
                    {
                        new ItemTemplate
                        {
                            Type = "item loc",
                            EventFlag = "X0",
                        }
                    };
                }
                toWrite.Add(spec);
            }
            ISerializer serializer = new SerializerBuilder().DisableAliases().Build();
            if (fileName == null)
            {
                serializer.Serialize(Console.Out, new EventConfig { EnemyEvents = toWrite });
            }
            else
            {
                using (var writer = File.CreateText(fileName))
                {
                    serializer.Serialize(writer, new EventConfig { EnemyEvents = toWrite });
                }
            }
        }
    }
}
