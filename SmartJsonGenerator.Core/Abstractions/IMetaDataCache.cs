using System.Reflection;

namespace SmartJsonGenerator.Core.Abstractions;

/// <summary>Provides cached, pre-compiled metadata for types processed by <c>SmartGenerator</c>.</summary>
public interface IMetaDataCache
{
    /// <summary>
    /// Returns the <see cref="TypeMetadata"/> for <paramref name="type"/>,
    /// building and caching it on first access.
    /// </summary>
    TypeMetadata GetOrAdd(Type type);
}

// ConstructorLinkedProperties : O(1) set — "was this property already populated by the ctor?"
// CompiledConstructor         : Expression-tree-compiled delegate for parameterized constructors.
// CompiledParameterlessConstructor : Expression-tree-compiled factory for parameterless ctors / value types.

/// <summary>
/// Pre-compiled reflection metadata for a single type. Sealed to prevent inheritance overhead.
/// All constructor delegates are Expression-tree-compiled once and cached for the lifetime of the process.
/// </summary>
/// <param name="Type">The reflected CLR type.</param>
/// <param name="IsRecordOrImmutable"><see langword="true"/> if the type is a record or uses constructor-based initialization.</param>
/// <param name="BestConstructor">The selected constructor (most parameters wins). <see langword="null"/> for abstract types.</param>
/// <param name="ConstructorParameters">Ordered list of constructor parameter descriptors.</param>
/// <param name="WritableProperties">Writable (and init-only) properties of the type.</param>
/// <param name="ConstructorLinkedProperties">O(1) set — "was this property already populated by the constructor?"</param>
/// <param name="CompiledConstructor">Expression-tree-compiled delegate for constructors that require arguments.</param>
/// <param name="CompiledParameterlessConstructor">Expression-tree-compiled factory for parameterless constructors and value types.</param>
public sealed record TypeMetadata(
    Type Type,
    bool IsRecordOrImmutable,
    ConstructorInfo? BestConstructor,
    IReadOnlyList<ParameterMetadata> ConstructorParameters,
    IReadOnlyList<IPropertyMetadata> WritableProperties,
    HashSet<string> ConstructorLinkedProperties,
    Func<object?[], object>? CompiledConstructor,
    Func<object>? CompiledParameterlessConstructor
);

/// <summary>Describes a single constructor parameter and its linked public property.</summary>
/// <param name="Name">Raw parameter name (camelCase).</param>
/// <param name="ParameterType">CLR type of the parameter.</param>
/// <param name="LinkedPropertyName">Matching public property name (PascalCase).</param>
public record ParameterMetadata(string Name, Type ParameterType, string LinkedPropertyName);

/// <summary>Provides compiled access to a single writable property of a type.</summary>
public interface IPropertyMetadata
{
    /// <summary>Property name (PascalCase).</summary>
    string Name { get; }

    /// <summary>CLR type of the property.</summary>
    Type PropertyType { get; }

    /// <summary>Expression-tree-compiled setter: <c>(object target, object? value) => target.Prop = value</c>.</summary>
    Action<object, object?> Setter { get; }
}
