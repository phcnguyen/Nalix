using System;

namespace Nalix.Common.Serialization.Attributes;

/// <summary>
/// Specifies that a field or property should be ignored during serialization.
/// </summary>
[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
public sealed class SerializeIgnoreAttribute : Attribute
{ }
