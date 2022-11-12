using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
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

        public void Randomize(Random random, Permutation perm)
        {
            Dictionary<int, string> assetDrops = new Dictionary<int, string>();
            Dictionary<int, HashSet<string>> assetMaps = new Dictionary<int, HashSet<string>>();
            if (data == null || ann == null || perm == null)
            {
                assetDrops = game.Params["AssetEnvironmentGeometryParam"].Rows
                    .Where(r => (int)r["pickUpItemLotParamId"].Value > 0)
                    .ToDictionary(r => r.ID, r => $"{r.ID} lot {r["pickUpItemLotParamId"].Value}");
            }
            else
            {
                Dictionary<ItemKey, HashSet<int>> itemAssets = new Dictionary<ItemKey, HashSet<int>>();
                foreach (KeyValuePair<ItemKey, ItemLocations> entry in data.Data)
                {
                    foreach (ItemLocation itemLoc in entry.Value.Locations.Values)
                    {
                        if (itemLoc.Scope.Type != ItemScope.ScopeType.ASSET) continue;
                        foreach (LocationKey loc in itemLoc.Keys)
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
                foreach (KeyValuePair<ItemKey, PlacementRestrictionAnnotation> entry in ann.ItemRestrict)
                {
                    if (entry.Value.Unique == null || !itemAssets.TryGetValue(entry.Key, out HashSet<int> assets)) continue;
                    // Limit materials to any of the possible placement maps
                    HashSet<string> restrictAreas = new HashSet<string>();
                    foreach (PlacementSlotAnnotation slot in entry.Value.Unique)
                    {
                        restrictAreas.UnionWith(slot.AllowedAreas(perm.IncludedAreas, perm.CombinedWeights));
                    }
                    string itemName = game.Name(entry.Key);
                    if (itemName.Contains("Smithing Stone"))
                    {
                        restrictAreas.RemoveWhere(a =>
                        {
                            // Events may be here as well
                            if (!ann.Areas.TryGetValue(a, out AreaAnnotation area)) return true;
                            return area.Tags == null || !area.Tags.Contains("minidungeon");
                        });
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
                    HashSet<string> restrictMaps = new HashSet<string>();
                    foreach (string area in restrictAreas)
                    {
                        if (ann.Areas.TryGetValue(area, out AreaAnnotation areaAnn) && areaAnn.Maps != null)
                        {
                            restrictMaps.UnionWith(areaAnn.Maps.Split(' '));
                        }
                    }
                    if (restrictMaps.Count == 0) continue;
                    foreach (int asset in assets)
                    {
                        assetMaps[asset] = restrictMaps;
                    }
                    // Console.WriteLine($"{game.Name(entry.Key)}: {string.Join(", ", restrictMaps)}");
                }
                // Custom request for Silver Tear Husk, which is fairly rare (76 of them)
                HashSet<string> underground = new HashSet<string>(game.Maps.Keys.Where(m => m.StartsWith("m12_")));
                if (underground.Count > 0)
                {
                    assetMaps[99825] = underground;
                }
            }

            List<AssetPart> locations = new List<AssetPart>();
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                MSBE msb = entry.Value;
                foreach (MSBE.Part.Asset o in msb.Parts.Assets)
                {
                    if (!o.ModelName.StartsWith("AEG")) continue;
                    int modelId = int.Parse(o.ModelName.Substring(3).Replace("_", ""));
                    if (!assetDrops.TryGetValue(modelId, out string drop)) continue;
                    // TODO er maybe should actually fill this in -.-
                    short animated = o.AssetSfxParamRelativeID;
                    locations.Add(new AssetPart
                    {
                        Map = entry.Key,
                        Name = o.Name,
                        ModelName = o.ModelName,
                        ModelID = modelId,
                        Animated = animated == 0,
                    });
                }
            }
            int modelCount = locations.Count;
            List<AssetPart> models = locations.ToList();
            Shuffle(random, locations);
            Shuffle(random, models);
            // Mapping from target location to source item
            Dictionary<AssetPart, AssetPart> mapping = new Dictionary<AssetPart, AssetPart>();
            if (assetMaps.Count == 0)
            {
                for (int i = 0; i < locations.Count; i++)
                {
                    mapping[locations[i]] = models[i];
                }
            }
            else
            {
                List<(AssetPart, HashSet<string>)> queue = new List<(AssetPart, HashSet<string>)>();
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
            foreach (KeyValuePair<string, MSBE> entry in game.EldenMaps)
            {
                if (locations.Count == 0) break;
                MSBE msb = entry.Value;
                HashSet<string> assetModels = new HashSet<string>(msb.Models.Assets.Select(m => m.Name));
                bool modified = false;
                foreach (MSBE.Part.Asset o in msb.Parts.Assets)
                {
                    if (locations.Count == 0) break;
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
            public string Map { get; set; }
            public string Name { get; set; }
            public string ModelName { get; set; }
            public int ModelID { get; set; }
            public bool Animated { get; set; }

            public override bool Equals(object obj) => obj is AssetPart o && Equals(o);
            public bool Equals(AssetPart o) => Map == o.Map && Name == o.Name;
            public override int GetHashCode() => Map.GetHashCode() ^ Name.GetHashCode();
        }
    }
}
