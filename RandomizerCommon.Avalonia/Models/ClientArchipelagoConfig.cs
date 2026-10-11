using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RandomizerCommon.Models
{
    public class ClientArchipelagoConfig
    {
        // Required fields (to output)
        [JsonPropertyName("url")]
        public string? Host { get; set; }
        [JsonPropertyName("slot")]
        public string? Player { get; set; }
        [JsonPropertyName("seed")]
        public string? Seed { get; set; }
        // Optional fields
        [JsonPropertyName("client_version")]
        public string? ClientVersion { get; set; }
        [JsonPropertyName("password")]
        public string? Password { get; set; }
        [JsonPropertyName("death_link_enabled")]
        public bool DeathLinkEnabled { get; set; }
        // Other to support read-modify-write
        [JsonExtensionData]
        public IDictionary<string, object>? UnknownFields { get; set; }

        public static ClientArchipelagoConfig Read(string path) => Read(File.OpenRead(path));
        public static ClientArchipelagoConfig Read(FileStream reader) => JsonSerializer.Deserialize<ClientArchipelagoConfig>(reader)!;

        public void Write(FileStream writer)
        {
            JsonSerializer.Serialize(writer, this, new JsonSerializerOptions()
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.Never,
                WriteIndented = false,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });
        }

        public void Update(string path, string? version = null)
        {
            using FileStream fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
            ClientArchipelagoConfig? config = null;
            // Keep existing config, best effort. Also possible race condition here but it's fine
            if (File.Exists(path))
            {
                try
                {
                    config = Read(fs);
                }
                catch { }
            }
            config ??= new();
            // Update only fields relevant to the run
            config.Host = Host;
            config.Player = Player;
            config.Seed = Seed;
            config.ClientVersion = version;
            config.Password = Password;
            config.DeathLinkEnabled = DeathLinkEnabled;
            fs.SetLength(0);
            config.Write(fs);
        }
    }
}
