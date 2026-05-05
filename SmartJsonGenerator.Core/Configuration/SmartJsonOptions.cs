using System.Collections.Concurrent;

namespace SmartJsonGenerator.Core.Configuration;

/// <summary>
/// Immutable configuration record for <c>SmartGenerator</c>.
/// Use <c>with</c> expressions to create customized instances.
/// </summary>
public record SmartJsonOptions
{
    /// <summary>Maximum recursive depth before generation stops and returns <see langword="null"/>. Default: 5.</summary>
    public int MaxDepth { get; init; } = 5;

    /// <summary>Number of elements generated for arrays, lists, and dictionaries. Default: 3.</summary>
    public int DefaultCollectionSize { get; set; } = 3;

    /// <summary>When <see langword="true"/>, null property values are omitted from the output. Default: <see langword="true"/>.</summary>
    public bool IgnoreNullValues { get; init; } = true;

    /// <summary>
    /// When <see langword="true"/>, a <c>CircularReferenceException</c> is thrown if a cycle is detected.
    /// When <see langword="false"/>, the cycle is silently broken by returning <see langword="null"/>. Default: <see langword="true"/>.
    /// </summary>
    public bool ThrowOnCircularReference { get; init; } = true;

    /// <summary>Per-type property override rules registered via the fluent API.</summary>
    public ConcurrentDictionary<Type, TypeRuleConfiguration> Rules { get; } = new();
}

