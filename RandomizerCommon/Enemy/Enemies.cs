using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.EnemyAnnotations;

namespace RandomizerCommon
{
    public class Enemies
    {
        private readonly GameData game;

        public Enemies(GameData game)
        {
            this.game = game;
        }

        // All of the below can be made readonly after Enemies becomes the access point for all of it.
        // As of right now this is a shared reference with variables in EnemyRandomizer.
        public Dictionary<uint, EnemyInfo> Infos = new();
        public Dictionary<uint, List<uint>> Owners = new();
        public Dictionary<uint, EnemyData> DefaultData = new();
        // Eventually most dupe state should live here
        public bool AnyDupeEnabled { get; set; }
        public Dictionary<uint, List<uint>> DupeEnemyMap = new();

        // Map from ESD id to defeat flag (better to calculate in actual pass?)
        public Dictionary<int, int> DeathLineFlags = new();
        // For later convenience, enemies to edit talk lines for
        public List<uint> TalkLineEdits = new();


        public EnemyInfo this[uint id]
        {
            get => Infos[id];
        }

        public EnemyInfo Info(uint id) => Infos[id];
        public EnemyData Data(uint id) => DefaultData[id];
        public bool TryInfo(uint id, out EnemyInfo info) => Infos.TryGetValue(id, out info);
        public bool TryData(uint id, out EnemyData data) => DefaultData.TryGetValue(id, out data);

        // Quick internal name for entity id
        public string QuickName(uint ent)
        {
            if (DefaultData.TryGetValue(ent, out EnemyData data))
            {
                return $"{data.Name} #{ent} ({game.ModelCharacterName(data.Model, data.Char)})";
            }
            return $"#{ent}";
        }

        // Slightly less quick name for helping people debug mod merging issues
        public string Name(uint ent)
        {
            string modelName = null;
            if (DefaultData.TryGetValue(ent, out EnemyData data))
            {
                modelName = game.ModelCharacterName(data.Model, data.Char);
            }
            string className = "";
            if (Infos.TryGetValue(ent, out EnemyInfo info))
            {
                if (info.ExtraName != null && info.ExtraName != modelName)
                {
                    modelName = info.ExtraName;
                }
                className = $"{info.Class} ";
            }
            return $"{className}#{ent} ({modelName ?? "unknown"})";
        }
    }
}
