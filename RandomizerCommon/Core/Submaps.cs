using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.IO;
using YamlDotNet.Serialization;
using Newtonsoft.Json;

namespace RandomizerCommon
{
    public class Submaps
    {
        public List<MultiMap> MapAreas { get; set; }

        private Dictionary<string, MultiMap> maps = new();

        public static Submaps Read(string file)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            using (var reader = File.OpenText(file))
            {
                Submaps ret = deserializer.Deserialize<Submaps>(reader);
                if (ret.MapAreas == null) throw new Exception($"Invalid config {file}");
                MultiMap validate(MultiMap map)
                {
                    if (map.Areas == null) throw new Exception($"Invalid config {file} - {map.Map} missing areas");
#if DEBUG
                    for (int i = 0; i < map.Areas.Count; i++)
                    {
                        bool last = i == map.Areas.Count - 1;
                        if (last != map.Areas[i].Empty()) throw new Exception($"Invalid config {file} - {map.Map} area {i} empty should be {last}");
                    }
#endif
                    return map;
                }
                // Where(e => e.Map != null)
                ret.maps = ret.MapAreas.ToDictionary(e => e.Map, e => validate(e));
                return ret;
            }
        }

        public void Write(string path, IDictionary<string, string> mapNames)
        {
            List<MultiMap> list = new();
            foreach ((string map, string name) in mapNames)
            {
                if (string.IsNullOrWhiteSpace(name)) continue;
                if (!maps.TryGetValue(map, out MultiMap multi))
                {
                    multi = new MultiMap { Map = map };
                }
                multi.Name = name;
                list.Add(multi);
            }
            Wrapper obj = new Wrapper { Maps = list.OrderBy(x => x.Map).ToList() };
            string output = JsonConvert
                .SerializeObject(obj, Formatting.Indented, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Ignore })
                .Replace("\r\n", "\n");
            File.WriteAllText(path, output);
        }

        public bool TryGetValue(string mapId, out MultiMap map) => maps.TryGetValue(mapId, out map);

        private class Wrapper
        {
            [JsonProperty(PropertyName = "maps", Order = 1)]
            public List<MultiMap> Maps { get; set; }
        }

        public class MultiMap
        {
            [JsonProperty(PropertyName = "map", Order = 1)]
            public string Map { get; set; }
            [JsonProperty(PropertyName = "name", Order = 2)]
            public string Name { get; set; }
            [JsonProperty(PropertyName = "comment", Order = 3)]
            public string Comment { get; set; }
            // TODO: Validate last one has no reqs
            [JsonProperty(PropertyName = "areas", Order = 4)]
            public List<MultiMapBound> Areas { get; set; }

            public bool Resolve(Vector3 coord, out MultiMapBound bound)
            {
                bound = Areas.Find(a => a.Match(coord));
                return bound != null;
            }
        }

        public class MultiMapBound
        {
            // Extra name for subdivision, with multiple elevations or subareas
            [JsonProperty(PropertyName = "name", Order = 1)]
            public string Name { get; set; }
            // Name from area config. Ignored for JSON output though
            [JsonIgnore]
            public string Area { get; set; }
            [JsonProperty(PropertyName = "comment", Order = 10)]
            public string Comment { get; set; }
            // One limitation, can't be 0 in this format
            [JsonProperty(PropertyName = "x_below", Order = 20)]
            public int XBelow { get; set; }
            [JsonProperty(PropertyName = "x_above", Order = 21)]
            public int XAbove { get; set; }
            [JsonProperty(PropertyName = "y_below", Order = 22)]
            public int YBelow { get; set; }
            [JsonProperty(PropertyName = "y_above", Order = 23)]
            public int YAbove { get; set; }
            [JsonProperty(PropertyName = "z_below", Order = 24)]
            public int ZBelow { get; set; }
            [JsonProperty(PropertyName = "z_above", Order = 25)]
            public int ZAbove { get; set; }

            public bool Empty()
            {
                return XBelow == 0 && XAbove == 0 && YBelow == 0 && YAbove == 0 && ZBelow == 0 && ZAbove == 0;
            }

            public bool Match(Vector3 pos)
            {
                bool match = true;
                if (XBelow != 0) match &= pos.X < XBelow;
                if (XAbove != 0) match &= pos.X > XAbove;
                if (YBelow != 0) match &= pos.Y < YBelow;
                if (YAbove != 0) match &= pos.Y > YAbove;
                if (ZBelow != 0) match &= pos.Z < ZBelow;
                if (ZAbove != 0) match &= pos.Z > ZAbove;
                return match;
            }
        }
    }
}
