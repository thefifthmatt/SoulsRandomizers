using RandomizerCommon;

namespace RandomizerCommon.Models
{
    public record ArchipelagoData(string RunID, RandomizerOptionsFile Options, ExternalItemPreset ExternalPreset, ClientArchipelagoConfig LoadConfig);
}
