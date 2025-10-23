using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace WordFilter
{
    #nullable enable

    public class WordFilters
    {
        [JsonProperty] public required string Word { get; set; }

        [JsonProperty] public required string Replacement { get; set; }

    }

    [JsonObject(MemberSerialization.OptIn)]
    public class WordFilterConfig
    {
        [JsonProperty]
        public readonly double Version = 1.0;

        [JsonProperty]
        public Dictionary<string, string> SymbolsToChar { get; set; } = new();

        [JsonProperty]
        public Dictionary<string, string> Acronyms { get; set; } = new();

        [JsonProperty]
        public HashSet<string> SafeWords { get; set; } = new();

        [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
        public List<WordFilters>? WordFilters { get; set; } = null;

        [JsonProperty] public bool ShouldLog { get; set; } = true;

        [JsonProperty] public HashSet<WordFilters> Filters { get; set; } = new();

        public static WordFilterConfig LoadDefault(ICoreServerAPI api)
        {
            try
            {
                var asset = api.Assets.Get("config/wordfilter/default.json");

                if (asset != null)
                {
                    return asset.ToObject<WordFilterConfig>();
                }
            }
            catch (Exception ex)
            {
                api.Logger.Error("Failed to load default wordfilter JSON: " + ex);
            }

            return new WordFilterConfig();
        }
    }
}
