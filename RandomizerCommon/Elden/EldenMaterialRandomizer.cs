using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using YamlDotNet.Core.Tokens;
using static RandomizerCommon.AnnotationData;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EldenMaterialRandomizer
    {
        private GameData game;
        private LocationData data;
        private AnnotationData ann;

        public EldenMaterialRandomizer(GameData game, LocationData data, AnnotationData ann)
        {
            this.game = game;
            this.data = data;
            this.ann = ann;
        }

        private class AssetPlacement
        {
            public AreaSiloType SiloType { get; set; }
            // All silo keys should have SiloType
            public Dictionary<AreaSilo, HashSet<string>> Maps { get; set; } = new();
        }

        public void Randomize(RandomizerOptions opt, Permutation perm)
        {
            Random random = new Random((int)opt.Seed);
            Dictionary<int, string> assetDrops = new();
            Dictionary<int, AssetPlacement> placements = new();
            // Dictionary<int, HashSet<string>> assetMaps = new();
            // There are 233 glovewort, 3366 rowa, 1128 erdleaf flower
            HashSet<int> fodderIds = new HashSet<int>();
            HashSet<int> glovewortIds = new HashSet<int>();
            if (data == null || ann == null || perm == null)
            {
                assetDrops = game.Params["AssetEnvironmentGeometryParam"].Rows
                    .Where(r => (int)r["pickUpItemLotParamId"].Value > 0)
                    .ToDictionary(r => r.ID, r => $"{r.ID} lot {r["pickUpItemLotParamId"].Value}");
                // For now, abandon this as a standalone thing. Maybe don't require permutation, though
                throw new Exception("Internal error: randomizing materials without item randomizer enabled");
            }

            Dictionary<ItemKey, HashSet<int>> itemAssets = new Dictionary<ItemKey, HashSet<int>>();
            foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Items)
            {
                // Somehow managed to sneak into data, ignore it to avoid weirdness
                if (entry.Key.ID == 17000) continue;
                foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                {
                    if (itemLoc.Scope.Type != ScopeType.Asset) continue;
                    foreach (Location loc in itemLoc.Keys)
                    {
                        // Console.WriteLine($"asset: {loc.Text}");
                        foreach (int asset in loc.Entities.Where(e => e.AssetID > 0).Select(e => e.AssetID).Distinct())
                        {
                            assetDrops[asset] = loc.Text;
                            AddMulti(itemAssets, entry.Key, asset);
                        }
                    }
                }
            }
            HashSet<string> minidungeons = new(ann.Areas.Where(e => e.Value.HasTag("minidungeon")).Select(e => e.Key));
            foreach ((ItemKey key, HashSet<int> assets) in itemAssets)
            {
                string itemName = game.Name(key);
                // Limit materials to any of the possible placement maps
                HashSet<string> restrictAreas = new();
                // These can safely be used in DLC-only silos
                HashSet<string> globalAreas = new();
                if (ann.ItemRestrict.TryGetValue(key, out PlacementRestrictionAnnotation restrict) && restrict.Unique != null)
                {
                    foreach (PlacementSlotAnnotation slot in restrict.Unique)
                    {
                        // TODO: Is permutation really necessary?
                        // For now, all area restrictions are assumed to be base-game only
                        // Note this includes events in the area, so it may not exist in ann.Areas
                        restrictAreas.UnionWith(slot.AllowedAreas(perm.IncludedAreas, perm.CombinedWeights));
                    }
                }
                if (itemName.Contains("Smithing Stone"))
                {
                    if (restrictAreas.Count > 0)
                    {
                        restrictAreas.IntersectWith(minidungeons);
                    }
                    globalAreas = minidungeons;
                    // Also, give stones the same range as Cracked Crystals.
                    // They use action button 7823 (2.75 height -2 offset), vs stones use 7800 (1.5 height -0.75 offset)
                    // Different text id, same text
                    foreach (int asset in assets)
                    {
                        PARAM.Row row = game.Params["AssetEnvironmentGeometryParam"][asset];
                        if (row == null) continue;
                        row["pickUpActionButtonParamId"].Value = 7823;
                    }
                }
                if (itemName.Contains("Glovewort"))
                {
                    glovewortIds.UnionWith(assets);
                }
                if (itemName == "Rowa Fruit" || itemName == "Erdleaf Flower" || itemName == "Rada Fruit")
                {
                    fodderIds.UnionWith(assets);
                }
                HashSet<string> getAreaMaps(HashSet<string> areas)
                {
                    HashSet<string> maps = new HashSet<string>();
                    foreach (string area in areas)
                    {
                        if (ann.Areas.TryGetValue(area, out AreaAnnotation areaAnn) && areaAnn.Maps != null)
                        {
                            maps.UnionWith(areaAnn.Maps.Split(' '));
                        }
                    }
                    return maps;
                }
                HashSet<string> restrictMaps = getAreaMaps(restrictAreas);
                HashSet<string> globalMaps = getAreaMaps(globalAreas);
                // Custom request for Silver Tear Husk, which is fairly rare (76 of them)
                if (itemName == "Silver Tear Husk")
                {
                    restrictMaps = new HashSet<string>(game.EldenMaps.Keys.Where(m => m.StartsWith("m12_")));
                }
                AssetPlacement place = new();
                if (restrictMaps.Count == 0 && globalAreas.Count == 0)
                {
                    place.SiloType = ann.OverallSilo;
                }
                else
                {
                    // Having restrictions automatically enforces a DLC silo. Global restrictions always apply in all silos, as well
                    if (globalAreas.Count > 0)
                    {
                        if (ann.OverallSilo == AreaSiloType.DLC || restrictMaps.Count > 0)
                        {
                            place.SiloType = AreaSiloType.DLC;
                            place.Maps = new()
                            {
                                [AreaSilo.ForDlc(false)] = restrictMaps.Count > 0 ? restrictMaps : globalMaps,
                                [AreaSilo.ForDlc(true)] = globalMaps,
                            };
                        }
                        else
                        {
                            place.SiloType = AreaSiloType.None;
                            place.Maps = new()
                            {
                                [AreaSilo.Default] = globalMaps,
                            };
                        }
                    }
                    else
                    {
                        place.SiloType = AreaSiloType.DLC;
                        place.Maps = new()
                        {
                            [AreaSilo.ForDlc(false)] = restrictMaps,
                        };
                    }
                }
                if (opt["matstats"])
                {
                    Console.WriteLine($"{game.Name(key)}: {place.SiloType} - {string.Join(", ", place.Maps.Keys)} | {string.Join(" | ", place.Maps.Select(m => string.Join(", ", m.Value)))}");
                }
                foreach (int asset in assets)
                {
                    placements[asset] = place;
                }
            }

            Dictionary<AreaSilo, List<AssetPart>> allLocations = new();
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                if (!opt["dlc"] && game.IsEldenDlcMap(entry.Key)) continue;
                MSBE msb = entry.Value;
                AreaSilo dlcSilo = AreaSilo.ForDlc(game.IsEldenDlcMap(entry.Key));
                foreach (MSBE.Part.Asset o in msb.Parts.Assets)
                {
                    if (!o.ModelName.StartsWith("AEG")) continue;
                    int modelId = int.Parse(o.ModelName.Substring(3).Replace("_", ""));
                    if (!placements.TryGetValue(modelId, out AssetPlacement place)) continue;
                    short animated = o.AssetSfxParamRelativeID;
                    AreaSilo silo = place.SiloType == AreaSiloType.DLC ? dlcSilo : AreaSilo.Default;
                    AssetPart part = new AssetPart
                    {
                        Map = entry.Key,
                        Name = o.Name,
                        ModelName = o.ModelName,
                        ModelID = modelId,
                        Animated = animated == 0,
                    };
                    AddMulti(allLocations, silo, part);
                }
            }
            // Mapping from target location to source item across all silos
            Dictionary<AssetPart, AssetPart> mapping = new Dictionary<AssetPart, AssetPart>();
            foreach ((AreaSilo silo, List<AssetPart> locations) in allLocations)
            {
                int modelCount = locations.Count;
                if (opt["matstats"])
                {
                    Console.WriteLine($"-- {silo}");
                    Dictionary<string, int> counts = new Dictionary<string, int>();
                    foreach (AssetPart part in locations)
                    {
                        string name = assetDrops[part.ModelID];
                        if (!counts.ContainsKey(name)) counts[name] = 0;
                        counts[name]++;
                    }
                    foreach ((string name, int count) in counts.OrderBy(e => e.Value))
                    {
                        Console.WriteLine($"{count}: {name}");
                    }
                }
                Shuffle(random, locations);
                // To balance spirit upgrades: glovewort is not limited to minidungeons, so triple it in the overworld.
                List<AssetPart> gloveworts = locations.Where(p => glovewortIds.Contains(p.ModelID)).ToList();
                List<AssetPart> models = locations.ToList();
                int glovewortIndex = 0;
                int glovewortMax = gloveworts.Count * 2;
                for (int i = 0; i < models.Count; i++)
                {
                    if (glovewortIndex == glovewortMax)
                    {
                        break;
                    }
                    if (fodderIds.Contains(models[i].ModelID))
                    {
                        AssetPart replace = gloveworts[glovewortIndex % gloveworts.Count];
                        glovewortIndex++;
                        models[i] = replace;
                    }
                }
                Shuffle(random, models);

                List<(AssetPart, HashSet<string>)> queue = new List<(AssetPart, HashSet<string>)>();
                Dictionary<int, HashSet<string>> assetMaps = new();
                foreach ((int id, AssetPlacement place) in placements)
                {
                    if (place.Maps.TryGetValue(silo, out HashSet<string> maps))
                    {
                        assetMaps[id] = maps;
                    }
                }
                // Currently 338 parts out of 14,846
                models.RemoveAll(m =>
                {
                    if (assetMaps.TryGetValue(m.ModelID, out HashSet<string> maps))
                    {
                        queue.Add((m, maps));
                        return true;
                    }
                    return false;
                });
                for (int i = 0; i < locations.Count; i++)
                {
                    AssetPart locPart = locations[i];
                    int queueIndex = queue.FindLastIndex(e => e.Item2.Contains(locPart.Map));
                    AssetPart itemPart;
                    if (queueIndex == -1 && models.Count > 0)
                    {
                        itemPart = models[models.Count - 1];
                        models.RemoveAt(models.Count - 1);
                    }
                    else
                    {
                        if (queueIndex == -1) queueIndex = queue.Count - 1;
                        itemPart = queue[queueIndex].Item1;
                        queue.RemoveAt(queueIndex);
                        // Console.WriteLine($"{assetDrops[itemPart.ModelID]} - {game.MapLocationName(locPart.Map)}");
                    }
                    mapping[locPart] = itemPart;
                }
            }

            if (opt["matstats"])
            {
                Console.WriteLine($"-- All");
                Dictionary<string, List<string>> placeMaps = new Dictionary<string, List<string>>();
                foreach ((AssetPart location, AssetPart model) in mapping)
                {
                    string modelName = assetDrops[model.ModelID];
                    string mapName = location.Map; // game.MapLocationName(location.Map);
                    AddMulti(placeMaps, modelName, mapName);
                }
                foreach ((string modelName, List<string> mapNames) in placeMaps.OrderBy(e => e.Key))
                {
                    Console.WriteLine($"{modelName}: {string.Join(", ", mapNames.GroupBy(m => m).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()}: {g.Key}"))}");
                }
            }
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                if (allLocations.Count == 0) break;
                if (!opt["dlc"] && game.IsEldenDlcMap(entry.Key)) continue;
                MSBE msb = entry.Value;
                HashSet<string> assetModels = new HashSet<string>(msb.Models.Assets.Select(m => m.Name));
                bool modified = false;
                foreach (MSBE.Part.Asset o in msb.Parts.Assets)
                {
                    if (!o.ModelName.StartsWith("AEG")) continue;
                    int modelId = int.Parse(o.ModelName.Substring(3).Replace("_", ""));
                    if (!assetDrops.TryGetValue(modelId, out string drop)) continue;

                    AssetPart keyPart = new AssetPart { Map = entry.Key, Name = o.Name };
                    if (!mapping.TryGetValue(keyPart, out AssetPart part))
                    {
#if DEBUG
                        throw new Exception($"No destination for material {entry.Key} {o.Name}");
#else
                        continue;
#endif
                    }
                    // if (glovewortIds.Contains(modelId)) Console.WriteLine($"Replacing {o.ModelName} in {entry.Key}: {part.ModelName} from {part.Map}");
                    string name = part.ModelName;
                    if (!assetModels.Contains(name))
                    {
                        msb.Models.Assets.Add(new MSBE.Model.Asset
                        {
                            Name = name,
                            SibPath = $@"N:\GR\data\Asset\Environment\geometry\{name.Substring(0, 6)}\{name}\sib\{name}.sib",
                        });
                        assetModels.Add(name);
                    }
                    o.ModelName = name;
                    // Ergh
                    if (part.Animated)
                    {
                        o.AssetSfxParamRelativeID = 0;
                    }
                    modified = true;
                }
                if (modified) game.WriteMSBs.Add(entry.Key);
            }
        }

        private class AssetPart
        {
            // Key parts
            public string Map { get; set; }
            public string Name { get; set; }
            // To transfer
            public string ModelName { get; set; }
            public int ModelID { get; set; }
            public bool Animated { get; set; }

            public override bool Equals(object obj) => obj is AssetPart o && Equals(o);
            public bool Equals(AssetPart o) => Map == o.Map && Name == o.Name;
            public override int GetHashCode() => Map.GetHashCode() ^ Name.GetHashCode();
        }
    }
}
