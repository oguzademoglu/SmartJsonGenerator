using FluentAssertions;
using SmartJsonGenerator.Core.Abstractions;
using SmartJsonGenerator.Core.Abstractions.ValueGenerators;
using SmartJsonGenerator.Core.Caching;
using SmartJsonGenerator.Core.Configuration;
using SmartJsonGenerator.Core.Core;
using SmartJsonGenerator.UnitTests.Models;

namespace SmartJsonGenerator.UnitTests;

public class SmartGeneratorTests
{
    private readonly SmartGenerator _sut;
    private readonly SmartJsonOptions _options;

    public SmartGeneratorTests()
    {
        _options = new SmartJsonOptions();
        var cache = new ConcurrentMetadataCache();

        // Default generatorları manuel enjekte ediyoruz (Pure DI)
        var generators = new List<IValueGenerator>
        {
            new NumericValueGenerator(),
            new StringValueGenerator(),
            new DateTimeValueGenerator(),
            new CustomRuleGenerator()
        };

        _sut = new SmartGenerator(cache, generators, _options);
    }

    [Fact]
    public void Generate_ShouldHandle_CircularReferences_WithoutCrashing()
    {
        // Act: Kendi kendini referans veren bir yapıyı tetikle
        var user = _sut.Generate<User>();

        // Assert
        user.Should().NotBeNull();
        user.Manager.Should().BeNull(); // MaxDepth veya Tracker dairesel bağı kırmalı
    }

    [Fact]
    public void Generate_ShouldApply_FluentRules_WithHighPriority()
    {
        // Arrange
        var expectedName = "Architect Partner";

        // GetOrAdd artık ConcurrentDictionary ile hata vermeyecektir
        var config = _options.Rules.GetOrAdd(typeof(User), _ => new TypeRuleConfiguration());

        var userSettings = new SmartJsonSettings<User>(config);
        userSettings.RuleFor(u => u.FullName, () => expectedName);

        // Act
        var user = _sut.Generate<User>();

        // Assert (using FluentAssertions; gerektirir)
        user.FullName.Should().Be(expectedName);
    }

    [Fact]
    public void GenerateJson_ShouldProduce_ValidStreamingOutput()
    {
        // Act
        var json = _sut.GenerateJson<Order>(count: 5);

        // Assert
        json.Should().StartWith("[");
        json.Should().Contain("\"Amount\":");
    }
}

