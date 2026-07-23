using InkPulse.Worker.Infrastructure.Constants;
using System;
using System.Collections.Generic;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Models
{
    public class CacheProperties
    {
        public static string SectionName => KeyConstant.ConfigSections.Cache;
        public string Redis { get; set; } = "";
        public Dictionary<string, SectionConfig> Sections { get; set; } = new();

        public class SectionConfig
        {
            public string Key { get; set; } = "";
            public int Ttl { get; set; }
        }

        public string BuildKey(string sectionKey, string id)
        {
            if (!Sections.TryGetValue(sectionKey, out var config))
            {
                throw new InvalidOperationException($"Cache section '{sectionKey}' is not configured");
            }
            return config.Key + id;
        }
    }
}
