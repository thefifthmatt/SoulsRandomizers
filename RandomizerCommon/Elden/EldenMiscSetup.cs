using RandomizerCommon;
using RandomizerCommon.Properties;
using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;
using YamlDotNet.Core.Tokens;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Messages;
using static SoulsIds.Events;

namespace RandomizerCommon
{
    public class EldenMiscSetup
    {
        [Localize]
        private static readonly Text fogRunningError = new Text(
            "Error: Unrestricted item placement was enabled in Item Randomizer,\nbut Fog Gate Randomizer was not detected",
            "GameMenu_fogRunningError");
        [Localize]
        private static readonly Text dlcMissingSote = new Text(
            "Error: Shadow of the Erdtree was enabled in randomizer but your game doesn't have it.\nEnable it on Steam or else uncheck it in the DLC tab.",
            "GameMenu_dlcMissingSote");
        [Localize]
        private static readonly Text dlcMissingTarnished = new Text(
            "Error: Tarnished Pack was enabled in randomizer but your game doesn't have it.\nEnable it on Steam or else uncheck it in the DLC tab.",
            "GameMenu_dlcMissingTarnished");
        [Localize]
        private static readonly Text springsealIn = new Text(
            "A spiritspring has been unsealed in {0}",
            "GameMenu_springsealIn");
        [Localize]
        private static readonly Text springsealBy = new Text(
            "A spiritspring has been unsealed by {0}",
            "GameMenu_springsealBy");

        private record SealedSpring(string Map, int Area, bool By = false);

        private static readonly SortedDictionary<int, SealedSpring> sealedSprings = new()
        {
            // Rauh Ruins Courtyard (rauhruins_high)
            [2044460500] = new SealedSpring("m61_44_46_00", 69400),
            // Rauh Base (rauhbase_high)
            [2046460500] = new SealedSpring("m61_46_46_00", 69001),
            // Gravesite Plain to Scadu Altus
            [2049420600] = new SealedSpring("m61_49_42_00", 68000),
            // Scadu Altus, Scaduview Cross. Count as Moorth Highway (scadualtus_high)
            [2049440600] = new SealedSpring("m61_49_44_00", 690002, By: true),
            // Scadu Altus, Rabbath's Rise. Count as Church District Highroad
            [2051470600] = new SealedSpring("m61_51_47_00", 690005, By: true),
        };

