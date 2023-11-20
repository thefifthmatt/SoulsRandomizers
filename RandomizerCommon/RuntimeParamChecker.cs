using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using SoulsFormats;
using SoulsIds;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class RuntimeParamChecker
    {
        public static readonly int RestartMessageId = 666400;
        public static readonly int FogMessageId = 666401;
        private static readonly int AlertFlag = GameData.EldenRingBase + 2001;

        private Dictionary<string, List<uint>> mapEntities = new Dictionary<string, List<uint>>();
        private List<int> requiredSpEffects = new List<int>();
        // Unique range, probably. Does not need to be writable
        private int eventId = 3888000;

        public RuntimeParamChecker() { }

        public void ScanMaps(Dictionary<string, IMsb> maps)
        {
            // Currently only supports Elden Ring
            foreach (KeyValuePair<string, IMsb> entry in maps)
            {
                if (entry.Value is MSBE msb)
                {
                    foreach (MSBE.Part.Enemy e in msb.Parts.Enemies)
                    {
                        // Mark bonfires, as they will generally be available
                        if (e.TalkID == 1000 && e.NPCParamID == 10000000 && e.EntityID > 0)
                        {
                            string eventMap = entry.Key;
                            if (eventMap.StartsWith("m60") && !eventMap.EndsWith("0"))
                            {
                                if (e.Name.StartsWith("m") && e.Name.Contains("-"))
                                {
                                    eventMap = e.Name.Split('-')[0];
                                }
                            }
                            AddMulti(mapEntities, eventMap, e.EntityID);
                        }
                    }
                }
            }
        }

        public void CheckEntries(GameData game)
        {
            // Currently: AssetEnvironmentGeometryParam AtkParam_Npc BonfireWarpParam BonfireWarpSubCategoryParam CharaInitParam EquipMtrlSetParam EquipParamAccessory EquipParamGem EquipParamGoods EquipParamProtector EquipParamWeapon GameAreaParam ItemLotParam_enemy ItemLotParam_map Magic NpcParam NpcThinkParam ShopLineupParam ShopLineupParam_Recipe SpEffectParam WorldMapLegacyConvParam WorldMapPointParam
            // Dynamic: CharaInitParam ItemLotParam_map NpcParam NpcThinkParam ShopLineupParam ShopLineupParam_Recipe WorldMapPointParam
            // Console.WriteLine(string.Join(" ", Params.Where(x => x.Value.AppliedParamdef != null).Select(x => x.Key)));
            // These are necessary for randomizer to function. If they do not match, something is wrong.
            List<string> dynamicParams = new List<string>
            {
                "CharaInitParam", "ItemLotParam_map", "NpcParam", "NpcThinkParam",
                "ShopLineupParam", "ShopLineupParam_Recipe", "WorldMapPointParam",
                "Bullet",
            };
            List<string> dynamicFmgs = new List<string> { "NpcName", "PlaceName" };
            MD5 md5 = MD5.Create();
            byte[] combined = null;
            void combineHash(byte[] hash)
            {
                if (combined == null)
                {
                    combined = hash;
                }
                else
                {
                    int len = Math.Min(hash.Length, combined.Length);
                    for (int i = 0; i < len; i++)
                    {
                        combined[i] ^= hash[i];
                    }
                }
            }
            // Just xor them together, TransformBlock doesn't make any sense to me
            foreach (string name in dynamicParams)
            {
                if (game.Params.Inner.TryGetValue(name, out PARAM param) && param.AppliedParamdef != null)
                {
                    byte[] data = param.Write();
                    combineHash(md5.ComputeHash(data));
                }
            }
            foreach (string name in dynamicFmgs)
            {
                if (game.ItemFMGs.FMGs.TryGetValue(name, out FMG fmg))
                {
                    byte[] data = fmg.Write();
                    combineHash(md5.ComputeHash(data));
                }
            }
            PARAM.Row npc = game.Params["NpcParam"][10000000];
            game.Params.Inner.TryGetValue("SpEffectParam", out PARAM spParam);
            if (combined != null && npc != null && spParam != null)
            {
                // Twos complement by default, and also, little-endian, so that's great
                BigInteger result = new BigInteger(combined.Concat(new byte[] { 0 }).ToArray());
                // 0 19, 19 16, 14 4, 9 18
                int sp1 = (int)(result % 20);
                result /= 20;
                int sp2 = (int)(result % 19);
                if (sp1 == sp2)
                {
                    sp2 = 19;
                }
                int spIndex = 4;
                foreach (int index in new List<int> { sp1, sp2 })
                {
                    int sp = 6950 + Math.Max(0, Math.Min(Math.Abs(index), 19));
                    string field = $"spEffectID{spIndex++}";
                    if ((int)npc[field].Value <= 0 && spParam[sp] != null)
                    {
                        npc[field].Value = sp;
                        requiredSpEffects.Add(sp);
                    }
                }
            }
#if DEBUG
            Console.WriteLine($"Check speffects: [{string.Join(", ", requiredSpEffects)}]");
#endif
        }

        public void EditEvents(string map, EMEVD emevd)
        {
            if (map == "common")
            {
                // SetEventFlag(TargetEventFlagType.EventFlag, alert, OFF)
                emevd.Events[0].Instructions.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, AlertFlag, (byte)0 }));
            }
            if (requiredSpEffects.Count == 0) return;
            if (!mapEntities.TryGetValue(map, out List<uint> entities)) return;
            foreach (uint entity in entities)
            {
                List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                // IfCharacterBackreadStatus(AND_01, entity, true, ComparisonType.Equal, 1)
                instrs.Add(new EMEVD.Instruction(4, 7, new List<object> { (sbyte)1, entity, (byte)1, (byte)0, 1f }));
                foreach (int sp in requiredSpEffects)
                {
                    // IfCharacterHasSpEffect(AND01, entity, sp, false, ComparisonType.Equal, 1)
                    instrs.Add(new EMEVD.Instruction(4, 5, new List<object> { (sbyte)1, entity, sp, (byte)0, (byte)0, 1f }));
                }
                // IfEventFlag(AND01, OFF, TargetEventFlagType.EventFlag, alert)
                instrs.Add(new EMEVD.Instruction(3, 0, new List<object> { (sbyte)1, (byte)0, (byte)0, AlertFlag }));
                // IfConditionGroup(MAIN, PASS, AND01)
                instrs.Add(new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)1 }));
                // SetEventFlag(TargetEventFlagType.EventFlag, alert, ON)
                instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, AlertFlag, (byte)1 }));
                // DisplayGenericDialog(msg, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5);
                instrs.Add(new EMEVD.Instruction(2007, 1, new List<object> { RestartMessageId, (short)1, (short)6, 0, 5f }));
                // WaitFixedTimeSeconds
                instrs.Add(new EMEVD.Instruction(1001, 0, new List<object> { (float)30 }));
                // SetEventFlag(TargetEventFlagType.EventFlag, alert, ON)
                instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, AlertFlag, (byte)0 }));
                // EndUnconditionally(EventEndType.Restart)
                instrs.Add(new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }));
                Events.AddSimpleEvent(emevd, eventId++, instrs, EMEVD.Event.RestBehaviorType.Restart);
            }
        }
    }
}
