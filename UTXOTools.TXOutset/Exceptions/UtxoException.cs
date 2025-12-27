namespace UTXOTools.TXOutset.Exceptions;

/// <summary>
/// Base exception for all UTXO file-related errors.
/// </summary>
public class UtxoException : Exception
{
    /// <summary>
    /// Initializes a new instance of the UtxoException class.
    /// </summary>
    public UtxoException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the UtxoException class with a message.
    /// </summary>
    /// <param name="message">The error message.</param>
    public UtxoException(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the UtxoException class with a message and inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UtxoException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
