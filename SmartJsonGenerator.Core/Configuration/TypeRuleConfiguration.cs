using System;
using System.Collections.Generic;
using System.Text;

namespace SmartJsonGenerator.Core.Configuration;

/// <summary>
/// Holds per-property factory overrides for a single type.
/// Populated via the fluent <see cref="SmartJsonSettings{T}"/> API.
/// </summary>
public class TypeRuleConfiguration
{
    /// <summary>Maps property names to their custom value factories.</summary>
    public Dictionary<string, Func<object?>> PropertyRules { get; } = new();

    /// <summary>Registers or replaces the factory for <paramref name="propertyName"/>.</summary>
    public void AddRule(string propertyName, Func<object?> factory)
    {
        PropertyRules[propertyName] = factory;
    }
}

