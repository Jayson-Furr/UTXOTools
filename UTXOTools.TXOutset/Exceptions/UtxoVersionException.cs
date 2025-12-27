namespace UTXOTools.TXOutset.Exceptions;

/// <summary>
/// Exception thrown when the UTXO file version is not supported.
/// </summary>
public class UtxoVersionException : UtxoException
{
    /// <summary>
    /// Gets the version that was found in the file.
    /// </summary>
    public ushort FoundVersion { get; }

    /// <summary>
    /// Gets the set of supported versions.
    /// </summary>
    public IReadOnlySet<ushort> SupportedVersions { get; }

    /// <summary>
    /// Initializes a new instance of the UtxoVersionException class.
    /// </summary>
    /// <param name="foundVersion">The version found in the file.</param>
    /// <param name="supportedVersions">The set of supported versions.</param>
    public UtxoVersionException(ushort foundVersion, IReadOnlySet<ushort> supportedVersions)
        : base(FormatMessage(foundVersion, supportedVersions))
    {
        FoundVersion = foundVersion;
        SupportedVersions = supportedVersions;
    }

    /// <summary>
    /// Initializes a new instance of the UtxoVersionException class with a custom message.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="foundVersion">The version found in the file.</param>
    /// <param name="supportedVersions">The set of supported versions.</param>
    public UtxoVersionException(string message, ushort foundVersion, IReadOnlySet<ushort> supportedVersions)
        : base(message)
    {
        FoundVersion = foundVersion;
        SupportedVersions = supportedVersions;
    }

    private static string FormatMessage(ushort foundVersion, IReadOnlySet<ushort> supportedVersions)
    {
        string supportedList = string.Join(", ", supportedVersions.OrderBy(v => v));
        return $"UTXO file version {foundVersion} is not supported. Supported versions: {supportedList}.";
    }
}
