using System;
using System.Collections.Generic;
using System.Text;

namespace SmartJsonGenerator.Core.Configuration;

public class TypeRuleConfiguration
{
    // Key: PropertyName, Value: Üretici fonksiyon
    public Dictionary<string, Func<object?>> PropertyRules { get; } = new();

    public void AddRule(string propertyName, Func<object?> factory)
    {
        PropertyRules[propertyName] = factory;
    }
}

