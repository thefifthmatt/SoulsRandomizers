using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RandomizerCommon
{
    // Compartmentalized state is built up in the following order. This is like a service container but with no dependency inversion.
    // Once a field is set here, it shouldn't be mutated. Each of these could have builders but that entails a lot of boilerplate.
    public class EnemyState
    {
        // Metadata and basic map data for all enemies, post-multiplier. Should be used instead of EnemyAnnotations once created
        private Enemies _enemies;
        public Enemies Enemies
        {
            get => _enemies ?? throw new Exception("Internal error: accessed enemy metadata before initialized");
            set => _enemies = value;
        }

        // The processed preset, which could also be injected. This is only used for misc options currently and is allowed to be null.
        private EnemyPreset _preset;
        public EnemyPreset Preset
        {
            get => _preset;
            set => _preset = value;
        }

        // Per-silo randomization results
        private EnemyPermutation _permutation;
        public EnemyPermutation Permutation
        {
            get => _permutation ?? throw new Exception("Internal error: accessed permutation before initialized");
            set => _permutation = value;
        }

        // Initial NPC edits
        private NpcEdits _npcEdits;
        public NpcEdits NpcEdits
        {
            get => _npcEdits ?? throw new Exception("Internal error: accessed npc edits before initialization");
            set => _npcEdits = value;
        }

        // Global randomization results after map pass
        public EnemyMapping _mapping;
        public EnemyMapping Mapping
        {
            get => _mapping ?? throw new Exception("Internal error: accessed enemy mapping before initialization");
            set => _mapping = value;
        }
    }
}