        public static void EldenCommonPass(
            GameData game, RandomizerOptions opt, Messages messages, Events events, PermutationWriter.Result result = null)
        {
            // Resident speffects
            PARAM.Row baseSp = game.Params["SpEffectParam"][5020];
            for (int i = 0; i < 20; i++)
            {
                GameEditor.CopyRow(baseSp, game.AddRow("SpEffectParam", 6950 + i));
            }

            HashSet<(int, int)> deleteCommands = new HashSet<(int, int)>
            {
                (2003, 28),  // Achievement
                // (2007, 15),  // Tutorial popup
            };
            List<int> debugLots = new List<int>
            {
                997200, // Rowa
                997210, // Golden Rowa
                997220, // Rimed Rowa
                998400, // Cave Moss
                998410, // Budding Cave Moss
                998420, // Crystal Cave Moss
            };
            if (opt["nerfmalenia"])
            {
                foreach (PARAM.Row row in game.Params["SpEffectParam"].Rows)
                {
                    if (row.ID >= 18400 && row.ID < 18420)
                    {
                        // Exclude p2 heal
                        if (row.ID == 18404) continue;
                        row["changeHpRate"].Value = 0f;
                    }
                }
            }
            int gargSpread = 20003;
            int gargId = 477020003;
            PARAM.Row gargNerf = game.AddRow("SpEffectParam", gargId, 20003);
            gargNerf["changeHpRate"].Value = 0f;
            gargNerf["changeHpPoint"].Value = 0;
            if (opt["nerfgargoyles"])
            {
                foreach (PARAM.Row row in game.Params["AtkParam_Npc"].Rows.Where(r => r.ID == 4770860 || r.ID == 4770451))
                {
                    row["atkMag"].Value = (ushort)0;
                }
            }
            if (opt["nerflantern"] && !opt["enemy"])
            {
                // If not enabled in enemy randomizer, do it here.
                // It's more reliable to do it there since it can create NpcParam entries
                foreach (PARAM.Row row in game.Params["NpcParam"].Rows)
                {
                    int model = row.ID / 10000;
                    if (model == 5280)
                    {
                        game.AddNpcSpEffect(row, 20011471, true);
                    }
                }
            }
            if (false && opt["dlc"] && opt["nerfgank"])
            {
                EMEVD.Event constr = game.Emevds["m20_01_00_00"].Events.Find(ev => ev.ID == 0);
                if (constr == null) throw new Exception("Missing constructor in m20_01");
                OldParams pre = OldParams.Preprocess(constr);
                for (int j = 0; j < constr.Instructions.Count; j++)
                {
                    Instr instr = events.Parse(constr.Instructions[j], pre);
                    if (!instr.Init) continue;
                    if (instr.Callee != 20010778 || instr.Offset + 16 >= instr.Count) continue;
                    instr[instr.Offset + 16] = 0;
                    instr.Save(pre);
                    constr.Instructions[j] = instr.Val;
                    game.WriteEmevds.Add("m20_01_00_00");
                }
            }
            if (opt["sombermode"])
            {
                foreach (PARAM.Row row in game.Params["EquipMtrlSetParam"].Rows)
                {
                    int id = (int)row["materialId01"].Value;
                    int cat = (byte)row["materialCate01"].Value;
                    int num = (sbyte)row["itemNum01"].Value;
                    if (cat == 4 && id >= 10100 && id < 10110 && num > 1)
                    {
                        row["itemNum01"].Value = (byte)1;
                    }
                }
            }
            if (opt["nerfsh"])
            {
                PARAM.Row row = game.Params["EquipParamWeapon"][17030000];
                // Change Serpent-Hunter to match Meteorite Staff
                row["reinforceTypeId"].Value = (short)3000;
                row["materialSetId"].Value = 0;
                row["isCustom"].Value = (byte)0;
                for (int i = 1; i < 25; i++)
                {
                    row[$"originEquipWep{i}"].Value = -1;
                }
            }
            // Always Nerf Boc if we can, just because
            if (game.WriteESDs.Contains("m60_00_00_00")
                && game.Talk["m60_00_00_00"].TryGetValue("t223006000", out ESD boc)
                && boc.StateGroups.TryGetValue(2000, out Dictionary<long, ESD.State> machine))
            {
                foreach (ESD.State state in machine.Values)
                {
                    foreach (ESD.CommandCall command in state.EntryCommands)
                    {
                        if (command.CommandBank != 6) continue;
                        for (int i = 0; i < command.Arguments.Count; i++)
                        {
                            if (AST.DisassembleExpression(command.Arguments[i]).TryAsInt(out int arg) && arg == 50)
                            {
                                command.Arguments[i] = AST.AssembleExpression(AST.MakeVal(25));
                            }
                        }
                    }
                }
            }
            CharacterWriter.WriteUpgrade(messages, game);
            if (opt["weaponreqs"])
            {
                // Same fields used in CharacterWriter requirements gathering
                List<string> weaponFields = new List<string> { "properStrength", "properAgility", "properMagic", "properFaith", "properLuck" };
                foreach (PARAM.Row row in game.Params["EquipParamWeapon"].Rows)
                {
                    foreach (string field in weaponFields)
                    {
                        row[field].Value = (byte)0;
                    }
                }
                List<string> magicFields = new List<string> { "requirementIntellect", "requirementFaith", "requirementLuck" };
                foreach (PARAM.Row row in game.Params["Magic"].Rows)
                {
                    foreach (string field in magicFields)
                    {
                        row[field].Value = (byte)0;
                    }
                }
            }
            bool silent = opt["silent"];
            if (!opt["noenvbgm"])
            {
                // Some maps e.g. m34_12 Sealed Tunnel have a SoundRegion with BGM.
                // BgmPlaceInfo=320 (Tunnel), EnvPlaceInfo=340 (Tower), Region=180 (Tutorial?)
                // EnvPlaceType refers to Env_320_Tunnel, BgmBossChrIdConv refers to Bgm_320_Tunnel
                // Editing this param seems to account for most simple cases, though.
                // WwiseValueToStrParam_EnvPlaceType is presumably for ambient non-music sounds.
                List<string> silentBgmNames = new List<string>
                {
                    "Bgm_999_None", "Bgm_180_Tutorial", "Bgm_190_Ritual",
                    "Bgm_250_Nursery",
                    // Choose to not randomize these
                    "Bgm_560_MtDragon", "Bgm_562_FingerRuin",
                };
                List<string> calmBgmNames = new List<string>
                {
                    "Bgm_111_Hub", "Bgm_161_VolcanoHall", "Bgm_340_Tower", "Bgm_502_FortressPlainFes",
                    "Bgm_221_Npc405", "Bgm_563_MediumVil",
                    // A lot of combat happens here so don't include them, despite no battle layer
                    // We'll see if it's okay or not, maybe should have area size heuristics
                    // "Bgm_560_MtDragon", "Bgm_562_FingerRuin",
                };
                Dictionary<string, string> duplicateBgmNames = new Dictionary<string, string>
                {
                    ["Bgm_351_UgKingCity_Tomb"] = "Bgm_300_Catacombs",
                    ["Bgm_390_CliffTunnel"] = "Bgm_320_Tunnel",
                    ["Bgm_501_FortressPlain"] = "Bgm_500_Fortress",
                    ["Bgm_202_TowerRevival"] = "Bgm_201_TowerHigh",
                    ["Bgm_053_DarkTree"] = "Bgm_051_CemeteryLatter",
                };
                if (opt["dlc"] && !opt["dlcsilo"])
                {
                    // No unique DLC track
                    duplicateBgmNames["Bgm_430_Cave_B"] = "Bgm_310_Cave";
                }
                // Source to target
                Dictionary<string, string> bgmMapping = new Dictionary<string, string>();
                // Collect into groups
                SortedDictionary<(AreaSilo, bool), List<string>> allBgms = new();
                foreach (PARAM.Row row in game.Params["WwiseValueToStrParam_BgmBossChrIdConv"].Rows)
                {
                    string bgm = (string)row["ParamStr"].Value;
                    if (!bgm.StartsWith("Bgm") || silentBgmNames.Contains(bgm) || duplicateBgmNames.ContainsKey(bgm)) continue;
                    bool dlc = row.ID > 11000030;
                    if (dlc && !opt["dlc"]) continue;
                    AreaSilo silo = opt["dlc"] && opt["dlcsilo"] ? AreaSilo.ForDlc(dlc) : AreaSilo.Default;
                    bool calm = calmBgmNames.Contains(bgm);
                    Util.AddMulti(allBgms, (silo, calm), bgm);
                }
                // Shuffle
                Random bgmRandom = new Random((int)opt.Seed);
                foreach (List<string> bgms in allBgms.Values)
                {
                    List<string> targetBgms = bgms.ToList();
                    Util.Shuffle(bgmRandom, targetBgms);
                    foreach (string bgm in bgms)
                    {
                        bgmMapping[bgm] = Util.PopDeranged(targetBgms, bgm);
                    }
                }
                // The logic is a bit more complicated, so make a transfer map instead of popping in place
                if (!silent) Console.WriteLine("-- Level BGM placements");
                foreach (PARAM.Row row in game.Params["WwiseValueToStrParam_BgmBossChrIdConv"].Rows)
                {
                    string bgm = (string)row["ParamStr"].Value;
                    if (!duplicateBgmNames.TryGetValue(bgm, out string sourceBgm)) sourceBgm = bgm;
                    if (!bgmMapping.TryGetValue(sourceBgm, out string newBgm)) continue;
                    if (!silent) Console.WriteLine($"Replacing {bgm}: {newBgm}");
                    row["ParamStr"].Value = newBgm;
                }
                if (!silent) Console.WriteLine();
            }
            if (opt["snowfast"])
            {
                if (opt["printjumps"])
                {
                    foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
                    {
                        MSBE msb = entry.Value;
                        foreach (MSBE.Region.MountJump r in msb.Regions.MountJumps)
                        {
                            Console.WriteLine($"{entry.Key} MountJump {r.Name}: height {r.JumpHeight}, radius {(r.Shape as MSB.Shape.Sphere)?.Radius}, unk {r.UnkT04}");
                        }
                        foreach (MSBE.Region.MountJumpFall r in msb.Regions.MountJumpFalls)
                        {
                            Console.WriteLine($"{entry.Key} MountJumpFall {r.Name}: height {(r.Shape as MSB.Shape.Cylinder)?.Height}, radius, {(r.Shape as MSB.Shape.Cylinder)?.Radius}");
                        }
                    }
                }

                // c4160 is noble's dog

                MiscMapEdits edits = new MiscMapEdits(game);
                // maps["m60_49_53_00"].Parts.Collisions.RemoveAll(col => col.UnkT00 == 13 || col.UnkT00 == 15);
                game.EldenMaps["m60_50_53_00"].Parts.Collisions.RemoveAll(col => col.UnkT00 == 13 || col.UnkT00 == 15);
                // game.WriteMSBs.Add("m60_50_53_00");
                // Zamor to Flame Peak
                edits.AddOverworldMountJump("m60_50_53_00", new Vector3(-150.735f, 1562.571f, 89.680f), 72, 10, 85, new Vector3(-12.105f, 51.044f, 0));
                // Flame Peak by hands
                edits.AddOverworldMountJump("m60_51_54_00", new Vector3(-16.127f, 1694.095f, -75.314f), 30, 10, 30, new Vector3(0, 10.587f, 7.972f));
                // Merchant skip. more left position -34.994f, 1578.808f, -91.147f
                edits.AddOverworldMountJump("m60_51_56_00", new Vector3(-18.043f, 1579.316f, -92.892f), 30, 10, 30);
                // Valley overlook. Could also do golden tree. Any routing would involve skipping either the tree or grace,
                // so leave it manual for now.
                // edits.AddOverworldMountJump("m60_52_57_00", new Vector3(-110.291f, 1608.516f, -49.313f), 50, 10, 50);
                // Castle sol spring (if not dungeon crawl)
                if (!opt["crawl"])
                {
                    // In front
                    edits.AddOverworldMountJump("m60_51_57_00", new Vector3(-0.156f, 1597.957f, 23.891f), 70, 15, 70);
                    // In back
                    edits.AddOverworldMountJump("m60_51_57_00", new Vector3(103.862f, 1611.682f, 71.715f), 54, 15, 60, new Vector3(0, 0, 4.945f));
                }
                // Warp gate between flame peak and overlook
                // Initial: new Vector3(119.658f, 1670.150f, -47.243f), new Vector3(0, -7.397f, 0),
                edits.AddOverworldSendingGate(
                    "m60_51_57_00",
                    new Vector3(119.450f, 1670.060f, -48.398f), new Vector3(0, -7.397f, -2.999f),
                    802212020, GameData.EldenRingBase + 2020, "m60_52_53_00", 1052530980);
                // Initial: new Vector3(-82.604f, 1801.689f, -40.085f), new Vector3(0, 178.482f, 0),
                edits.AddOverworldSendingGate(
                    "m60_52_53_00",
                    new Vector3(-81.921f, 1801.741f, -38.394f), new Vector3(0, -179.998f, -8.119f),
                    802212021, GameData.EldenRingBase + 2020, "m60_51_57_00", 1051570980);
                // Finally, torrent on East Altus Divine Tower.
                string tower = "m34_14_00_00";
                game.WriteMSBs.Add(tower);
                game.WriteEmevds.Add(tower);
                foreach (MSBE.Part.Collision col in game.EldenMaps[tower].Parts.Collisions)
                {
                    // 3414000, 3414002, 3414010, 3414011, 3414012
                    // Don't allow Fell Twins, or ideally the tower part itself, but mainly Fell Twins
                    // if (col.PlayRegionID == 3414000 || col.PlayRegionID == 3414002)
                    // if (col.PlayRegionID == 3414011) // || col.PlayRegionID == 3414012)
                    // h000100 h000101 - starting elevator
                    // h000110
                    // h000200 - bridge after morgott
                    // h000300 h000301 - big elevator room
                    // h000500 h000501
                    if (col.Name == "h000200" || col.Name == "h000110")
                    {
                        col.DisableTorrent = false;
                    }
                }
                // Torrent dismount based on Caria Manor's 1035501650. Take pillars of doorway
                MSBE.Part.Asset blue = edits.AddDerivativeAsset(
                    tower, "AEG099_090", "AEG022_020_1002",
                    new Vector3(416.599f, 30.300f, -200.000f), new Vector3(0, 90, 0));
                blue.EntityID = 802212030;
                blue.AssetSfxParamRelativeID = -1;
                EMEVD.Event towerConstr = game.Emevds[tower].Events.Find(ev => ev.ID == 0);
                if (towerConstr != null)
                {
                    towerConstr.Instructions.Add(new EMEVD.Instruction(2000, 6, new List<object>
                        {
                            0, 900005610, blue.EntityID, 100, 800, 0
                        }));
                }
                // tower.Parts.Assets.Find(a => a.Name == "AEG027_033_0500").AssetUnk1.DisableTorrentAssetOnly = true;
            }
            // Map from stone unlock to
            Dictionary<int, int> unlockSpringFlags = new();
            if (opt["dlc"] && opt["springseals"])
            {
                if (opt["printjumps"])
                {
                    foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
                    {
                        MSBE msb = entry.Value;
                        foreach (MSBE.Region.LockedMountJump r in msb.Regions.LockedMountJumps)
                        {
                            Console.WriteLine($"{entry.Key} MountJump {r.Name}: height {r.JumpHeight}, radius {(r.Shape as MSB.Shape.Sphere)?.Radius}, unk4 {r.UnkT04}, unk8 {r.UnkT08}");
                        }
                        foreach (MSBE.Region.LockedMountJumpFall r in msb.Regions.LockedMountJumpFalls)
                        {
                            Console.WriteLine($"{entry.Key} MountJumpFall {r.Name}: height {(r.Shape as MSB.Shape.Cylinder)?.Height}, radius, {(r.Shape as MSB.Shape.Cylinder)?.Radius}, unk8 {r.UnkT08}");
                        }
                    }
                }
                // Unlock common_func event 90005638(X0_4 flag, X4_4 hitby asset, X8_4 spring asset)
                // e.g. in m61_44_46_00 (2044460500, 2044461500, 2044461501)
                // To randomize them, change the flag associated with the spring itself, and change error message.
                void addCommonFunc(EMEVD emevd, int id, IEnumerable<string> instrs, EMEVD.Event.RestBehaviorType rest = EMEVD.Event.RestBehaviorType.Default)
                {
                    EMEVD.Event ev = new EMEVD.Event(id, rest);
                    OldParams pre = OldParams.NewEvent(ev);
                    foreach (string ins in instrs)
                    {
                        (EMEVD.Instruction instr, List<EMEVD.Parameter> ps) = events.ParseAddArg(ins);
                        ev.Instructions.Add(instr);
                        pre.AddParameters(instr, ps);
                    }
                    pre.Postprocess();
                    emevd.Events.Add(ev);
                }
                // Actually use Events here as this is too complicated
                // 59005850(X0_4 seal flag, X4_4 hitby asset, X8_4 msgid)
                // 59005851(X0_4 spring flag, X4_4 spring asset)
                addCommonFunc(game.Emevds["common_func"], 59005850, new[]
                {
                    "GotoIfEventFlag(Label.Label0, OFF, TargetEventFlagType.EventFlag, X0_4)",
                    "ChangeAssetEnableState(X4_4, Disabled)",
                    "EndUnconditionally(EventEndType.End)",
                    "Label0()",
                    "SetAssetInvulnerability(X4_4, Enabled)",
                    "DeleteAssetfollowingSFX(X4_4, true)",
                    "CreateAssetfollowingSFX(X4_4, 100, 841075)",
                    "IfPlayerIsInWorldType(AND_01, WorldType.OwnWorld)",
                    "IfAssetHitBy(AND_01, X4_4, 20000)",
                    "IfConditionGroup(MAIN, PASS, AND_01)",
                    "SetAssetInvulnerability(X4_4, Disabled)",
                    "RequestAssetDestruction(X4_4, 0)",
                    "DeleteAssetfollowingSFX(X4_4, true)",
                    "CreateAssetfollowingSFX(X4_4, 100, 841072)",
                    "WaitFixedTimeRealFrames(1)",
                    "SetEventFlag(TargetEventFlagType.EventFlag, X0_4, ON)",
                    "WaitFixedTimeSeconds(2)",
                    "DisplayGenericDialog(X8_4, PromptType.OKCANCEL, NumberofOptions.OneButton, 0, 100)",
                }, EMEVD.Event.RestBehaviorType.Restart);
                addCommonFunc(game.Emevds["common_func"], 59005851, new[]
                {
                    "GotoIfEventFlag(Label.Label0, OFF, TargetEventFlagType.EventFlag, X0_4)",
                    "ChangeAssetEnableState(X4_4, Disabled)",
                    "EndUnconditionally(EventEndType.End)",
                    "Label0()",
                    "SetAssetInvulnerability(X4_4, Enabled)",
                    "DeleteAssetfollowingSFX(X4_4, true)",
                    "CreateAssetfollowingSFX(X4_4, 100, 841076)",
                    "IfEventFlag(AND_01, ON, TargetEventFlagType.EventFlag, X0_4)",
                    "IfConditionGroup(MAIN, PASS, AND_01)",
                    "DeleteAssetfollowingSFX(X4_4, true)",
                    "CreateAssetfollowingSFX(X4_4, 100, 841072)",
                    "SetAssetInvulnerability(X4_4, Disabled)",
                    "WaitFixedTimeRealFrames(1)",
                    "RequestAssetDestruction(X4_4, 0)",
                }, EMEVD.Event.RestBehaviorType.Restart);
                game.WriteEmevds.Add("common_func");

                // 2020030 Somewhere, a spiritspring has been unsealed
                // Mapping from spring to seal, for editing flags
                SortedDictionary<int, int> springMapping = new();
                // Mapping from seal to spring, for editing message
                SortedDictionary<int, int> sealMapping = new();
                List<int> springIds = sealedSprings.Keys.ToList();
                List<int> sealIds = springIds.ToList();
                Random sealRandom = new Random((int)opt.Seed + 38);
                Util.Shuffle(sealRandom, sealIds);
                for (int i = 0; i < springIds.Count; i++)
                {
                    springMapping[springIds[i]] = sealIds[i];
                    sealMapping[sealIds[i]] = springIds[i];
                }
                if (!silent) Console.WriteLine("-- Sealed spiritsprings");
                int msgBase = 265030400;
                foreach ((int locId, SealedSpring loc) in sealedSprings)
                {
                    int unsealsId = sealMapping[locId];
                    int unsealedById = springMapping[locId];
                    string springName = game.ItemFMGs["PlaceName"][loc.Area];
                    string sealName = game.ItemFMGs["PlaceName"][sealedSprings[unsealedById].Area];
                    if (!silent) Console.WriteLine($"{springName}: unsealed at {sealName}");
                    // Make text for the seal itself, based on what it unseals (the target's flag is changed)
                    SealedSpring unseals = sealedSprings[unsealsId];
                    Text text = unseals.By ? springsealBy : springsealIn;
                    int msgId = msgBase++;
                    messages.SetFMGEntry(game, FMGCategory.Menu, "EventTextForMap", msgId, text, new FMGArg { Category = FMGCategory.Item, Name = "PlaceName", ID = unseals.Area, BaseText = "a location" });
                    // Edit flag at the spring's location. The seal does not change except for the event being swapped out, also msg id
                    List<string> mapIds = new() { loc.Map };
                    string dupeMap = GameData.EldenDupeMap(loc.Map);
                    if (dupeMap != loc.Map && game.Emevds.ContainsKey(dupeMap))
                    {
                        mapIds.Add(dupeMap);
                    }
                    foreach (string map in mapIds)
                    {
                        EMEVD emevd = game.Emevds[map];
                        MSBE msb = game.EldenMaps[map];
                        foreach (MSBE.Region.LockedMountJump r in msb.Regions.LockedMountJumps)
                        {
                            if (r.UnkT08 == locId) r.UnkT08 = unsealedById;
                        }
                        foreach (MSBE.Region.LockedMountJumpFall r in msb.Regions.LockedMountJumpFalls)
                        {
                            if (r.UnkT08 == locId) r.UnkT08 = unsealedById;
                        }
                        // This happens to always be in event 0, the first event
                        EMEVD.Event constr = emevd.Events.Find(ev => ev.ID == 0);
                        if (constr == null) continue;
                        int sealAsset = 0, springAsset = 0;
                        for (int j = 0; j < constr.Instructions.Count; j++)
                        {
                            Instr instr = events.Parse(constr.Instructions[j]);
                            if (!instr.Init) continue;
                            if (instr.Callee != 90005638 || instr.Count + instr.Offset < 3) continue;
                            int flag = (int)instr[instr.Offset];
                            if (flag == locId)
                            {
                                sealAsset = (int)instr[instr.Offset + 1];
                                springAsset = (int)instr[instr.Offset + 2];
                                constr.Instructions[j] = new EMEVD.Instruction(1014, 69);
                            }
                        }
                        // 59005850(X0_4 seal flag, X4_4 hitby asset, X8_4 msgid)
                        // 59005851(X0_4 spring flag, X4_4 spring asset)
                        if (sealAsset > 0)
                        {
                            // Seal flag remains the same
                            constr.Instructions.Add(new EMEVD.Instruction(2000, 6, new List<object> { 0, 59005850, locId, sealAsset, msgId }));
                        }
                        if (springAsset > 0)
                        {
                            // Spring flag is changed
                            constr.Instructions.Add(new EMEVD.Instruction(2000, 6, new List<object> { 0, 59005851, unsealedById, springAsset }));
                        }
                        game.WriteMSBs.Add(map);
                        game.WriteEmevds.Add(map);
                        // Console.WriteLine($"{map} {locId} -> ({sealAsset}, {springAsset}), msg {msgId}");
                    }
                }
                if (!silent) Console.WriteLine();
            }

            if (opt["rainbow"])
            {
                Random matRandom = new Random((int)opt.Seed);
                PARAM matParam = game.Params["MaterialExParam"];
                PARAM.Row baseRow = matParam[20300];
                List<int> matParams = new();
                int vi = 0;
                foreach (Color color in webColorValues.Select(e => Color.FromKnownColor(e)))
                {
                    PARAM.Row row = GameEditor.AddRow(matParam, 992000 + vi++, baseRow);
                    row["materialParamValue0"].Value = color.R / 255.0f;
                    row["materialParamValue1"].Value = color.G / 255.0f;
                    row["materialParamValue2"].Value = color.B / 255.0f;
                    row["materialParamValue4"].Value = 1.0f;
                    matParams.Add(row.ID);
                }
                foreach (PARAM.Row npc in game.Params["NpcParam"].Rows)
                {
                    int model = npc.ID / 10000;
                    if (npc.ID >= 90010000 || model == 3200 || model == 3210) continue;
                    for (int i = 0; i <= 4; i++)
                    {
                        int mat = (int)npc[$"residentMaterialExParamId0{i}"].Value;
                        if (mat == -1)
                        {
                            npc[$"residentMaterialExParamId0{i}"].Value = Util.Choice(matRandom, matParams);
                            break;
                        }
                    }
                }
            }
            // 71801 is graveyard flag, 102 is "definitely in limgrave"?
            // This one should be opening the graveyard exit but it seems to activate straight away (because endif tutorial?)
            int mapUnlockFlag = 101; // 18000021;
            if (opt["allmaps"])
            {
                FMGX npcNames = game.ItemFMGs["NpcName"];
                HashSet<int> merchantNames = new HashSet<int>(
                    npcNames.Entries.Where(e => e.Text != null && e.Text.Contains("Merchant")).Select(e => e.ID));
                void rewriteMerchantIcons(PARAM.Row row)
                {
                    for (int i = 1; i <= 8; i++)
                    {
                        // Ignore npc vs place name, as the ids are distinct probably
                        int name = (int)row[$"textId{i}"].Value;
                        if (merchantNames.Contains(name))
                        {
                            row[$"textEnableFlagId{i}"].Value = (uint)mapUnlockFlag;
                            int giftFlag = 0;
                            // Exclusion for Kale
                            if (result != null && result.MerchantGiftFlags.TryGetValue(name, out giftFlag) && giftFlag != 400049)
                            {
                                row[$"textDisableFlagId{i}"].Value = (uint)giftFlag;
                            }
                        }
                    }
                }
                foreach (PARAM.Row row in game.Params["WorldMapPointParam"].Rows)
                {
                    if ((int)row["textId1"].Value > 0)
                    {
                        row["eventFlagId"].Value = (uint)mapUnlockFlag;
                    }
                    rewriteMerchantIcons(row);
                }
                foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                {
                    rewriteMerchantIcons(row);
                }
            }

            messages.SetFMGEntry(
                game, FMGCategory.Menu, "EventTextForMap",
                RuntimeParamChecker.FogMessageId, fogRunningError);
            messages.SetFMGEntry(
                game, FMGCategory.Menu, "EventTextForMap",
                RuntimeParamChecker.DlcMessageId, dlcMissingSote);
            messages.SetFMGEntry(
                game, FMGCategory.Menu, "EventTextForMap",
                RuntimeParamChecker.TarnishedMessageId, dlcMissingTarnished);
            // Just english for now
            // Failed to save game.\nSave data is corrupted.\n\nSelect "OK" to try again.
            game.MenuFMGs["GR_System_Message_win64"][401106] = "Failed to save game. Save data is corrupted.\n\nIf there was a recent Elden Ring update, you must first launch\nElden Ring WITHOUT randomizer to migrate your save data.\n\nAlternatively, use RandomizerCrashFix.dll to patch out this check.";

            EMEVD.Instruction debugLot(int i)
            {
                return new EMEVD.Instruction(2003, 4, new List<object> { debugLots[i] });
            }
            // game.Params["AssetEnvironmentGeometryParam"][277011]["HP"].Value = (short)500;
            foreach (KeyValuePair<string, EMEVD> entry in game.Emevds)
            {
                string map = entry.Key;
                EMEVD emevd = entry.Value;
                void addNewEvent(int id, IEnumerable<EMEVD.Instruction> instrs, EMEVD.Event.RestBehaviorType rest = EMEVD.Event.RestBehaviorType.Default)
                {
                    if (emevd.Events.Any(e => e.ID == id)) return;
                    AddSimpleEvent(emevd, id, instrs, rest);
                }
                foreach (EMEVD.Event e in entry.Value.Events)
                {
                    // Some custom edits
                    if (false && e.ID == 6905)
                    {
                        e.Instructions.Insert(1, debugLot(1));
                        e.Instructions.Insert(0, debugLot(0));
                    }
                    for (int i = 0; i < e.Instructions.Count; i++)
                    {
                        EMEVD.Instruction ins = e.Instructions[i];
                        // Instr instr = events.Parse(e.Instructions[i]);
                        bool delete = false;
                        if (deleteCommands.Contains((ins.Bank, ins.ID)))
                        {
                            delete = true;
                        }
                        if (delete)
                        {
                            EMEVD.Instruction newInstr = new EMEVD.Instruction(1014, 69);
                            e.Instructions[i] = newInstr;
                            e.Parameters = e.Parameters.Where(p => p.InstructionIndex != i).ToList();
                            game.WriteEmevds.Add(entry.Key);
                        }
                    }
#if DEBUG
                    if (entry.Key == "m12_02_00_00" && opt["cheat_shortcut"])
                    {
                        if (e.ID == 12022609 || e.ID == 12022629)
                        {
                            int gotoIndex = e.Instructions.FindIndex(ins => ins.Bank == 1003 && ins.ID == 101);
                            if (gotoIndex >= 0)
                            {
                                e.Instructions.Insert(gotoIndex, new EMEVD.Instruction(1000, 3, new List<object> { (byte)1 }));
                            }
                        }
                    }
#endif

                }
                if (entry.Key == "common")
                {
#if DEBUG
                    // Testing event
                    addNewEvent(19003105, new List<EMEVD.Instruction>
                    {
                        // If event flag, give lot
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 110 }), debugLot(0),
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 197 }), debugLot(1),
                        // new EMEVD.Instruction(1003, 1, new List<object> { (byte)1, (byte)0, (byte)0, 177 }), debugLot(2),
                        // Wait for event flag, give lot
                        // new EMEVD.Instruction(3, 0, new List<object> { (byte)0, (byte)1, (byte)0, 187 }), debugLot(3),
                        // Item posession check
                        // IfPlayerHasdoesntHaveItem(OR01, ItemType.Gem = 4, X, OwnershipState.Owns = 1)
                        // new EMEVD.Instruction(3, 4, new List<object> { (byte)5, (byte)4, 60100, (byte)1 }),
                        // new EMEVD.Instruction(1000, 1, new List<object>{ (byte)1, (byte)0, (byte)5 }), debugLot(0),
                    });
                    List<string> gifts = new List<string>();
                    // gifts.Add("Rotten Breath");
                    // gifts.Add("Mimic Tear Ashes +10");
                    // gifts.AddRange(new[] { "Godskin Stitcher", "Latenna the Albinauric +1" });
                    // gifts.AddRange(new[] { "Gargoyle's Greatsword", "Redmane Knight Helm", "Redmane Surcoat" });
                    // gifts.AddRange(new[] { "Rivers of Blood", "Grafted Blade Greatsword" });
                    // gifts.AddRange(new List<string> { "Mushroom Crown", "Mushroom Head", "Mushroom Body", "Mushroom Arms", "Mushroom Legs" });
                    // Black raptor set also pretty cool. And fingerprint set
                    Dictionary<ItemKey, int> giftAmounts = gifts.ToDictionary(g => game.ItemForName(g), g => 1);
                    // giftAmounts[new ItemKey(ItemType.GOOD, 8136)] = 1;
                    // game.Params["ItemLotParam_map"][34110080]["lotItemId01"].Value = 8151;
                    // giftAmounts[new ItemKey(ItemType.GOOD, 20760)] = 10; // Mushroom
                    // giftAmounts[new ItemKey(ItemType.GOOD, 20651)] = 10; // Trina Lily
                    if (opt["cheatgift"] && giftAmounts.Count > 0)
                    {
                        int lotId = 12020840;
                        PARAM.Row row = null;
                        ItemKey checkItem = null;
                        foreach (KeyValuePair<ItemKey, int> giftEntry in giftAmounts)
                        {
                            if (row == null)
                            {
                                row = game.Params["ItemLotParam_map"][lotId];
                            }
                            else
                            {
                                row = game.AddRow("ItemLotParam_map", lotId + 1, lotId);
                                lotId++;
                            }
                            ItemKey item = giftEntry.Key;
                            row["lotItemId01"].Value = item.ID;
                            row["lotItemCategory01"].Value = (int)game.LotValues[item.Type];
                            row["lotItemNum01"].Value = giftEntry.Value;
                            if (checkItem == null) checkItem = item;
                        }
                        game.Params["ItemLotParam_map"].Rows.Sort((a, b) => (a.ID.CompareTo(b.ID)));
                        // game.Params["ItemLotParam_map"][12020840]["ItemLotId1"].Value = 207010;
                        addNewEvent(19003106, new List<EMEVD.Instruction>
                        {
                            // IfPlayerHasdoesntHaveItem(MAIN, ItemType.Goods = 3, 207010, OwnershipState.Owns = 0)
                            new EMEVD.Instruction(3, 4, new List<object> { (byte)0, (byte)checkItem.Type, checkItem.ID, (byte)0 }),
                            // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventIDSlotNumber, 0)
                            // new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)2, 0 }),
                            // DirectlyGivePlayerItem(ItemType.Goods = 3, 207010, 12027885, 1), yet again fails
                            // new EMEVD.Instruction(2003, 43, new List<object> { (byte)3, 207000, 12027780, 1 }),
                            // SetEventFlag(TargetEventFlagType.EventFlag, 12027840, OFF)
                            new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 12027840, (byte)0 }),
                            // Item lot
                            new EMEVD.Instruction(2003, 4, new List<object> { 12020840 }),
                        });
                    }
                    if (opt["cheathp"])
                    {
                        addNewEvent(19003107, new List<EMEVD.Instruction>
                        {
                            // 10000: self. 35000: only helpers.
                            // SetSpEffect(35000, 110)
                            // new EMEVD.Instruction(2004, 8, new List<object> { 20000, 110 }),
                            // new EMEVD.Instruction(2004, 8, new List<object> { 35000, 110 }),
                            // Disable damage
                            new EMEVD.Instruction(2004, 39, new List<object> { 10000, 0 }),
                            new EMEVD.Instruction(2004, 39, new List<object> { 35000, 0 }),
                            // Scale damage
                            // new EMEVD.Instruction(2004, 8, new List<object> { 20000, 7200 }),
                            // new EMEVD.Instruction(2004, 8, new List<object> { 35000, 7200 }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)2 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                        });
                    }
                    if (opt["cheatimm"])
                    {
                        addNewEvent(19003109, new List<EMEVD.Instruction>
                        {
                            // 10000: self. 35000: only helpers.
                            new EMEVD.Instruction(2004, 12, new List<object> { 10000, 1 }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)2 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                        });
                    }
                    if (opt["bonfire"])
                    {
                        List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                        foreach (PARAM.Row row in game.Params["BonfireWarpParam"].Rows)
                        {
                            uint flag = (uint)row["eventflagId"].Value;
                            if (flag / 10000 == 7)
                            {
                                instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, flag, (byte)1 }));
                            }
                        }
                        addNewEvent(19003108, instrs);
                    }
