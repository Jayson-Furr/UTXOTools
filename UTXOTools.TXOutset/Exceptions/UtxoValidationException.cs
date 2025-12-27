namespace UTXOTools.TXOutset.Exceptions;

/// <summary>
/// Exception thrown when a UTXO file validation fails.
/// </summary>
public class UtxoValidationException : UtxoException
{
    /// <summary>
    /// Gets the validation error type.
    /// </summary>
    public ValidationError ErrorType { get; }

    /// <summary>
    /// Initializes a new instance of the UtxoValidationException class.
    /// </summary>
    /// <param name="errorType">The type of validation error.</param>
    /// <param name="message">The error message.</param>
    public UtxoValidationException(ValidationError errorType, string message)
        : base(message)
    {
        ErrorType = errorType;
    }

    /// <summary>
    /// Initializes a new instance of the UtxoValidationException class.
    /// </summary>
    /// <param name="errorType">The type of validation error.</param>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception.</param>
    public UtxoValidationException(ValidationError errorType, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorType = errorType;
    }
}

/// <summary>
/// Types of validation errors that can occur when validating a UTXO file.
/// </summary>
public enum ValidationError
{
    /// <summary>
    /// The file magic bytes are invalid.
    /// </summary>
    InvalidMagic,

    /// <summary>
    /// The file version is not supported.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// The network magic bytes are not recognized.
    /// </summary>
    UnknownNetwork,

    /// <summary>
    /// The UTXO count in the header does not match the actual count.
    /// </summary>
    CountMismatch,

    /// <summary>
    /// A transaction ID is invalid or malformed.
    /// </summary>
    InvalidTxId,

    /// <summary>
    /// A script is invalid or malformed.
    /// </summary>
    InvalidScript,

    /// <summary>
    /// An amount value is invalid.
    /// </summary>
    InvalidAmount,

    /// <summary>
    /// The file is truncated or incomplete.
    /// </summary>
    Truncated,

    /// <summary>
    /// A general validation error.
    /// </summary>
    Other
}
