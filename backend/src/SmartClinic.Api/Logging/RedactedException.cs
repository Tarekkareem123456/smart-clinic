namespace SmartClinic.Api.Logging;

/// <summary>
/// T-007 — what gets handed to <see cref="ILogger"/> instead of the raw
/// exception, so credential-like text inside exception messages (for
/// example sensitive key/value pairs, Bearer values, JWTs) is never written to a
/// log, whichever provider/formatter is configured.
///
/// Diagnostics are preserved: <see cref="ToString"/> (what console, file and
/// JSON providers print) starts with the ORIGINAL exception type name, then
/// the redacted message chain and the original stack traces; the
/// <see cref="Exception.Message"/> is the redacted original message; the
/// original type is available as <see cref="OriginalTypeName"/>. The
/// original exception object is not retained.
/// Create with <see cref="LogRedactor.ToSafeException"/>.
/// </summary>
public sealed class RedactedException : Exception
{
    private readonly string _description;
    private readonly string? _stackTrace;

    public RedactedException(Exception original)
        : base(LogRedactor.RedactText(original.Message, 4096))
    {
        OriginalTypeName = original.GetType().FullName ?? original.GetType().Name;
        _stackTrace = original.StackTrace;
        _description = LogRedactor.DescribeException(original);
    }

    public string OriginalTypeName { get; }

    public override string? StackTrace => _stackTrace;

    public override string ToString() => _description;
}
