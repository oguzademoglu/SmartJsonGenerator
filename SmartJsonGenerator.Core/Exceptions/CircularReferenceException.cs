using System;
using System.Collections.Generic;
using System.Text;

namespace SmartJsonGenerator.Core.Exceptions;

/// <summary>
/// Reserved for future use. Will be thrown when a circular reference is detected
/// and <c>SmartJsonOptions.ThrowOnCircularReference</c> is <see langword="true"/>.
/// </summary>
public class CircularReferenceException
{
}

