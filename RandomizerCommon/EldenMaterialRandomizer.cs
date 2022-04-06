using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SoulsFormats;
using static RandomizerCommon.LocationData;
using static RandomizerCommon.Util;

namespace RandomizerCommon
{
    public class EldenMaterialRandomizer
    {
        private GameData game;
        private LocationData data;

        public EldenMaterialRandomizer(GameData game, LocationData data)
        {
            this.game = game;
            this.data = data;
        }

        public void Randomize(Random random)
        {
            Dictionary<int, string> assetDrops = new Dictionary<int, string>();
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
                        }
                    }
                }
            }

            Dictionary<string, MSBX> maps = game.Maps.ToDictionary(e => e.Key, e => e.Value as MSBX);
            foreach (KeyValuePair<string, MSBX> entry in maps)
            {
                MSBX msb = entry.Value;
                List<MSBX.Part> assets = new List<MSBX.Part>();
                foreach (MSBX.Part part in msb.PartsX)
                {
                    // Should really be using part.ModelName, but easier said than done (SoulsFormats...)
                    if (!(part.Type == MSBX.PartType.Asset
                        && GameData.ExtractModelName(part.Name, out string modelName)
                        && modelName.StartsWith("AEG")))
                    {
                        continue;
                    }
                    int modelId = int.Parse(modelName.Substring(3).Replace("_", ""));
                    if (!assetDrops.TryGetValue(modelId, out string drop)) continue;
                    // Console.WriteLine($"{game.GetLocationSuffix(entry.Key, "")} {part.Name}: {drop}");
                    assets.Add(part);
                }
                if (assets.Count > 1)
                {
                    game.WriteMSBs.Add(entry.Key);
                    List<MSBX.Part> assign = assets.ToList();
                    Shuffle(random, assign);
                    List<int> assignModels = assign.Select(e => e.ModelIndex).ToList();
                    for (int i = 0; i < assets.Count; i++)
                    {
                        assets[i].ModelIndex = assignModels[i];
                    }
                }
            }
        }
    }
}
