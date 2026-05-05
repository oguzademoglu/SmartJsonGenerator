using System;
using System.Collections.Generic;
using System.Text;

namespace SmartJsonGenerator.UnitTests.Models;

public record Order(Guid Id, decimal Amount, DateTime CreatedAt);

