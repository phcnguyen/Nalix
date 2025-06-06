using Nalix.Common.Exceptions;
using Nalix.Shared.Serialization.Buffers;

namespace Nalix.Shared.Serialization.Formatters.Primitives;

/// <summary>
/// Cung cấp serialize/deserialize cho kiểu string với hiệu năng cao (dùng unsafe, length dạng ushort).
/// </summary>
public sealed class StringFormatter : IFormatter<string>
{
    /// <summary>
    /// Serializes a string value into the provided writer.
    /// </summary>
    /// <param name="writer">The serialization writer used to store the serialized data.</param>
    /// <param name="value">The string value to serialize.</param>
    public unsafe void Serialize(ref DataWriter writer, string value)
    {
        if (value == null)
        {
            // 65535 biểu diễn null
            FormatterProvider.Get<ushort>().Serialize(ref writer, SerializationLimits.Null);
            return;
        }

        if (value.Length == 0)
        {
            FormatterProvider.Get<ushort>().Serialize(ref writer, 0);
            return;
        }

        // Tính trước số byte sẽ cần khi encode UTF8
        int byteCount = System.Text.Encoding.UTF8.GetByteCount(value);
        if (byteCount > SerializationLimits.MaxString)
            throw new SerializationException("The string exceeds the allowed limit.");

        FormatterProvider.Get<ushort>().Serialize(ref writer, (ushort)byteCount);

        if (byteCount > 0)
        {
            writer.Expand(byteCount);
            System.Span<byte> dest = writer.GetSpan(byteCount);

            fixed (char* src = value)
            fixed (byte* pDest = dest)
            {
                // Encode trực tiếp vào dest
                int bytesWritten = System.Text.Encoding.UTF8
                    .GetBytes(src, value.Length, pDest, byteCount);

                if (bytesWritten != byteCount)
                    throw new SerializationException("UTF8 encoding error for the string.");
            }

            writer.Advance(byteCount);
        }
    }

    /// <summary>
    /// Deserializes a string value from the provided reader.
    /// </summary>
    /// <param name="reader">The serialization reader containing the data to deserialize.</param>
    /// <returns>The deserialized string value.</returns>
    /// <exception cref="SerializationException">
    /// Thrown if the string length exceeds the maximum allowed limit.
    /// </exception>
    public unsafe string Deserialize(ref DataReader reader)
    {
        ushort length = FormatterProvider.Get<ushort>().Deserialize(ref reader);
        if (length == 0) return string.Empty;
#pragma warning disable CS8603 // Possible null reference return.
        if (length == SerializationLimits.Null) return null;
#pragma warning restore CS8603 // Possible null reference return.
        if (length > SerializationLimits.MaxString)
            throw new SerializationException("String length out of range");

        System.ReadOnlySpan<byte> dest = reader.GetSpan(length);

        string result;
        fixed (byte* src = dest)
        {
            result = System.Text.Encoding.UTF8.GetString(src, length);
        }

        reader.Advance(length);
        return result;
    }
}
