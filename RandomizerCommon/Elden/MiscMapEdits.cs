using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon
{
    public class MiscMapEdits
    {
        private readonly GameData game;
        // This may cause collisions if multiple instances of this are created,
        // but can be avoided by adding all new instances of a particular model in one place.
        private int partIndex = 4000;

        public MiscMapEdits(GameData game)
        {
            this.game = game;
        }

        public string GetEventMap(string eventMap, string eventPart)
        {
            // Parse out low-level map using Elden Ring convention.
            // Can also select neighbor maps here if the emevd does not exist in those cases, but it should for fog gates.
            if (eventMap.StartsWith("m6") && !eventMap.EndsWith("0"))
            {
                if (!eventPart.StartsWith("m") || !eventPart.Contains("-"))
                {
                    throw new Exception($"Entity {eventPart} in {eventMap} is missing a prefix specifying a low-level map");
                }
                eventMap = eventPart.Split('-')[0];
            }
            if (game.NeighborMaps.TryGetValue(eventMap, out string neighborMap))
            {
                eventMap = neighborMap;
            }
            return eventMap;
        }

        private string NewPartName(string model, string other)
        {
            string name = $"{model}_{partIndex++:d4}";
            return other.StartsWith("m") && other.Contains("-") ? $"{other.Split('-')[0]}-{name}" : name;
        }

        // A bit expensive compared to MapEditors, but okay for one-offs
        private void AddAssetModel(MSBE msb, string name)
        {
            if (!msb.Models.Assets.Any(m => m.Name == name))
            {
                msb.Models.Assets.Add(new MSBE.Model.Asset
                {
                    Name = name,
                    SibPath = $@"N:\GR\data\Asset\Environment\geometry\{name.Substring(0, 6)}\{name}\sib\{name}.sib",
                });
            }
        }

        private void SetNameIdent(MSBE.Part part)
        {
            if (int.TryParse(part.Name.Split('_').Last(), out int partIndex))
            {
                part.Unk08 = partIndex;
            }
        }

        private bool SetAssetName(MSBE.Part.Asset fog, string newName)
        {
            string oldName = fog.Name;
            fog.Name = newName;
            SetNameIdent(fog);
            // Some fogs can have self-references. No idea what these do
            bool success = true;
            for (int j = 0; j < fog.UnkPartNames.Length; j++)
            {
                string refName = fog.UnkPartNames[j];
                if (refName != null)
                {
                    if (refName == oldName)
                    {
                        fog.UnkPartNames[j] = fog.Name;
                    }
                    else
                    {
                        success = false;
                    }
                }
            }
            if (fog.UnkT54PartName != null)
            {
                if (fog.UnkT54PartName == oldName)
                {
                    fog.UnkT54PartName = fog.Name;
                }
                else
                {
                    success = false;
                }
            }
            return success;
        }

        public MSBE.Part.Asset AddDerivativeAsset(string map, string model, string nearName, Vector3 pos, Vector3 rot, string newName = null)
        {
            newName ??= NewPartName(model, nearName);
            MSBE msb = game.EldenMaps[map];
            MSBE.Part.Asset a = msb.Parts.Assets.Find(o => o.Name == nearName);
            if (a == null) throw new Exception($"Missing asset {nearName} in {map}, needed to create {newName}");
            a = (MSBE.Part.Asset)a.DeepCopy();
            SetAssetName(a, newName);
            a.Position = pos;
            a.Rotation = rot;
            a.ModelName = model;
            a.EntityID = 0;
            msb.Parts.Add(a);
            AddAssetModel(msb, model);
            game.WriteMSBs.Add(map);
            return a;
        }

        private MSBE.Part.Asset baseAsset;
        public MSBE.Part.Asset AddOverworldAsset(string map, string model, Vector3 pos, Vector3 rot)
        {
            if (baseAsset == null)
            {
                // This one is also used by fog rando. RIP if someone deletes it
                baseAsset = game.EldenMaps["m60_46_38_00"].Parts.Assets.Find(e => e.Name == "AEG007_310_2000");
            }
            // Overworld assets are pretty simple, can be copied between maps. Start with an (assumed) unmodified one.
            // UnkT54 seems unique per asset but no seeming effect. All refs should be self refs.
            MSBE.Part.Asset target = (MSBE.Part.Asset)baseAsset.DeepCopy();
            target.ModelName = model;
            AddAssetModel(game.EldenMaps[map], target.ModelName);
            SetAssetName(target, NewPartName(target.ModelName, target.Name));
            target.Position = pos;
            target.Rotation = rot;
            game.EldenMaps[map].Parts.Assets.Add(target);
            game.WriteMSBs.Add(map);
            return target;
        }

        public void AddOverworldMountJump(string map, Vector3 pos, float jumpHeight, float fallRadius, float fallHeight, Vector3? jumpRot = null)
        {
            // This doesn't seem to be needed - was only present in one case
            // MSBE.Part.Asset asset = AddOverworldAsset(map, "AEG099_090", pos, rot);
            // asset.AssetSfxParamRelativeID = -1;
            MSBE msb = game.EldenMaps[map];
            msb.Regions.MountJumps.Add(new MSBE.Region.MountJump
            {
                Shape = new MSB.Shape.Sphere(5),
                UnkE08 = 255, MapID = -1, UnkS0C = -1,
                Position = pos,
                Rotation = jumpRot ?? new Vector3(),
                JumpHeight = jumpHeight,
                UnkT04 = -1,
            });
            msb.Regions.MountJumpFalls.Add(new MSBE.Region.MountJumpFall
            {
                Shape = new MSB.Shape.Cylinder(fallRadius, fallHeight + 3),
                UnkE08 = 255, MapID = -1, UnkS0C = -1,
                Position = pos - new Vector3(0, 3, 0),
                Rotation = new Vector3(),
            });
            game.WriteMSBs.Add(map);
        }

        public void AddOverworldLockedMountJump(string map, Vector3 pos, uint flag, float jumpHeight, float fallRadius, float fallHeight, Vector3? jumpRot = null)
        {
            MSBE msb = game.EldenMaps[map];
            msb.Regions.LockedMountJumps.Add(new MSBE.Region.LockedMountJump
            {
                Shape = new MSB.Shape.Sphere(5),
                UnkE08 = 255,
                MapID = -1,
                UnkS0C = -1,
                Position = pos,
                Rotation = jumpRot ?? new Vector3(),
                JumpHeight = jumpHeight,
                UnkT04 = -1,
                UnkT08 = (int)flag,
            });
            msb.Regions.LockedMountJumpFalls.Add(new MSBE.Region.LockedMountJumpFall
            {
                Shape = new MSB.Shape.Cylinder(fallRadius, fallHeight + 3),
                UnkE08 = 255,
                MapID = -1,
                UnkS0C = -1,
                Position = pos - new Vector3(0, 3, 0),
                Rotation = new Vector3(),
                UnkT08 = (int)flag,
            });
            game.WriteMSBs.Add(map);
        }

        public void AddOverworldSendingGate(
            string map, Vector3 pos, Vector3 rot, int entityId, int baseFlag,
            string destMap, int dest)
        {
            // baseFlag uses 3 flags in total
            MSBE.Part.Asset asset = AddOverworldAsset(map, "AEG099_510", pos, rot);
            asset.EntityID = (uint)entityId;
            List<byte> destId = GameData.ParseMap(destMap);
            // Event 90005605:
            // X0_4 = asset, X4_4 = warp bytes, X8_4 = warp target, X12_4 = warp areaname, X16_4 = online unique flag,
            // X20_4 X24_4 = dialog flags, X28_4 = requirement flag, X32_4 = deny message, X36_4 X40_4 = unused
            AddInit(map, new EMEVD.Instruction(2000, 6, new List<object>
            {
                0, 90005605,
                (uint)entityId, destId[0], destId[1], destId[2], destId[3], dest, 0, baseFlag + 2,
                baseFlag, baseFlag + 1, 0, 0, 0, 0,
            }));
        }

        private void AddInit(string map, EMEVD.Instruction instr)
        {
            // Event 0 does have to exist in source map, as it's not created here.
            EMEVD.Event constr = game.Emevds[map].Events.Find(ev => ev.ID == 0);
            if (constr == null) throw new Exception($"Event script {map} missing event 0");
            constr.Instructions.Add(instr);
            game.WriteEmevds.Add(map);
        }

        private static Vector3 MoveInDirection(Vector3 v, Vector3 r, float dist)
        {
            float angle = r.Y * (float)Math.PI / 180;
            return new Vector3(v.X + (float)Math.Sin(angle) * dist, v.Y, v.Z + (float)Math.Cos(angle) * dist);
        }

        public void AddGrace(
            string map, Vector3 pos, Vector3 rot,
            int baseId, string assetName, string playerName, string enemyName,
            int bonfireFlagId, int placeFmgId, int sortId,
            int overrideAssetId, int overrideRowId)
        {
            HashSet<uint> flags = new HashSet<uint>(game.Params["BonfireWarpParam"].Rows.Select(r => (uint)r["eventflagId"].Value));
            HashSet<uint> bonfireEntities = new HashSet<uint>(game.Params["BonfireWarpParam"].Rows.Select(r => (uint)r["bonfireEntityId"].Value));
            HashSet<int> rowIds = new HashSet<int>(game.Params["BonfireWarpParam"].Rows.Select(r => r.ID));

            PARAM.Row baseRow = game.Params["BonfireWarpParam"].Rows.Find(r => (uint)r["bonfireEntityId"].Value == baseId);
            // For simplicity, just copy all of it. Only asset may be in a different map (_02 tiles in overworld)
            int baseChrId = baseId - 1000;
            int basePlayerId = baseId - 970;
            MSBE msb = game.EldenMaps[map];
            MSBE.Part.Enemy baseChr = msb.Parts.Enemies.Find(e => enemyName == null ? e.EntityID == baseChrId : e.Name == enemyName);
            // Probably any player is fine
            MSBE.Part.Player basePlayer = msb.Parts.Players.Find(e => playerName == null ? e.EntityID == basePlayerId : e.Name == playerName);
            MSBE.Part.Asset baseAsset = msb.Parts.Assets.Find(e => e.Name == assetName);
            if (baseRow == null || baseChr == null || basePlayer == null || baseAsset == null)
            {
                throw new Exception($"Missing {map} row {baseId} ({baseRow}), enemy {baseChrId} {enemyName} ({baseChr}), player {basePlayerId} {playerName} ({basePlayer}), or asset {assetName} ({baseAsset})");
            }
            void addBonfireEntities(IEnumerable<MSBE.Part> parts, uint offset)
            {
                foreach (MSBE.Part part in parts)
                {
                    if (part.EntityID > 0 && part.EntityID % 10000 < 1000)
                    {
                        bonfireEntities.Add(part.EntityID + offset);
                    }
                }
            }

            // if (b.HasTag("chapel")) assetId = 10011952;
            // if (b.HasTag("chapel")) newRowId = 100102;

            addBonfireEntities(msb.Parts.Enemies, 1000);
            addBonfireEntities(msb.Parts.Players, 970);
            uint baseFlag = (uint)baseRow["eventflagId"].Value;
            // Graces have a few parts:
            // AEG099_060 asset (14001951), c0000 warp player (14000981), c1000 enemy (14000951), param row (140001), event flag (71401)
            // Make map changes
            Vector3 playerPos = MoveInDirection(pos, rot, 2);
            // Need to use a close-ish range
            uint assetId = overrideAssetId > 0 ? (uint)overrideAssetId : (uint)baseId;
            while (bonfireEntities.Contains(assetId)) assetId++;
            bonfireEntities.Add(assetId);
            uint chrId = assetId - 1000;
            uint playerId = assetId - 970;
            // Asset
            MSBE.Part.Asset newAsset = AddDerivativeAsset(map, "AEG099_060", assetName, pos, rot);
            newAsset.EntityID = assetId;
            // Enemy
            MSBE.Part.Enemy newChr = (MSBE.Part.Enemy)baseChr.DeepCopy();
            newChr.EntityID = chrId;
            if (newChr.ModelName != "c1000")
            {
                newChr.ModelName = "c1000";
                newChr.ThinkParamID = 1;
                newChr.NPCParamID = 10000000;
                newChr.TalkID = 1000;
                newChr.CharaInitID = -1;
                // if (b.HasTag("chapel")) newChr.CollisionPartName = "h002000";
            }
            newChr.Name = NewPartName("c1000", baseChr.Name);
            SetNameIdent(newChr);
            newChr.Position = pos;
            newChr.Rotation = rot;
            msb.Parts.Enemies.Add(newChr);
            // Player
            MSBE.Part.Player newPlayer = (MSBE.Part.Player)basePlayer.DeepCopy();
            newPlayer.EntityID = playerId;
            newPlayer.Name = NewPartName("c0000", basePlayer.Name);
            SetNameIdent(newPlayer);
            newPlayer.Position = playerPos;
            newPlayer.Rotation = rot;
            msb.Parts.Players.Add(newPlayer);
            game.WriteMSBs.Add(map);
            // Map param changes
            int newRowId = overrideRowId > 0 ? overrideRowId : baseRow.ID;
            uint newFlagId = baseFlag;
            while (rowIds.Contains(newRowId)) newRowId++;
            while (flags.Contains(newFlagId)) newFlagId++;
            rowIds.Add(newRowId);
            flags.Add(newFlagId);
            // New row from scratch in case there are NPCs etc there
            // TODO: other languages
            // int newTextId = placeFmgBase++;
            // itemFMGs["PlaceName"][newTextId] = b.Text;
            PARAM.Row row = GameEditor.AddRow(game.Params["BonfireWarpParam"], newRowId);
            row["eventflagId"].Value = newFlagId;
            row["bonfireEntityId"].Value = assetId;
            // TODO: Look into auto-sort-id, but they can conflict between multiple new graces in the same subcategory
            row["bonfireSubCategorySortId"].Value = (ushort)sortId;
            List<byte> mapParts = GameData.ParseMap(map);
            row["areaNo"].Value = mapParts[0];
            row["gridXNo"].Value = mapParts[1];
            row["gridZNo"].Value = mapParts[2];
            row["posX"].Value = pos.X;
            row["posY"].Value = pos.Y;
            row["posZ"].Value = pos.Z;
            row["textId1"].Value = placeFmgId;
            List<string> copyFields = new List<string>
            {
                "forbiddenIconId", "bonfireSubCategoryId", "iconId", "dispMask00", "dispMask01",
                "noIgnitionSfxDmypolyId_0", "noIgnitionSfxId_0", // "unkE8",
            };
            foreach (string field in copyFields)
            {
                row[field].Value = baseRow[field].Value;
            }
            // Map event changes. TODO: Use Events with proper emedf
            EMEVD.Instruction reg = new EMEVD.Instruction(2009, 3, new List<object> { bonfireFlagId, assetId, 0f, 0f, 0, 5f });
            AddInit(map, reg);

            game.Params["BonfireWarpParam"].Rows = game.Params["BonfireWarpParam"].Rows.OrderBy(r => r.ID).ToList();
        }
    }
}
