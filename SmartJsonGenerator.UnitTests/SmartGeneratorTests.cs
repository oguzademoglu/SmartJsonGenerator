using FluentAssertions;
using SmartJsonGenerator.Core.Abstractions;
using SmartJsonGenerator.Core.Abstractions.ValueGenerators;
using SmartJsonGenerator.Core.Caching;
using SmartJsonGenerator.Core.Configuration;
using SmartJsonGenerator.Core.Core;
using SmartJsonGenerator.UnitTests.Models;
using System.Text.Json;

namespace SmartJsonGenerator.UnitTests;

public class SmartGeneratorTests
{
    private readonly SmartGenerator _sut;
    private readonly SmartJsonOptions _options;

    public SmartGeneratorTests()
    {
        _options = new SmartJsonOptions();
        var cache = new ConcurrentMetadataCache();

        // Generator kayıt sırası önemlidir: en spesifik önce, en genel fallback sona.
        // CustomRuleGenerator, isim bazlı string tespiti (Email, City, Name…) yaptığından
        // StringValueGenerator'dan ÖNCE gelmeli; aksi hâlde string tipi için her zaman
        // StringValueGenerator kazanır ve semantic üretim devre dışı kalır.
        var generators = new List<IValueGenerator>
        {
            new CustomRuleGenerator(),   // email / city / name / phone… (spesifik)
            new NumericValueGenerator(), // int / long / decimal…
            new DateTimeValueGenerator(), // DateTime / DateOnly / TimeSpan…
            new StringValueGenerator()   // generic fallback — her string'i üretir
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

    // -------------------------------------------------------------------------
    // Company / Org-chart tests
    // -------------------------------------------------------------------------

    [Fact]
    public void Generate_Company_ShouldPopulate_NestedRecords()
    {
        // Act
        var company = _sut.Generate<Company>();

        // Assert — top-level record
        company.Should().NotBeNull();
        company.BrandName.Should().NotBeNullOrEmpty();

        // Assert — nested list of departments
        company.OrgChart.Should().NotBeNull();
        company.OrgChart.Should().HaveCount(_options.DefaultCollectionSize);

        foreach (var dept in company.OrgChart)
        {
            dept.Name.Should().NotBeNullOrEmpty();
            dept.Team.Should().NotBeNull()
                .And.HaveCount(_options.DefaultCollectionSize);

            foreach (var emp in dept.Team)
            {
                emp.FullName.Should().NotBeNullOrEmpty();
                emp.Email.Should().NotBeNullOrEmpty();

                // Assert — doubly-nested record
                emp.HomeAddress.Should().NotBeNull();
                emp.HomeAddress.City.Should().NotBeNullOrEmpty();
                emp.HomeAddress.Street.Should().NotBeNullOrEmpty();
            }
        }
    }

    [Fact]
    public void GenerateMany_Company_200_ShouldAllBeValid()
    {
        // Act — generate 200 Company instances
        var companies = _sut.GenerateMany<Company>(200).ToList();

        // Assert — count
        companies.Should().HaveCount(200);

        // Assert — every record is properly populated
        companies.Should().AllSatisfy(company =>
        {
            company.BrandName.Should().NotBeNullOrEmpty();
            company.OrgChart.Should().NotBeNullOrEmpty();

            company.OrgChart.Should().AllSatisfy(dept =>
            {
                dept.Name.Should().NotBeNullOrEmpty();
                dept.Team.Should().NotBeNullOrEmpty();

                dept.Team.Should().AllSatisfy(emp =>
                {
                    emp.FullName.Should().NotBeNullOrEmpty();
                    emp.Email.Should().NotBeNullOrEmpty();
                    emp.HomeAddress.Should().NotBeNull();
                    emp.HomeAddress.City.Should().NotBeNullOrEmpty();
                });
            });
        });
    }

    [Fact]
    public void GenerateJson_Company_200_WriteToFile()
    {
        var options = new SmartJsonOptions { DefaultCollectionSize = 2 };

        string[] brands    = ["Acme Corp", "Globex", "Initech", "Umbrella Ltd", "Stark Industries", "Wayne Enterprises", "Dunder Mifflin", "Vandelay Industries"];
        string[] deptNames = ["Engineering", "Marketing", "Sales", "HR", "Finance", "Operations", "R&D", "Customer Success"];
        string[] streets   = ["Baker Street", "Fifth Avenue", "Elm Street", "Sunset Blvd", "Wall Street", "Broadway", "Main Street", "Park Lane"];
        string[] domains   = ["corp.com", "ltd.io", "inc.co", "global.org", "tech.net"];

        options.GlobalNamingRules["BrandName"] = () => brands[Random.Shared.Next(brands.Length)];
        options.GlobalNamingRules["Name"]      = () => deptNames[Random.Shared.Next(deptNames.Length)];
        options.GlobalNamingRules["Email"]     = () =>
        {
            string[] first = ["alice", "bob", "carol", "dave", "eve", "frank", "grace", "hank", "iris", "jake"];
            string[] last  = ["smith", "jones", "brown", "taylor", "white", "harris", "clark"];
            return $"{first[Random.Shared.Next(first.Length)]}.{last[Random.Shared.Next(last.Length)]}@{domains[Random.Shared.Next(domains.Length)]}";
        };
        options.GlobalNamingRules["Street"] = () => $"{Random.Shared.Next(1, 500)} {streets[Random.Shared.Next(streets.Length)]}";

        var generators = new List<IValueGenerator>
        {
            new CustomRuleGenerator(),
            new NumericValueGenerator(),
            new DateTimeValueGenerator(),
            new StringValueGenerator()
        };
        var sut = new SmartGenerator(new ConcurrentMetadataCache(), generators, options);

        var json = sut.GenerateJson<Company>(200);

        var path = Path.Combine(Path.GetTempPath(), "companies_200.json");
        File.WriteAllText(path, json);
    }

    [Fact]
    public void GenerateJson_Company_200_ShouldProduceValidJsonArray()
    {
        // Arrange — GlobalNamingRules ile anlamlı veriler üret
        var options = new SmartJsonOptions();
        options.GlobalNamingRules["Email"]    = () => $"user{Random.Shared.Next(1000, 9999)}@company.com";
        options.GlobalNamingRules["Street"]   = () => $"{Random.Shared.Next(1, 999)} Main Street";
        options.GlobalNamingRules["BrandName"] = () =>
        {
            string[] brands = ["Acme Corp", "Globex", "Initech", "Umbrella", "Stark Industries", "Wayne Enterprises"];
            return brands[Random.Shared.Next(brands.Length)];
        };

        var generators = new List<IValueGenerator>
        {
            new CustomRuleGenerator(),
            new NumericValueGenerator(),
            new DateTimeValueGenerator(),
            new StringValueGenerator()
        };
        var sut = new SmartGenerator(new ConcurrentMetadataCache(), generators, options);

        // Act
        var json = sut.GenerateJson<Company>(200);

        // Assert — geçerli JSON array
        json.Should().StartWith("[");
        json.Should().EndWith("]");

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().Be(200);

        // Her elemanda BrandName ve OrgChart property'si olmalı
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            element.TryGetProperty("BrandName", out _).Should().BeTrue();
            element.TryGetProperty("OrgChart", out _).Should().BeTrue();
        }
    }
}

