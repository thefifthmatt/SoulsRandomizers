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
        public static readonly int DlcMessageId = 666402;
        public static readonly int TarnishedMessageId = 666403;
        private readonly int AlertFlag;

        private Dictionary<string, List<uint>> mapEntities = new Dictionary<string, List<uint>>();
        private List<int> requiredSpEffects = new List<int>();
        // Unique range, probably. Does not need to be writable
        private int eventId = 3888000;

        public RuntimeParamChecker(int flag)
        {
            AlertFlag = flag;
        }

        public void ScanMaps<T>(IReadOnlyDictionary<string, T> maps) where T : IMsb
        {
            // Currently only supports Elden Ring and DS1
            foreach (KeyValuePair<string, T> entry in maps)
            {
                if (entry.Value is MSBE msb)
                {
                    foreach (MSBE.Part.Enemy e in msb.Parts.Enemies)
                    {
                        // Mark bonfires, as they will generally be available
                        if (e.TalkID == 1000 && e.NPCParamID == 10000000 && e.EntityID > 0)
                        {
                            string eventMap = entry.Key;
                            if (eventMap.StartsWith("m6") && !eventMap.EndsWith("0"))
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
                else if (entry.Value is MSB1 msb1)
                {
                    foreach (MSB1.Part.Enemy e in msb1.Parts.Enemies)
                    {
                        // Mark bonfires, as they will generally be available. ids are like 1010962, 1411964, 1510950
                        if (e.TalkID > 0 && e.NPCParamID == 100000 && e.ModelName == "c1000" && (e.EntityID % 10000) / 100 == 9)
                        {
                            string eventMap = entry.Key;
                            AddMulti(mapEntities, eventMap, (uint)e.EntityID);
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
            List<string> dynamicParams, dynamicFmgs;
            if (game.EldenRing)
            {
                dynamicParams = new List<string>
                {
                    "CharaInitParam", "ItemLotParam_map", "NpcParam", "NpcThinkParam",
                    "ShopLineupParam", "ShopLineupParam_Recipe", "WorldMapPointParam",
                    "Bullet",
                };
                dynamicFmgs = new List<string> { "NpcName", "PlaceName" };
            }
            else
            {
                dynamicParams = new List<string>
                {
                    "CharaInitParam", "ItemLotParam", "NpcParam", "NpcThinkParam",
                };
                dynamicFmgs = new List<string> { "NPC_name_" };
            }
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
            PARAM.Row npc = game.Params["NpcParam"][game.EldenRing ? 10000000 : 100000];
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
                int baseSp = game.EldenRing ? 6950 : 88000;
                foreach (int index in new List<int> { sp1, sp2 })
                {
                    int sp = baseSp + Math.Max(0, Math.Min(Math.Abs(index), 19));
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

        public void EditEvents(string map, EMEVD emevd, Events events, GameData game)
        {
            if (map == "common")
            {
                string cmd = game.EldenRing ? $"SetEventFlag(TargetEventFlagType.EventFlag, {AlertFlag}, OFF)" : $"SetEventFlag({AlertFlag}, OFF)";
                emevd.Events[0].Instructions.Add(events.ParseAdd(cmd));
            }
            if (requiredSpEffects.Count == 0) return;
            if (!mapEntities.TryGetValue(map, out List<uint> entities)) return;
            foreach (uint entity in entities)
            {
                List<string> instrs = new();
                string extra = game.EldenRing ? ", ComparisonType.Equal, 1" : "";
                instrs.Add($"IfCharacterBackreadStatus(AND_01, {entity}, true{extra})");
                foreach (int sp in requiredSpEffects)
                {
                    instrs.Add($"IfCharacterHasSpEffect(AND_01, {entity}, {sp}, false{extra})");
                }
                instrs.Add($"IfEventFlag(AND_01, OFF, TargetEventFlagType.EventFlag, {AlertFlag})");
                instrs.Add($"IfConditionGroup(MAIN, PASS, AND_01)");
                if (game.EldenRing)
                {
                    instrs.Add($"SetEventFlag(TargetEventFlagType.EventFlag, {AlertFlag}, ON)");
                    instrs.Add($"DisplayGenericDialog({RestartMessageId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5)");
                    instrs.Add($"WaitFixedTimeSeconds(30)");
                    instrs.Add($"SetEventFlag(TargetEventFlagType.EventFlag, {AlertFlag}, OFF)");
                }
                else
                {
                    instrs.Add($"SetEventFlag({AlertFlag}, ON)");
                    instrs.Add($"DisplayGenericDialog({RestartMessageId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, -1, 100)");
                    instrs.Add($"WaitFixedTimeSeconds(30)");
                    instrs.Add($"SetEventFlag({AlertFlag}, OFF)");
                }
                instrs.Add($"EndUnconditionally(EventEndType.Restart)");
                Events.AddSimpleEvent(emevd, eventId++, instrs.Select(events.ParseAdd), EMEVD.Event.RestBehaviorType.Restart);
            }
        }
    }
}
