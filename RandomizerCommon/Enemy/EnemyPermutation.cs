using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.EnemyAnnotations;
using static RandomizerCommon.LocationData;

namespace RandomizerCommon
{
    public class EnemyPermutation
    {
        public bool AnyRandomized { get; set; }
        public SortedDictionary<EnemySiloType, EnemySilo> Silos = new();

        public IEnumerable<KeyValuePair<EnemySiloType, EnemySilo>> GetSilos(EnemyClass type)
        {
            return Silos.Where(e => e.Key.Type == type);
        }

        public IEnumerable<KeyValuePair<uint, uint>> GetSiloMappings(EnemyClass type)
        {
            return Silos.Where(e => e.Key.Type == type).SelectMany(e => e.Value.Mapping);
        }

        public EnemySilo GetOrCreateSilo(EnemySiloType siloType)
        {
            if (!Silos.TryGetValue(siloType, out EnemySilo silo))
            {
                Silos[siloType] = silo = new EnemySilo { SiloType = siloType };
            }
            return silo;
        }

        public class EnemySiloType : IComparable<EnemySiloType>
        {
            public EnemyClass Type { get; set; }
            public AreaSilo Area { get; set; } = AreaSilo.Default;

            public static EnemySiloType Of(EnemyClass type) => new EnemySiloType { Type = type };
            public EnemySiloType WithArea(AreaSilo area) => new EnemySiloType { Type = Type, Area = area };

            public override string ToString() => string.Join(" ", new[] { Type.ToString(), Area.Name }.Where(s => s != null));
            public int CompareTo(EnemySiloType o)
            {
                return (Type, Area.Type, Area.Index).CompareTo((o.Type, o.Area.Type, o.Area.Index));
            }
        }

        public class EnemySilo
        {
            public EnemySiloType SiloType { get; set; }
            public EnemyClass Type => SiloType.Type;
            // For now, entity IDs are unique and not collapsed (e.g. if two enemies are the same in different positions).
            public List<uint> Sources = new();
            public List<uint> Targets = new();
            // Targets which are copied from other ones. Must exist in tempBosses and target must exist in Targets.
            public List<uint> TempTargets = new();
            // Mapping from target to source
            public Dictionary<uint, uint> Mapping = new();
            // Mapping from (target owner, source helper) to other source helper
            public Dictionary<(uint, uint), uint> SwapMapping = new();
        }
    }
}
