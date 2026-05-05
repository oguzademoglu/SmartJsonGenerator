using System;
using System.Collections.Generic;
using System.Text;

namespace SmartJsonGenerator.UnitTests.Models;

public record User(
int Id,
string FullName,
List<Order> Orders,
Cat Pet,
User? Manager // Dairesel referans testi için
);

