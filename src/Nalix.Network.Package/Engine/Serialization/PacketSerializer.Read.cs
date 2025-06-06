using Nalix.Common.Exceptions;
using Nalix.Common.Package.Metadata;

namespace Nalix.Network.Package.Engine.Serialization;

public static partial class PacketSerializer
{
    /// <summary>
    /// Reads a packet from a given data span using unsafe direct memory access.
    /// </summary>
    /// <param name="data">The data span to read the packet from.</param>
    /// <returns>The deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if the data is invalid or corrupted.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static unsafe Packet ReadPacket(System.ReadOnlySpan<byte> data)
    {
        if (data.Length < PacketSize.Header)
            throw new PackageException(
                $"Data size ({data.Length}) is smaller than the minimum header size ({PacketSize.Header}).");

        try
        {
            // Use unsafe direct pointer access for header
            fixed (byte* pData = data)
            {
                // Read entire header at once instead of field by field
                PacketHeader* pHeader = (PacketHeader*)pData;

                ushort length = pHeader->Length;

                if (length > data.Length)
                    throw new PackageException($"Packet length ({length}) exceeds available data ({data.Length}).");

                // Extract all fields from the header in one go
                ushort id = pHeader->Id;
                uint checksum = pHeader->Checksum;
                long timestamp = pHeader->Timestamp;
                byte number = pHeader->Number;
                byte type = pHeader->Type;
                byte flags = pHeader->Flags;
                byte priority = pHeader->Priority;

                // Create payload efficiently
                MaterializePayload(
                    data[PacketSize.Header..],
                    length - PacketSize.Header,
                    out System.Byte[] payload);

                return new Packet(id, number, checksum, timestamp, type, flags, priority, payload);
            }
        }
        catch (System.Exception ex) when (ex is not PackageException)
        {
            throw new PackageException("Failed to deserialize packet", ex);
        }
    }

    /// <summary>
    /// Asynchronously reads a packet from a stream.
    /// </summary>
    /// <param name="stream">The stream to read the packet from.</param>
    /// <param name="cancellationToken">A token to observe for cancellation requests.</param>
    /// <returns>A task representing the asynchronous read operation, returning the deserialized packet.</returns>
    /// <exception cref="PackageException">Thrown if any error occurs during reading from the stream.</exception>
    [System.Runtime.CompilerServices.MethodImpl(
        System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public static async System.Threading.Tasks.ValueTask<Packet> ReadFromStreamAsync(
        System.IO.Stream stream,
        System.Threading.CancellationToken cancellationToken = default)
    {
        // Use thread-local buffer for the header to reduce allocations
        byte[] headerBuffer = RentHeaderBuffer();

        try
        {
            // Read the packet header
            int bytesRead = await stream.ReadAtLeastAsync(headerBuffer, PacketSize.Header, throwOnEndOfStream: true, cancellationToken);

            if (bytesRead < PacketSize.Header)
                throw new PackageException(
                    $"Failed to read the packet header. Got {bytesRead} bytes instead of {PacketSize.Header}.");

            // Read the packet length from the header
            ushort length = System.Runtime.InteropServices.MemoryMarshal.Read<ushort>(headerBuffer);

            if (length < PacketSize.Header)
                throw new PackageException($"Invalid packet length: {length}. Must be at least {PacketSize.Header}.");

            int payloadSize = length - PacketSize.Header;

            if (payloadSize <= 0)
            {
                // No payload, just return a packet constructed from the header
                return ReadPacket(System.MemoryExtensions.AsSpan(headerBuffer, 0, PacketSize.Header));
            }

            // For payloads, optimize based on size
            if (length <= 8192) // Use shared buffer pool for reasonably sized packets
            {
                byte[] fullBuffer = System.Buffers.ArrayPool<System.Byte>.Shared.Rent(length);
                try
                {
                    // Copy header to the full buffer
                    System.Buffer.BlockCopy(headerBuffer, 0, fullBuffer, 0, PacketSize.Header);

                    // Read the payload directly into the buffer
                    bytesRead = await stream.ReadAtLeastAsync(
                        System.MemoryExtensions.AsMemory(fullBuffer, PacketSize.Header, payloadSize),
                        payloadSize,
                        throwOnEndOfStream: true,
                        cancellationToken);

                    if (bytesRead < payloadSize)
                        throw new PackageException($"Failed to read the full packet payload. Got {bytesRead} bytes instead of {payloadSize}.");

                    return ReadPacket(System.MemoryExtensions.AsSpan(fullBuffer, 0, length));
                }
                finally
                {
                    System.Buffers.ArrayPool<System.Byte>.Shared.Return(fullBuffer, clearArray: true);
                }
            }
            else // For extremely large packets, avoid exhausting the pool
            {
                // Allocate a new buffer for the full packet
                byte[] fullBuffer = new byte[length];

                // Copy header to the full buffer
                System.Buffer.BlockCopy(headerBuffer, 0, fullBuffer, 0, PacketSize.Header);

                // Read the payload directly into the buffer
                bytesRead = await stream.ReadAtLeastAsync(
                    System.MemoryExtensions.AsMemory(fullBuffer, PacketSize.Header, payloadSize),
                    payloadSize,
                    throwOnEndOfStream: true,
                    cancellationToken);

                if (bytesRead < payloadSize)
                    throw new PackageException($"Failed to read the full packet payload. Got {bytesRead} bytes instead of {payloadSize}.");

                return ReadPacket(fullBuffer);
            }
        }
        catch (System.Exception ex) when (ex is not PackageException and not System.OperationCanceledException)
        {
            throw new PackageException("Failed to read packet from stream", ex);
        }
    }
}
