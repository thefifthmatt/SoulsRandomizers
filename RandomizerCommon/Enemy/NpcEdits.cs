using SoulsFormats;
using SoulsIds;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon
{
    public class NpcEdits
    {
        private readonly GameData game;
        private readonly EnemyState state;
        // Based on game
        private readonly Dictionary<int, PARAM.Row> Npcs;
        private readonly string subSizeName;
        private readonly Dictionary<string, HashSet<int>> existingParamIds = new();

        // NPC -> team id. Default team affiliations. Most are removed and later reapplied (like in DS1 enemy rando)
        public readonly Dictionary<int, byte> NpcOriginalTeam = new();
        // NPC -> speffects, for resist and not. Make sure that swamp-immune locations remain swamp-immune
        public readonly Dictionary<int, List<int>> NpcCopySpEffect = new();
        public readonly List<int> ResistSps = new();
        // Dupe color speffects in Elden Ring
        public readonly List<int> DupeBossSpEffects = new List<int>();
        // NPC -> (item lot, event flag). Changed from NPC drops to scripted drops
        public readonly Dictionary<int, (int, int)> NpcItemLots = new();
        // NPC -> threatLv
        public readonly Dictionary<int, uint> NpcThreatLvs = new();
        // All threatLvs, mainly to save the effort of making it twice when bossbgm randomized
        public readonly List<uint> ThreatLvs = new();
        // Elden Ring: Think remapping for NPCs based on whether they're placed as invaders or world NPCs (default not randomized)
        public readonly Dictionary<int, int> NpcAsInvaderThink = new();
        public readonly Dictionary<int, int> NpcAsWorldThink = new();
        // DS1 NPC stuff
        public readonly Dictionary<int, int> ToGravelordNpc = new();
        public readonly Dictionary<int, int> FromGravelordNpc = new();
        public readonly HashSet<int> SkeletonNpcs = new();

        public NpcEdits(GameData game, EnemyState state)
        {
            this.game = game;
            this.state = state;
            Npcs = GameEditor.ParamToDictionary(game.Params["NpcParam"]);
            subSizeName = game.DS1 ? null : (game.Sekiro ? "sub" : "chr");
        }

        public PARAM.Row GetNpc(int id) => Npcs[id];
        public bool TryGetNpc(int id, out PARAM.Row row) => Npcs.TryGetValue(id, out row);

        public void ShrinkNpc(PARAM.Row row, float radius, float height)
        {
            if ((float)row["hitRadius"].Value > radius)
            {
                row["hitRadius"].Value = radius;
                if (subSizeName != null) row[$"{subSizeName}HitRadius"].Value = radius;
            }
            if ((float)row["hitHeight"].Value > height)
            {
                row["hitHeight"].Value = height;
                if (subSizeName != null) row[$"{subSizeName}HitHeight"].Value = height;
            }
        }

        // Fire Giant and Valiant Gargoyles, see comment below
        private static readonly HashSet<int> noBackHome = new HashSet<int> { 47600900, 47700200, 47701200 };
        public void ShrinkRange(PARAM.Row row, int range, int? backRange = null)
        {
            if (row == null || !state.Permutation.AnyRandomized) return;
            if ((ushort)row["eye_dist"].Value > range)
            {
                row["eye_dist"].Value = (ushort)range;
            }
            if ((ushort)row["nose_dist"].Value > range)
            {
                row["nose_dist"].Value = (ushort)range;
            }
            // This causes issues in non-random dupe fights, though just disable in dupe mode entirely for now
            // This is mainly for bosses tracking you super long distances in the overworld, but disrupts vanilla fights.
            // Temp hack: hardcode specific rows and let them be nightmares for the time being.
            range = backRange ?? range;
            if ((ushort)row["maxBackhomeDist"].Value > range && !noBackHome.Contains(row.ID))
            {
                row["maxBackhomeDist"].Value = (ushort)range;
            }
        }

        // Duplicate npc params
        // For bosses, make a copy of them with XP drops (Experience) and money drops (getSoul)
        // For minibosses, prepare to make a copy of them if regular enemies go there, with modified Hp, stamina/staminaRecoverBaseVal?, and HealthbarNum
        private PARAM.Row DuplicateAdjacentRow(string paramName, int baseId)
        {
            if (!existingParamIds.TryGetValue(paramName, out HashSet<int> existingIds))
            {
                existingParamIds[paramName] = existingIds = new HashSet<int>(game.Params[paramName].Rows.Select(e => e.ID));
            }
            PARAM.Row baseRow = game.Params[paramName][baseId];
            if (baseRow == null) throw new Exception($"{paramName} {baseId} is referenced but does not exist");
            int copyId = baseId;
            while (existingIds.Contains(copyId)) copyId++;
            existingIds.Add(copyId);
            PARAM.Row copyRow = game.AddRow(paramName, copyId);
            GameEditor.CopyRow(baseRow, copyRow);
            return copyRow;
        }

        public PARAM.Row DuplicateAdjacentThink(int baseId) => DuplicateAdjacentRow("NpcThinkParam", baseId);

        // Special version for NPCs. Should be used consistently for all adds
        public PARAM.Row DuplicateAdjacentNpc(int baseId)
        {
            if (!Npcs.TryGetValue(baseId, out PARAM.Row baseRow))
            {
                if (baseRow == null) throw new Exception($"NpcParam {baseId} is referenced but does not exist");
            }
            int copyId = baseId;
            while (Npcs.ContainsKey(copyId)) copyId++;
            PARAM.Row copyRow = game.AddRow("NpcParam", copyId);
            GameEditor.CopyRow(baseRow, copyRow);
            Npcs[copyId] = copyRow;
            return copyRow;
        }
    }
}
