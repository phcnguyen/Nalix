namespace Nalix.Common.Exceptions;

/// <summary>
/// Custom exception for errors related to cryptographic operations.
/// </summary>
[System.Serializable]
public class CryptoException : BaseException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public CryptoException(System.String message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="CryptoException"/> class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public CryptoException(System.String message, System.Exception innerException)
        : base(message, innerException)
    {
    }
}
