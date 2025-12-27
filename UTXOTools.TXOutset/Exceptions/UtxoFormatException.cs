namespace UTXOTools.TXOutset.Exceptions;

/// <summary>
/// Exception thrown when the UTXO file format is invalid or corrupted.
/// </summary>
public class UtxoFormatException : UtxoException
{
    /// <summary>
    /// Gets the position in the file where the error occurred, if available.
    /// </summary>
    public long? Position { get; }

    /// <summary>
    /// Initializes a new instance of the UtxoFormatException class.
    /// </summary>
    public UtxoFormatException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFormatException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UtxoFormatException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFormatException class with a message and position.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position in the file where the error occurred.</param>
    public UtxoFormatException(string message, long position) : base($"{message} (at position {position})")
    {
        Position = position;
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFormatException class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UtxoFormatException(string message, Exception innerException) : base(message, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the UtxoFormatException class with a message, position, and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="position">The position in the file where the error occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public UtxoFormatException(string message, long position, Exception innerException)
        : base($"{message} (at position {position})", innerException)
    {
        Position = position;
    }
}
