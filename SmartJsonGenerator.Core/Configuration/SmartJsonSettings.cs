using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SmartJsonGenerator.Core.Configuration;

/// <summary>
/// Fluent builder for registering per-property value overrides on type <typeparamref name="T"/>.
/// </summary>
/// <typeparam name="T">The target type whose properties can be configured.</typeparam>
public class SmartJsonSettings<T>
{
    private readonly TypeRuleConfiguration _config;

    /// <summary>Initializes the settings builder with the given rule configuration store.</summary>
    public SmartJsonSettings(TypeRuleConfiguration config) => _config = config;

    /// <summary>
    /// Registers a custom factory for the property selected by <paramref name="propertyExpression"/>.
    /// The factory is called instead of the default value generator every time this property is populated.
    /// </summary>
    public SmartJsonSettings<T> RuleFor<TProperty>(
        Expression<Func<T, TProperty>> propertyExpression,
        Func<TProperty> factory)
    {
        var body = propertyExpression.Body;
        if (body is UnaryExpression unary)
        {
            body = unary.Operand;
        }

        if (body is MemberExpression member)
        {
            _config.AddRule(member.Member.Name, () => factory()!);
        }
        return this;
    }
}

