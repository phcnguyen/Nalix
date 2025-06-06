using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Provides high-performance serialization and deserialization for nullable value types.
/// </summary>
/// <typeparam name="T">
/// The underlying value type, which must have a registered formatter.
/// </typeparam>
public sealed class NullableFormatter<T> : IFormatter<T?> where T : struct
{
    /// <summary>
    /// Flag indicating that the value is null.
    /// </summary>
    private const byte NoValueFlag = 0;

    /// <summary>
    /// Flag indicating that the value is present.
    /// </summary>
    private const byte HasValueFlag = 1;

    /// <summary>
    /// Serializes a nullable value into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The nullable value to serialize.</param>
    public void Serialize(ref DataWriter writer, T? value)
    {
        // 0 = null, 1 = has value
        FormatterProvider
            .Get<byte>()
            .Serialize(ref writer, value.HasValue ? HasValueFlag : NoValueFlag);

        if (value.HasValue)
        {
            FormatterProvider.Get<T>().Serialize(ref writer, value.Value);
        }
    }

    /// <summary>
    /// Deserializes a nullable value from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized nullable value.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the nullable data is invalid or has an unexpected format.
    /// </exception>
    public T? Deserialize(ref DataReader reader)
    {
        byte hasValue = FormatterProvider
            .Get<byte>()
            .Deserialize(ref reader);

        if (hasValue == NoValueFlag) return null;
        if (hasValue != HasValueFlag) throw new SerializationException("Invalid nullable data!");

        return FormatterProvider.Get<T>().Deserialize(ref reader);
    }
}
