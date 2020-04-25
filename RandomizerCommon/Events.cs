using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using System.Text.RegularExpressions;
using SoulsFormats;
using static RandomizerCommon.Util;
using static SoulsFormats.EMEVD.Instruction;

namespace RandomizerCommon
{
    public class EventsDeprecated
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
        private static string TextArg(object arg)
        {
            return arg is float f ? f.ToString(CultureInfo.InvariantCulture) : arg.ToString();
        }

        private EMEDF doc;
        private Dictionary<string, (int, int)> docByName;
        private Dictionary<EMEDF.InstrDoc, List<int>> funcBytePositions;

        public EventsDeprecated(string emedfPath)
        {
            doc = EMEDF.ReadFile(emedfPath);
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

            public override string ToString() => $"{Name} ({string.Join(",", Args.Select(a => TextArg(a)))})";
        }

        private int IndexFromByteOffset(Instr instr, int offset)
        {
            int paramIndex = funcBytePositions[instr.Doc].IndexOf(offset);
            if (paramIndex == -1) throw new Exception($"Finding {instr.Name}, target {offset}, available {string.Join(",", funcBytePositions[instr.Doc])}");
            return paramIndex;
        }

        public static bool IsTemp(int flag)
        {
            return (flag / 1000) % 10 == 5;
        }

        public EMEVD.Instruction CopyInstruction(EMEVD.Instruction i)
        {
            return i.Layer.HasValue ? new EMEVD.Instruction(i.Bank, i.ID, i.Layer.Value, i.ArgData) : new EMEVD.Instruction(i.Bank, i.ID, i.ArgData);
        }