#endif
                    // End debug

                    // Event for Varre
                    // Use 19003110 as base for "real" stuff. 19003130 in PermutationWriter
                    if (opt["varre"])
                    {
                        addNewEvent(19003110, new List<EMEVD.Instruction>
                        {
                            // The dialogue trigger is 1035449207 and it sets 1035449235 (progress quest) and 3198 ("update me" flag)
                            // EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 1035449235)
                            new EMEVD.Instruction(1003, 2, new List<object> { (byte)0, (byte)1, (byte)0, 1035449235 }),
                            // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 1035449207)
                            new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, 1035449207 }),
                            // debugLot(2),
                            // SetEventFlag(TargetEventFlagType.EventFlag, 1035449235, ON)
                            // SetEventFlag(TargetEventFlagType.EventFlag, 3198, ON)
                            new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 1035449235, (byte)1 }),
                            new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 3198, (byte)1 }),
                        });
                    }

                    if (opt["allmaps"])
                    {
                        // Event 1600 does map gifts, but just piggyback on item flags
                        // Mapping from item id to item flag. For now though just use map-activated flags directly...
                        SortedDictionary<int, int> mapFlags = new SortedDictionary<int, int>
                        {
                            [8600] = 62010,  // Map: Limgrave, West
                            [8601] = 62011,  // Map: Weeping Peninsula
                            [8602] = 62012,  // Map: Limgrave, East
                            [8603] = 62020,  // Map: Liurnia, East
                            [8604] = 62021,  // Map: Liurnia, North
                            [8605] = 62022,  // Map: Liurnia, West
                            [8606] = 62030,  // Map: Altus Plateau
                            [8607] = 62031,  // Map: Leyndell, Royal Capital
                            [8608] = 62032,  // Map: Mt. Gelmir
                            [8609] = 62040,  // Map: Caelid
                            [8610] = 62041,  // Map: Dragonbarrow
                            [8611] = 62050,  // Map: Mountaintops of the Giants, West
                            [8612] = 62051,  // Map: Mountaintops of the Giants, East
                            [8613] = 62060,  // Map: Ainsel River
                            [8614] = 62061,  // Map: Lake of Rot
                            [8615] = 62063,  // Map: Siofra River
                            [8616] = 62062,  // Map: Mohgwyn Palace
                            [8617] = 62064,  // Map: Deeproot Depths
                            [8618] = 62052,  // Map: Consecrated Snowfield
                                             // DLC
                            [2008600] = 62080, // Map: Gravesite Plain
                            [2008601] = 62081, // Map: Scadu Altus
                            [2008602] = 62082, // Map: Southern Shore
                            [2008603] = 62083, // Map: Rauh Ruins
                            [2008604] = 62084, // Map: Abyss
                        };
                        // Maybe don't unlock DLC map stuff if not enabled? Doesn't make a huge difference

                        // Simpler event, just wait for entry into Limgrave
                        List<EMEVD.Instruction> instrs = new List<EMEVD.Instruction>();
                        // IfEventFlag(MAIN, ON, TargetEventFlagType.EventFlag, 102)
                        instrs.Add(new EMEVD.Instruction(3, 0, new List<object> { (sbyte)0, (byte)1, (byte)0, mapUnlockFlag }));
                        foreach (KeyValuePair<int, int> mapFlag in mapFlags)
                        {
                            int flag = mapFlag.Value; // + 1000: just used for notification evidently
                                                      // SetEventFlag(TargetEventFlagType.EventFlag, flag, ON)
                            instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, flag, (byte)1 }));
                        }
                        // Underground maps visible
                        instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 82001, (byte)1 }));
                        // DLC maps visible
                        instrs.Add(new EMEVD.Instruction(2003, 66, new List<object> { (byte)0, 82002, (byte)1 }));
                        addNewEvent(19003111, instrs);
                        EMEVD.Event grantEvent = emevd.Events.Find(e => e.ID == 1600);
                        if (grantEvent != null)
                        {
                            OldParams pre = OldParams.Preprocess(grantEvent);
                            grantEvent.Instructions.RemoveAll(ins => ins.Bank == 2003 && ins.ID == 66);
                            pre.Postprocess();
                        }
                    }

                    if (opt["fog"])
                    {
                        addNewEvent(19003112, new List<EMEVD.Instruction>
                        {
                            // DisplayGenericDialog(msg, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5);
                            new EMEVD.Instruction(2007, 1, new List<object> { RuntimeParamChecker.FogMessageId, (short)1, (short)6, 0, 5f }),
                            // WaitFixedTimeSeconds
                            new EMEVD.Instruction(1001, 0, new List<object> { (float)30 }),
                            // EndUnconditionally(EventEndType.Restart)
                            new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 })
                        });
                    }
                    // This commonly softlocks people when they don't realize enemies etc are missing, so add warning
                    if (opt["dlc"])
                    {
                        events.AddSimpleEvent(emevd, 19003120, new[]
                        {
                            "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 6951)",
                            $"DisplayGenericDialog({RuntimeParamChecker.DlcMessageId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5)",
                        });
                    }
                    if (opt["tarnished"])
                    {
                        events.AddSimpleEvent(emevd, 19003121, new[]
                        {
                            "EndIfEventFlag(EventEndType.End, ON, TargetEventFlagType.EventFlag, 6953)",
                            $"DisplayGenericDialog({RuntimeParamChecker.TarnishedMessageId}, PromptType.OKCANCEL, NumberofOptions.NoButtons, 0, 5)",
                        });
                    }

                    if (opt["dlc"] && opt["woodsfast"])
                    {
                        // Easier than parsing the instructions I guess
                        emevd.Events.RemoveAll(ev => ev.ID == 1070);
                    }
                    game.WriteEmevds.Add(entry.Key);
                }
                // As other conditions are added here, PermutationWriter might need to have more of this logic
                int endRunes = 0;
                if (opt["runereq"])
                {
                    endRunes = 7;
                }
                else if (opt.GetInt(RandomizerOptions.IntOpt.RunesEnd, out int runeOpt))
                {
                    endRunes = runeOpt;
                }
                if (entry.Key == "m19_00_00_00" && endRunes > 0)
                {
                    int runeFlag = 180 + endRunes;
                    EMEVD.Event fog = emevd.Events.Find(ev => ev.ID == 19002500);
                    if (fog == null) throw new Exception($"Couldn't locate event in {entry.Key} to make final boss require all Great Runes");
                    OldParams pre = OldParams.Preprocess(fog);
                    int labelIndex = fog.Instructions.FindIndex(ins => ins.Bank == 1014 && ins.ID == 1);
                    int sfxIndex = fog.Instructions.FindIndex(Math.Max(labelIndex, 0), ins => ins.Bank == 2006 && ins.ID == 4);
                    if (labelIndex < 0 || sfxIndex < 0 || sfxIndex != labelIndex + 1)
                    {
                        throw new Exception($"Couldn't locate index to make final boss require all Great Runes ({labelIndex} {sfxIndex})");
                    }
                    fog.Instructions.InsertRange(sfxIndex, new List<EMEVD.Instruction>
                    {
                        // SkipIfEventFlag(<lines>, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(1003, 1, new List<object> { (byte)6, (byte)1, (byte)0, runeFlag }),
                        // 9320 = Examine
                        // IfActionButton(OR05, 9320, 19001500)
                        new EMEVD.Instruction(3, 24, new List<object> { (sbyte)-5, 9320, 19001500 }),
                        // IfEventFlag(OR05, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(3, 0, new List<object> { (sbyte)-5, (byte)1, (byte)0, runeFlag }),
                        // IfConditionGroup(MAIN, ON, OR05)
                        new EMEVD.Instruction(0, 0, new List<object> { (sbyte)0, (byte)1, (sbyte)-5 }),
                        // EndIfEventFlag(EventEndType.Restart, ON, TargetEventFlagType.EventFlag, 187)
                        new EMEVD.Instruction(1003, 2, new List<object> { (byte)1, (byte)1, (byte)0, runeFlag }),
                        // WaitFixedTimeSeconds(0.5)
                        // new EMEVD.Instruction(1001, 0, new List<object> { (float)0.5 }),
                        // 20003 = You cannot proceed without more Great Runes
                        // DisplayGenericDialog(20003, PromptType.YESNO = 0, NumberofOptions.NoButtons = 6, 19001500, 3)
                        new EMEVD.Instruction(2007, 1, new List<object> { 20003, (short)0, (short)6, 19001500, 3f }),
                        // EndUnconditionally(EventEndType.Restart)
                        new EMEVD.Instruction(1000, 4, new List<object> { (byte)1 }),
                    });
                    pre.Postprocess();

                    game.WriteEmevds.Add(entry.Key);
                }
            }
        }

        // https://learn.microsoft.com/en-us/dotnet/api/system.windows.media.colors?view=windowsdesktop-8.0
        // Remove most green, gray, and off-white
        private static readonly List<KnownColor> webColorValues = new List<KnownColor>
        {
            KnownColor.AntiqueWhite,
            KnownColor.Aqua,
            KnownColor.Aquamarine,
            KnownColor.Blue,
            KnownColor.BlueViolet,
            KnownColor.Brown,
            KnownColor.BurlyWood,
            KnownColor.CadetBlue,
            KnownColor.Chocolate,
            KnownColor.Coral,
            KnownColor.CornflowerBlue,
            KnownColor.Crimson,
            KnownColor.Cyan,
            KnownColor.DarkBlue,
            KnownColor.DarkCyan,
            KnownColor.DarkGoldenrod,
            KnownColor.DarkGray,
            KnownColor.DarkGreen,
            KnownColor.DarkKhaki,
            KnownColor.DarkMagenta,
            KnownColor.DarkOliveGreen,
            KnownColor.DarkOrange,
            KnownColor.DarkOrchid,
            KnownColor.DarkRed,
            KnownColor.DarkSalmon,
            KnownColor.DarkSeaGreen,
            KnownColor.DarkSlateBlue,
            KnownColor.DarkSlateGray,
            KnownColor.DarkTurquoise,
            KnownColor.DarkViolet,
            KnownColor.DeepPink,
            KnownColor.DeepSkyBlue,
            KnownColor.DimGray,
            KnownColor.DodgerBlue,
            KnownColor.Firebrick,
            KnownColor.ForestGreen,
            KnownColor.Fuchsia,
            KnownColor.Gold,
            KnownColor.Goldenrod,
            KnownColor.Green,
            KnownColor.Honeydew,
            KnownColor.HotPink,
            KnownColor.IndianRed,
            KnownColor.Indigo,
            KnownColor.Khaki,
            KnownColor.Lavender,
            KnownColor.LavenderBlush,
            KnownColor.LemonChiffon,
            KnownColor.LightBlue,
            KnownColor.LightCoral,
            KnownColor.LightCyan,
            KnownColor.LightGoldenrodYellow,
            KnownColor.LightGreen,
            KnownColor.LightPink,
            KnownColor.LightSalmon,
            KnownColor.LightSeaGreen,
            KnownColor.LightSkyBlue,
            KnownColor.LightSlateGray,
            KnownColor.LightSteelBlue,
            KnownColor.LightYellow,
            KnownColor.LimeGreen,
            KnownColor.Magenta,
            KnownColor.Maroon,
            KnownColor.MediumAquamarine,
            KnownColor.MediumBlue,
            KnownColor.MediumOrchid,
            KnownColor.MediumPurple,
            KnownColor.MediumSeaGreen,
            KnownColor.MediumSlateBlue,
            KnownColor.MediumSpringGreen,
            KnownColor.MediumTurquoise,
            KnownColor.MediumVioletRed,
            KnownColor.MidnightBlue,
            KnownColor.MistyRose,
            KnownColor.Moccasin,
            KnownColor.NavajoWhite,
            KnownColor.Navy,
            KnownColor.Olive,
            KnownColor.OliveDrab,
            KnownColor.Orange,
            KnownColor.OrangeRed,
            KnownColor.Orchid,
            KnownColor.PaleGoldenrod,
            KnownColor.PaleGreen,
            KnownColor.PaleTurquoise,
            KnownColor.PaleVioletRed,
            KnownColor.PapayaWhip,
            KnownColor.PeachPuff,
            KnownColor.Peru,
            KnownColor.Pink,
            KnownColor.Plum,
            KnownColor.PowderBlue,
            KnownColor.Purple,
            KnownColor.RebeccaPurple,
            KnownColor.Red,
            KnownColor.RosyBrown,
            KnownColor.RoyalBlue,
            KnownColor.SaddleBrown,
            KnownColor.Salmon,
            KnownColor.SandyBrown,
            KnownColor.SeaGreen,
            KnownColor.Sienna,
            KnownColor.Silver,
            KnownColor.SkyBlue,
            KnownColor.SlateBlue,
            KnownColor.SlateGray,
            KnownColor.SpringGreen,
            KnownColor.SteelBlue,
            KnownColor.Tan,
            KnownColor.Teal,
            KnownColor.Thistle,
            KnownColor.Tomato,
            KnownColor.Turquoise,
            KnownColor.Violet,
            KnownColor.Wheat,
            KnownColor.WhiteSmoke,
            KnownColor.Yellow,
            KnownColor.YellowGreen,
        };
    }
}