using System;

namespace InkPulse.Worker.Infrastructure.Services.Caching.Models
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class CacheSectionAttribute : Attribute
    {
        public string Value { get; }

        public CacheSectionAttribute(string value)
        {
            Value = value;
        }
    }
}
