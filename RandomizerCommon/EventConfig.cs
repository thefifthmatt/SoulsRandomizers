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
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class EventConfig
    {
        public List<NewEvent> NewEvents { get; set; }
        public List<CommandSegment> DefaultSegments { get; set; }
        // Maybe "item" config should be split up in a different file
        public List<EventSpec> ItemTalks { get; set; }
        public List<EventSpec> ItemEvents { get; set; }
        public List<EventSpec> EnemyEvents { get; set; }

        public class NewEvent
        {
            public int ID { get; set; }
            public string Name { get; set; }
            public string Comment { get; set; }
            public List<string> Commands { get; set; }
        }

        public class EventSpec : AbstractEventSpec
        {
            public List<EnemyTemplate> Template { get; set; }
            public List<ItemTemplate> ItemTemplate { get; set; }
            // Shorthand for simple dupe behaviors, "rewrite" or "copy"
            public string Dupe { get; set; }
        }

        public class EnemyTemplate
        {
            // chr, multichr, loc, start, end, startphase, endphase, remove, segment
            // chr and multichr create copies
            public string Type { get; set; }
            // Documentation on edits being made
            public string Comment { get; set; }
            // The affected entities, if a chr command or if conds/cmds are used below which need to be transplanted
            public int Entity { get; set; }
            // The other entity involved. The source for locs, or the target for chrs. (Currently only works for loc)
            public int Transfer { get; set; }
            // All possible affected entities, generally for removing initializations/commands conditionally
            public string Entities { get; set; }
            // A flag which ends this event when on, if chr
            public int DefeatFlag { get; set; }
            // A flag which ends this event when off, if chr
            public int AppearFlag { get; set; }
            // A 5xxx flag which this event waits for (phase change or boss fight), or the flag itself if start event
            public int StartFlag { get; set; }
            // Flags which set fight state used between events. Used for double feature.
            public string ProgressFlag { get; set; }
            // Phase change flag is set in this event. This is replaced/removed if a single flag, or else if
            // "<command> -> <flag>", added after matching command.
            public string MusicFlag { get; set; }
            // The condition groups used to end a boss fight, first for music flag and second for permanent flag. Either a group or a command name (with cond group 0)
            public string EndCond { get; set; }
            public string EndCond2 { get; set; }
            // Moving CameraSetParam ids between regions in Sekiro
            public string Camera { get; set; }
            // A finisher deathblow, to add conditions to stop it from proccing unnecessarily
            public int Deathblow { get; set; }
            // This character's invincibility is managed here, so after they lose it, their immortality may need to be reset if an immortal boss
            // In DS3, this also sets invincibility/immortality of other enemies in the fight
            public int Invincibility { get; set; }
            // Replace idle/wakeup character animations, like 700 1700, for whoever gets placed in this enemy.
            // The main format is like "<entity> <initial> <wakeup>", 0 if not provided in this event, and arg refs acceptable.
            // A different format "gravity <eventid>" is supported on common funcs when a gravity-less version is available.
            public string Animation { get; set; }
            // Replacing boss/miniboss health bar names. Either "entity" to refer to Entity, or "<entity> <name>" containing ints or arg refs
            public string Name { get; set; }
            // In Sekiro, commands used when starting a boss fight for this entity, like SetLockOnPoint, ForceAnimationPlayback, SetDispMask, SetAIId
            public string StartCmd { get; set; }
            // Replacement for StartCmd/EndCond which is more general, as segments which must be transplanted to other segments
            public List<CommandSegment> Segments { get; set; }
            // Data for modifying the contents of an event when the entity is duplicated into another one
            public Dupe Dupe { get; set; }
            // Commands to change when the tree dragon entity is disabled, with the argument spec as the first semicolon-separate value.
            public string TreeDragons { get; set; }
            // Directive to rewrite event flags depending on which tree dragons are enabled for lightning.
            public string TreeDragonFlags { get; set; }
            // Commands to unconditionally remove.
            public string Remove { get; set; }
            // Commands to unconditionally remove when enemy is not unique
            public string RemoveDupe { get; set; }
            // Args to replace (TODO: replacing entire commands)
            public string Replace { get; set; }
            // Commands to add
            public List<EventAddCommand> Add { get; set; }
            // When provided, rewrites the entire event to the given commands, before applying other edits.
            // This is effectively like creating a brand new event, but per enemy randomization target.
            public List<string> NewEvent { get; set; }
            // What to do with regions if a chr command - chrpoint (exact), arenapoint (center/random), arenabox10 (random), arena (bgm), arenasfx (center), or dist10.
            public List<string> Regions { get; set; }
            // Heuristic check for doing nothing. TODO fully migrate to "default" type
            public bool IsDefault() =>
                Entity == 0 && DefeatFlag == 0 && AppearFlag == 0 && StartFlag == 0 && MusicFlag == null
                    && EndCond == null && EndCond2 == null && StartCmd == null && Segments == null && Dupe == null
                    && Remove == null && RemoveDupe == null && Replace == null && Add == null && NewEvent == null
                    && Regions == null && Camera == null && Invincibility == 0 && Deathblow == 0 && Name == null
                    && TreeDragons == null && TreeDragonFlags == null && Animation == null;
        }

        public class CommandSegment
        {
            // Type summary:
            // disable: Initialization that leaves enemies disabled, when followed by "end if defeated" condition
            // dead: Initialization that leaves enemies disabled, preceded by "goto if alive" and followed by unconditional end
            // setup: Block that leaves boss enabled and inactive
            // altsetup: If setup does not exist, alternate commands to do so (won't be removed from event later)
            // start: Includes activation and boss health bar
            // end: End condition, music cue, and HandleBossDefeat
            // endphase: Only end condition
            // remove: Selection to remove. This is somewhat redundant with Remove attribute in templates, but bounded like segments
            // All bosses must provide:
            // 1. dead or disable - disables boss
            // 2. setup or altsetup - following dead/disable, makes boss enabled and inactive
            // 3. start - following setup/altsetup, make boss enabled and active
            // 4. end or endphase - condition for boss death which disables them after
            public string Type { get; set; }
            // Whether the replacement spot for the should be based on Start or the first match
            public bool IgnoreMatch { get; set; }
            // Whether to add invincibility commands to this location, to enable (in setup) and disable (in start).
            public bool Invincibility { get; set; }
            // Regions used in this segment (these behave differently from chr template Regions, as they apply to the segment commands only)
            public List<string> Regions { get; set; }
            // Starting command for Commands to match against (must be GotoIf for dead)
            public string Start { get; set; }
            // Ending command for Commands to match against (must be End for dead, EndIf for disable)
            public string End { get; set; }
            // Commands, which mostly match ones in the range. (This can include some custom commands? hide, show, fall damage)
            // If not altsetup and not NoMatch, this must include at least one matching instruction
            public List<string> Commands { get; set; }
        }

        // Parsed segment data
        public class SegmentData
        {
            public List<EMEVD.Instruction> Instructions { get; set; }
            public List<string> Regions { get; set; }
        }

        public class Dupe
        {
            // Whether to skip rewriting entities for a loc event, e.g. if modifying original enemy behavior
            public bool NoRewrite { get; set; }
            // The existing enemy arg and the dupe arg position to add, when parameterized, or entity otherwise
            // Implies rewrite when not locarg, and the rewrite it limited to the entity
            public string Entity { get; set; }
            // Health bar name id whose dupe should have different indices and name
            public string HealthBar { get; set; }
            // Health bar name id parameter, to add at the end after Entity args
            public string HealthBarArg { get; set; }
            // Condition groups, when and/or logic mismatches. <source group> [<source 2 group> <combined group>].
            // By default uses 12, 13 as replacement groups (only used by butterfly end, miniboss start)
            public string Condition { get; set; }
            // Generators to add to dupe rewrite map, to duplicate for different enemies
            // This is meant for loc events, as chr events should use chrgen regions.
            public string Generator { get; set; }
            // When rewriting an event, add a quick delay between animations
            public int DelayAnimation { get; set; }
        }

        public class ItemTemplate
        {
            // item, any, loc, carp, default (ER)
            public string Type { get; set; }
            // Documentation on edits being made
            public string Comment { get; set; }
            // The event flag to potentially rewrite (space-separate list)
            public string EventFlag { get; set; }
            // The argument to edit, if an arg event. If a second is given, copies the second to the first.
            public string EventFlagArg { get; set; }
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
    }
}