        public EMEVD.Event CopyEvent(EMEVD.Event src, int newId)
        {
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
            public Dictionary<string, List<InstrEdit>> NameEdits { get; set; }
            public Dictionary<string, List<InstrEdit>> ArgEdits { get; set; }
            public Dictionary<(string, string), List<InstrEdit>> NameArgEdits { get; set; }
            public HashSet<InstrEdit> PendingEdits = new HashSet<InstrEdit>();
            public Dictionary<int, List<InstrEdit>> PendingAdds = new Dictionary<int, List<InstrEdit>>();

            // Returns all applicable edits
            public List<InstrEdit> GetMatches(Instr instr)
            {
                List<InstrEdit> nameEdit = null;
                if (NameEdits != null && !NameEdits.TryGetValue(instr.Name, out nameEdit) && ArgEdits == null && NameArgEdits == null) return null;
                List<string> strArgs = instr.Args.Select(a => TextArg(a)).ToList();
                List<InstrEdit> edits = new List<InstrEdit>();
                if (ArgEdits != null)
                {
                    edits.AddRange(strArgs.SelectMany(s => ArgEdits.TryGetValue(s, out List<InstrEdit> edit) ? edit : new List<InstrEdit>()));
                }
                if (nameEdit != null)
                {
                    edits.AddRange(nameEdit);
                }
                if (NameArgEdits != null && NameArgEdits.TryGetValue((instr.Name, string.Join(",", strArgs)), out List<InstrEdit> nameArgEdit))
                {
                    edits.AddRange(nameArgEdit);
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
                                if (edit.ValEdit.TryGetValue(TextArg(instr[i]), out string replace))
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
                    if (ArgEdits == null) ArgEdits = new Dictionary<string, List<InstrEdit>>();
                    AddMulti(ArgEdits, toFind, edit);
                }
                else if (docName(toFind))
                {
                    // If this isn't a name, it will come up later as an unused pending edit
                    if (NameEdits == null) NameEdits = new Dictionary<string, List<InstrEdit>>();
                    AddMulti(NameEdits, toFind, edit);
                }
                else
                {
                    (string cmd, List<string> addArgs) = ParseCommandString(toFind);
                    if (NameArgEdits == null) NameArgEdits = new Dictionary<(string, string), List<InstrEdit>>();
                    AddMulti(NameArgEdits, (cmd, string.Join(",", addArgs)), edit);
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
                    if (ArgEdits == null) ArgEdits = new Dictionary<string, List<InstrEdit>>();
                    AddMulti(ArgEdits, parts[0], edit);
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
                    if (NameArgEdits == null) NameArgEdits = new Dictionary<(string, string), List<InstrEdit>>();
                    AddMulti(NameArgEdits, (cmd, string.Join(",", addArgs)), edit);
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
            // TODO: Add information here to see what is being matched, not just what the edit is
            public override string ToString() => $"Edit (Add {Add}, Remove {Remove}, PosEdit {(PosEdit == null ? "" : string.Join(",", PosEdit))}, ValEdit {(ValEdit == null ? "" : string.Join(",", ValEdit))})";
        }

        public void AddMacro(EventEdits edits, List<EventAddCommand> adds)
        {
            foreach (EventAddCommand add in Enumerable.Reverse(adds))
            {
                if (add.Before == null && add.After == null)
                {
                    AddMacro(edits, null, true, add.Cmd);
                }
                else
                {
                    AddMacro(edits, add.After ?? (add.Before == "start" ? null : add.Before), add.After != null, add.Cmd);
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
                    // At the end. This is not being inserted at the start, but rather the end, so reverse the order
                    foreach (InstrEdit addEdit in Enumerable.Reverse(lineEdit.Value))
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
                AddMulti(edits.PendingAdds, addAfter ? -1 : 0, edit);
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

        // Condition rewriting
        public List<int> FindCond(EMEVD.Event e, string req)
        {
            List<int> cond = new List<int>();
            bool isGroup = int.TryParse(req, out int _);
            for (int i = 0; i < e.Instructions.Count; i++)
            {
                Instr instr = Parse(e.Instructions[i]);
                if (isGroup && instr.Name.StartsWith("IF") && instr[0].ToString() == req)
                {
                    cond.Add(i);
                    continue;
                }
                else if (isGroup && instr.Name == "IF Condition Group" && instr[2].ToString() == req)
                {
                    cond.Add(i);
                    return cond;
                }
                else if (!isGroup && instr.Name == req && instr[0].ToString() == "0")
                {
                    cond.Add(i);
                    return cond;
                }
            }
            throw new Exception($"Couldn't find ending condition '{req}', group {isGroup}, in event {e.ID}");
        }
        public List<EMEVD.Instruction> RewriteCondGroup(List<EMEVD.Instruction> after, Dictionary<int, int> reloc, int target)
        {
            sbyte targetCond = (sbyte)target;
            sbyte sourceCond = 0;
            return after.Select(afterInstr =>
            {
                Instr instr = Parse(CopyInstruction(afterInstr));
                if (instr.Name == "IF Condition Group")
                {
                    if (sourceCond == 0) throw new Exception($"Internal error: can't infer condition group for {instr}");
                    instr[0] = targetCond;
                    instr[2] = (sbyte)(sourceCond > 0 ? 12 : -12);
                }
                else
                {
                    if (sourceCond == 0)
                    {
                        sourceCond = (sbyte)instr[0];
                    }
                    // This is way too hacky... can add more semantic info if it becomes fragile
                    instr[0] = after.Count == 1 ? targetCond : (sbyte)(sourceCond > 0 ? 12 : -12);
                }
                RewriteInts(instr, reloc);
                instr.Save();
                return instr.Val;
            }).ToList();
        }

        public bool ParseArgSpec(string arg, out int pos)
        {
            // For event initializations with int args specified as X0, X4, X8, etc., return the arg position, e.g. 0, 1, 2
            pos = 0;
            if (arg.StartsWith("X") && int.TryParse(arg.Substring(1), out pos))
            {
                pos /= 4;
                return true;
            }
            return false;
        }

        public abstract class AbstractEventSpec
        {
            public int ID { get; set; }
            public string Comment { get; set; }
            public List<string> DebugInfo { get; set; }
            public List<string> DebugInit { get; set; }
            public List<string> DebugCommands { get; set; }
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

        public List<T> CreateEventConfig<T>(
            SortedDictionary<int, EventDebug> eventInfos,
            Predicate<int> eligibleFilter,
            Func<T> createSpec,
            Func<int, string> quickId,
            HashSet<int> eventsOverride = null,
            HashSet<int> idsOverride = null)
            where T : AbstractEventSpec
        {
            List<T> toWrite = new List<T>();
            foreach (KeyValuePair<int, EventDebug> entry in eventInfos.OrderBy(e => e.Key))
            {
                EventDebug info = entry.Value;
                // At least for now, don't rewrite constructors in configs
                if (entry.Key == 0 || entry.Key == 50) continue;
                bool process = info.Highlight;
                process = process && info.IDs.Any(id => eligibleFilter(id));
                if (eventsOverride?.Count > 0) process = eventsOverride.Contains(entry.Key);
                else if (idsOverride?.Count > 0) process = idsOverride.Intersect(info.IDs).Count() > 0;
                if (!process) continue;

                T spec = createSpec();
                spec.ID = entry.Key;
                spec.Comment = "none";
                spec.DebugInfo = info.IDs.Select(id => quickId(id)).Distinct().ToList();
                spec.DebugInit = info.Callers.Count > 0 ? info.Callers.Select(c => c.CallString()).ToList() : null;
                spec.DebugCommands = info.Instructions.Select(c => $"{(c.HighlightArgs.Count > 0 ? "+ " : "")}{c.CallString()}").ToList();
                toWrite.Add(spec);
            }
            return toWrite;
        }
    }
}
