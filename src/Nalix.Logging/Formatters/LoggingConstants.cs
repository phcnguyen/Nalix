namespace Nalix.Logging.Formatters;

/// <summary>
/// Contains constant values used for logging.
/// </summary>
public static class LoggingConstants
{
    /// <summary>
    /// The opening bracket character used in log formatting.
    /// </summary>
    public const System.Char LogBracketOpen = '[';

    /// <summary>
    /// The closing bracket character used in log formatting.
    /// </summary>
    public const System.Char LogBracketClose = ']';

    /// <summary>
    /// The default space separator used in log messages.
    /// </summary>
    public const System.Char LogSpaceSeparator = ' ';

    /// <summary>
    /// The default dash separator used in log messages.
    /// </summary>
    public const System.Char LogDashSeparator = '-';

    /// <summary>
    /// The default buffer size for logging operations.
    /// </summary>
    public const System.Int32 DefaultLogBufferSize = 60;
}
