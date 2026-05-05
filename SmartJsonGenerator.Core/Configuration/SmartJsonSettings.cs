using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;

namespace SmartJsonGenerator.Core.Configuration;

public class SmartJsonSettings<T>
{
    private readonly TypeRuleConfiguration _config;

    public SmartJsonSettings(TypeRuleConfiguration config) => _config = config;

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

