namespace SmartJsonGenerator.UnitTests.Models;

public record Address(string City, string Street);

public record Employee(string FullName, string Email, Address HomeAddress);

public record Department(string Name, List<Employee> Team);

public record Company(string BrandName, List<Department> OrgChart);
