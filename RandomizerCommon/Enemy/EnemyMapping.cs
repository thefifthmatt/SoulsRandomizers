using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static RandomizerCommon.EnemyAnnotations;

namespace RandomizerCommon
{
    public class EnemyMapping
    {
        private readonly Enemies enemies;
        public EnemyMapping(Enemies enemies)
        {
            this.enemies = enemies;
        }

        // Mapping of enemies which are randomized, from source to target
        public readonly Dictionary<uint, List<uint>> Mapping = new();
        // Another one from target to source
        public readonly Dictionary<uint, uint> RevMapping = new();
        // Mapping of new helpers, (owner target, source helper) -> target helper
        // More of a reverse mapping
        public readonly Dictionary<(uint, uint), uint> HelperMapping = new();
        // To support swapboss mode, mapping from (owner target, source helper) -> other source helper
        // This is mainly for scaling.
        public readonly Dictionary<(uint, uint), uint> SwapMapping = new();

        public bool GetTargets(uint source, out List<uint> targets) => Mapping.TryGetValue(source, out targets);
        public bool GetSource(uint target, out uint source) => RevMapping.TryGetValue(target, out source);

        // Utilites to avoid using mapping/revMapping directly
        public bool IsRandomizedTarget(uint target)
        {
            // If not present in revMapping, source == target
            // Otherwise, if source comes from target, this counts as not being randomized as well - this means that some templates
            // won't run, but rewriting should still happen as part of DupeIndex.
            return RevMapping.TryGetValue(target, out uint source)
                && !(enemies.AnyDupeEnabled && enemies.TryInfo(target, out EnemyInfo targetInfo) && targetInfo.DupeFrom == source);
        }

        public bool IsRandomizedSource(uint source)
        {
            // Likewise, if source ever moves outside of its own spot
            return Mapping.TryGetValue(source, out List<uint> targets)
                && !(enemies.AnyDupeEnabled && targets.All(target => enemies.TryInfo(target, out EnemyInfo targetInfo) && targetInfo.DupeFrom == source));
        }

        // Can pare this interface down, but currently returns true if randomized for a real (non-dupe) target.
        // and outputs the source (present in infos map) and real target (may not be - also can be ignored if using replace/rewrite logic)
        public bool GetRandomizedSource(uint baseTarget, int dupeIndex, out uint source, out uint realTarget)
        {
            source = baseTarget;
            realTarget = baseTarget;
            if (dupeIndex >= 0)
            {
                if (!enemies.DupeEnemyMap.TryGetValue(baseTarget, out List<uint> dupes)) return false;
                // This used to be an error, and kind of is, but it's on the caller to check dupeCount themselves
                // It matches how we use the main entity for params if not all can be filled in.
                if (dupeIndex >= dupes.Count) return false;
                realTarget = dupes[dupeIndex];
            }
            if (!RevMapping.TryGetValue(realTarget, out uint realSource)) return false;
            source = realSource;
            return true;
        }

        // Undoes dupe stuff, for non-helpers only!
        public uint GetBaseTarget(uint realTarget, out int dupeIndex)
        {
            dupeIndex = -1;
            // We're using DupeFrom on a target here, meaning the source does not matter
            if (!enemies.TryInfo(realTarget, out EnemyInfo info) || info.DupeFrom <= 0) return realTarget;
            if (!enemies.DupeEnemyMap.TryGetValue(info.DupeFrom, out List<uint> dupes)) throw new Exception($"Internal error: {info.DupeFrom} ({realTarget} base) has no dupes");
            dupeIndex = dupes.IndexOf(realTarget);
            // TODO: When would this ever happen?
#if DEBUG
            if (dupeIndex == -1) throw new Exception($"Internal error: {realTarget}'s DupeFrom {info.DupeFrom} missing it: [{string.Join(",", dupes)}]");
#endif
            return info.DupeFrom;
        }

        public List<uint> GetHelperTargets(uint target)
        {
            List<uint> helperTargets = new();
            if (RevMapping.TryGetValue(target, out uint source))
            {
                if (enemies.Owners.TryGetValue(source, out List<uint> helpers))
                {
                    foreach (uint helper in helpers)
                    {
                        if (HelperMapping.TryGetValue((target, helper), out uint helperTarget))
                        {
                            helperTargets.Add(helperTarget);
                        }
                    }
                }
            }
            else
            {
                // Not randomized
                if (enemies.Owners.TryGetValue(target, out List<uint> helpers))
                {
                    helperTargets.AddRange(helpers);
                }
            }
            return helperTargets;
        }

    }
}
