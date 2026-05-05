using System.Collections.Concurrent;

namespace SmartJsonGenerator.Core.Configuration;

public record SmartJsonOptions
{
    public int MaxDepth { get; init; } = 5;
    public int DefaultCollectionSize { get; set; } = 3;
    public bool IgnoreNullValues { get; init; } = true;
    public bool ThrowOnCircularReference { get; init; } = true;
    public ConcurrentDictionary<Type, TypeRuleConfiguration> Rules { get; } = new();
}

