using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using static RandomizerCommon.EventConfig;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    // Separate item event config (previously in enemy-focused EventConfig, and still there for Sekiro)
    public class ItemEventConfig
    {
        public List<NewItemEvent> NewEvents { get; set; }
        public List<ItemEventSpec> ItemTalks { get; set; }
        public List<ItemEventSpec> ItemEvents { get; set; }
        public List<LogicFlag> LogicFlags { get; set; }
        public List<LogicGate> LogicGates { get; set; }

        // Currently for one-off init events, not common_func events
        public class NewItemEvent
        {
            // public int ID { get; set; }
            // public string Name { get; set; }
            public string Map { get; set; }
            public TemplateFilter Filter { get; set; }
            public string Comment { get; set; }
            public List<string> Commands { get; set; }
        }

        public class ItemEventSpec : AbstractEventSpec
        {
            // If the interactions between these get complicated, FlagTemplate could probably become an ItemTemplate field
            public List<ItemTemplate> Template { get; set; }
            public List<ItemFlagTemplate> FlagTemplate { get; set; }

            public EventSpec DeepCopy()
            {
                // Despite the name, does not deep-copy debug strings from parent class, which would be expensive anyway
                EventSpec o = (EventSpec)MemberwiseClone();
                if (o.Template != null) o.Template = o.Template.Select(x => x.DeepCopy()).ToList();
                if (o.ItemTemplate != null) o.ItemTemplate = o.ItemTemplate.Select(x => x.DeepCopy()).ToList();
                return o;
            }
        }

        public class TemplateFilter
        {
            // Corresponding to either options or items where the condition has changed vs vanilla game, and may be used in template itself.
            // Options: liurnia, dragonbarrow, altus, leyndell. Can also add royal
            // Items: Anything which can be split into shards. In this case, it also prevents removing the item if present (may require custom handling in the future)
            public string Gate { get; set; }
            // Area to block, taking combined conditions from all active blocks for that area
            public string BlockArea { get; set; }

            // -- These are only used for filtering, not by templates
            // Space-separated list of gates. If any are enabled, run the template.
            public string Gates { get; set; }
            // Edit made based on in-scope content, currently just dlc
            public string Content { get; set; }
            // Map from to param name to arg value (integer width only)
            public Dictionary<string, long> Args { get; set; }
        }

        public class ItemTemplate
        {
            // loc, arg, remove, default (ignore)
            public string Type { get; set; }
            public string Comment { get; set; }
            // Condition for the edit. If needed this could be a list to allow any condition.
            public List<TemplateFilter> Filter { get; set; }

            // -- Arg edits
            // String-separated lists of item args to unconditionally turn into flag args.
            // This might be more suitable for FlagTemplate but for now there's less duplication doing it this way.
            public string ItemArg { get; set; }
            // <param> <arg>, where arg is an integer or 'gate' when Filter.Gate is set. Also, could be a list if needed.
            public string SetArg { get; set; }

            // -- Event edits based on different item conditions
            // Flag to replace with new condition flag, based on Filter.Gate
            public int CondFlag { get; set; }
            // List of item accesses to replace with flag accesses. If they are params, also add a loc event with ItemArg.
            public List<string> ItemUse { get; set; }

            // Dynamic commands to add
            public List<DynamicEdit> Edits { get; set; }
            // Commands to add
            public List<EventAddCommand> Add { get; set; }
            // Commands to unconditionally remove.
            public List<string> Removes { get; set; }
            // Commands or args to replace
            public List<EventReplaceCommand> Replaces { get; set; }
            // Metadata and passively registered commands for detecting condition group rewriting.
            public string CondOrder { get; set; }
            public List<string> CondIdentity { get; set; }
        }

        public class DynamicEdit
        {
            public List<string> BeforeCmds { get; set; }
            public int ItemCond { get; set; }
            public List<string> AfterCmds { get; set; }
            public string Before { get; set; }
            public string After { get; set; }
            public string Replace { get; set; }
        }

        public class LogicFlag
        {
            // If a stable flag is needed for a condition, either gate or item. An event is set up to sync this on/off.
            public string Name { get; set; }
            [YamlMember(DefaultValuesHandling = DefaultValuesHandling.Preserve)]
            public int Index { get; set; }
            public string Comment { get; set; }
        }

        public class LogicGate
        {
            // Item or gate name
            public string Name { get; set; }
            public string Comment { get; set; }
            // For gate, preexisting flag to use or other custom value
            // int is flag, 'none' means always block, and 'custom' is resolved in code based on options
            public string Cond { get; set; }
            // For item, expect to find it in ObjAct and change to flag edit
            public bool ObjAct { get; set; }
            // List of objects to add
            // Format is: <map> <model> <base> <x> <y> <z> <y rot> [<x rot> <z rot>]
            public List<string> Block { get; set; }
        }

        public class ItemFlagTemplate
        {
            // item, any, loc, carp, default (ER)
            public string Type { get; set; }
            // Documentation on edits being made
            public string Comment { get; set; }
            // The event flag to potentially rewrite (space-separate list)
            // At the moment, these are interpreted as ints. uint migration has not be necessary so far.
            public string EventFlag { get; set; }
            // The argument to edit, if an arg event. If a second is given, copies the second to the first.
            public string EventFlagArg { get; set; }
            // The item lot to use for the event flag replacement. TODO: implement
            public string ItemLot { get; set; }
            // A condition group to use for converting flag checks to item flags, when item template is used.
            // Right now extremely hacky, should use Replace instead. ItemCond should be positive and able to increment until 15.
            public int ItemCond { get; set; }
            // The shop slot qwc to use for the event flag replacement. May not be needed, since shop event flags are unambiguous
            // public string ShopQwc { get; set; }
            // An arg to blank out, in the case of alternate drops
            public string RemoveArg { get; set; }
            // An entity to use to identity item lots, mainly for making carp drops unique
            public string Entity { get; set; }
            // Indicates presence of item for ESD shard edits, goods only currently
            public int UseItem { get; set; }
            // For ESD edits, the machine with the flag usage (this can shift between releases, and maybe should deserve its own EventSpec)
            public string Machine { get; set; }

            // -- Not supported in all games and should probably move to the new ItemTemplate:
            // Commands to unconditionally remove.
            public string Remove { get; set; }
            // Args to replace
            public string Replace { get; set; }
            // Commands to add to an event, before randomizing it
            public List<EventAddCommand> Add { get; set; }

            // Check for doing nothing
            // This should probably use explicit 'default' type like enemy templates
            public bool IsDefault() => EventFlag == null && ItemLot == null && RemoveArg == null && Entity == null && Remove == null && Replace == null && Add == null;

            public ItemFlagTemplate DeepCopy()
            {
                ItemFlagTemplate o = (ItemFlagTemplate)MemberwiseClone();
                if (o.Add != null) o.Add = o.Add.Select(x => x.DeepCopy()).ToList();
                return o;
            }
        }

        public static ItemEventConfig FromEnemyConfig(EventConfig enemyConfig)
        {
            return new ItemEventConfig
            {
                ItemTalks = enemyConfig.ItemTalks?.Select(CopySpec).ToList(),
                ItemEvents = enemyConfig.ItemEvents?.Select(CopySpec).ToList(),
            };
        }

        private static ItemEventSpec CopySpec(EventSpec enemySpec)
        {
            return new ItemEventSpec
            {
                ID = enemySpec.ID,
                Map = enemySpec.Map,
                Comment = enemySpec.Comment,
                DebugInfo = enemySpec.DebugInfo,
                DebugInit = enemySpec.DebugInit,
                DebugCommands = enemySpec.DebugCommands,
                DebugOtherInits = enemySpec.DebugOtherInits,
                FlagTemplate = enemySpec.ItemTemplate,
            };
        }
    }
}
