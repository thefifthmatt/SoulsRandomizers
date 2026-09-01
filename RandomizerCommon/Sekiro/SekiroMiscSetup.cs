using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class SekiroMiscSetup
    {

        public static void SekiroCommonPass(GameData game, Events events, RandomizerOptions opt)
        {
            ParamDictionary Params = game.Params;

            // Snap (for convenience, but can also softlock the player)
            if (opt["snap"]) Params["EquipParamGoods"][3980]["goodsUseAnim"].Value = (sbyte)84;
            // No tutorials
            if (Params.ContainsKey("MenuTutorialParam")) Params["MenuTutorialParam"].Rows = Params["MenuTutorialParam"].Rows.Where(r => r.ID == 0).ToList();
            // Memos pop up and don't just disappear mysteriously
            foreach (int popup in new[] { 9221, 9223, 9225 })
            {
                PARAM.Row row = Params["EquipParamGoods"][popup];
                row["isDrop"].Value = (byte)0;
                row["isNoticeLog"].Value = (byte)1;
                row["isNoticeDialog"].Value = (byte)1;
            }

            // These are just always deleted
            HashSet<string> deleteCommands = new HashSet<string>
            {
                "Show Tutorial Text", "Show Hint Box", "Show Small Hint Box", "Award Achievement"
            };
            HashSet<uint> deleteEvents = new()
            {
                // Putting away sword in areas
                20006200,
            };
            // Slowless slow walk
            if (opt["headlesswalk"]) deleteEvents.Add(20005431);

            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    bool commonInit = entry.Key == "common" && e.ID == 0;
                    int maxPermSlot = 0;
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        Instr instr = events.Parse(e.Instructions[i]);
                        bool delete = false;
                        if (instr.Init)
                        {
                            if (deleteEvents.Contains(instr.Callee)) delete = true;
                            else if (commonInit && instr.Callee == 750 && instr.Offset == 2) maxPermSlot = Math.Max(maxPermSlot, (int)instr[0]);
                        }
                        else
                        {
                            if (deleteCommands.Contains(instr.Name)) delete = true;
                        }
                        if (delete)
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            // Just in case...
                            e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                        }
                    }
                    // Add permanent shop placement flags. Also.... abuse this for headless ape bestowal lot, if enemy rando is enabled.
                    // Slight hack to ignore this for specific preset
                    if (opt["enemy"] && opt["bosses"] && commonInit)
                    {
                        entry.Value.Events[0].Instructions.Add(new EMEVD.Instruction(2000, 0, new List<object> { maxPermSlot + 1, (uint)750, (uint)9307, (uint)9314 }));
                    }
                }
            }
        }
    }
}