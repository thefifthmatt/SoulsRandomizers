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
using static RandomizerCommon.Util;
using static SoulsFormats.EMEVD.Instruction;

namespace RandomizerCommon
{
    public class EventConfig
    {
        public List<EventSpec> ItemTalks { get; set; }
        public List<EventSpec> ItemEvents { get; set; }
        public List<EventSpec> EnemyEvents { get; set; }

        public class EventSpec : AbstractEventSpec
        {
            public List<EnemyTemplate> Template { get; set; }
            public List<ItemTemplate> ItemTemplate { get; set; }
        }

        public class EnemyTemplate
        {
            // chr, multichr, loc, start, end, startphase, endphase, remove, replace
            // chr and multichr create copies
            public string Type { get; set; }
            // The affected entities, if a chr command or if conds/cmds are used below which need to be transplanted
            public int Entity { get; set; }
            // The other entity involved. The source for locs, or the target for chrs. (Currently only works for loc)
            public int Transfer { get; set; }
            // A flag which ends this event when on, if chr
            public int DefeatFlag { get; set; }
            // A flag which ends this event when off, if chr
            public int AppearFlag { get; set; }
            // A 5xxx flag which this event waits for (phase change or boss fight), or the flag itself if start event
            public int StartFlag { get; set; }
            // 5xxx flags which are both set and read as chr, and should be isolated for the target entity if it is duplicated
            public string ProgressFlag { get; set; }
            // The condition groups used to end a boss fight, first for music flag and second for permanent flag. Either a group or a command name (with cond group 0)
            public string EndCond { get; set; }
            public string EndCond2 { get; set; }
            // Moving CameraSetParam ids between regions
            public string Camera { get; set; }
            // A finisher deathblow, to add conditions to stop it from proccing unnecessarily
            public int Deathblow { get; set; }
            // This character's invincibility is managed here, so after they lose it, their immortality may need to be reset if an immortal boss
            public int Invincibility { get; set; }
            // Replacing boss/miniboss health bar names
            public string Name { get; set; }
            // Commands used when starting a boss fight for this entity, especially related to disp mask or lockon points.
            // Usually not needed for minibosses and other enemies, when those are standalone chr events.
            // Maybe should automatically remove (or transplant for boss starts) Set Lock On Point, Force Animation Playback, Set Dispmask, Set AI ID
            public string StartCmd { get; set; }
            // Commands to change when the tree dragon entity is disabled, with the argument spec as the first semicolon-separate value.
            public string TreeDragons { get; set; }
            // Directive to rewrite event flags depending on which tree dragons are enabled for lightning.
            public string TreeDragonFlags { get; set; }
            // Commands to unconditionally remove.
            public string Remove { get; set; }
            // Commands to unconditionally remove when enemy is not unique
            public string RemoveDupe { get; set; }
            // Args to replace
            public string Replace { get; set; }
            // Commands to add to an event, before randomizing it
            public List<EventAddCommand> Add { get; set; }
            // What to do with regions if a chr command - chrpoint (exact), arenapoint (center/random), arenabox10 (random), arena (bgm), arenasfx (center), or dist10.
            public List<string> Regions { get; set; }
            // Check for doing nothing. Maybe should just have a type for nothing
            public bool IsDefault() =>
                Entity == 0 && DefeatFlag == 0 && AppearFlag == 0 && StartFlag == 0 && EndCond == null && EndCond2 == null && StartCmd == null && Remove == null && RemoveDupe == null && Replace == null
                    && Add == null && Regions == null && Camera == null && Invincibility == 0 && Deathblow == 0 && ProgressFlag == null && Name == null && TreeDragons == null && TreeDragonFlags == null;
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
    }
}
