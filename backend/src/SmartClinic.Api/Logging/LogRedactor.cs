using System.Text;
using System.Text.RegularExpressions;

namespace SmartClinic.Api.Logging;

/// <summary>
/// T-007 — the ONE place that decides what request data may be written to
/// logs. Sensitive authentication/security material (passwords, access and
/// refresh tokens, MFA/TOTP secrets, API keys, cookies, Authorization
/// values) is replaced with <see cref="Redacted"/>; ordinary diagnostic
/// data (method, path, non-sensitive query keys, status, timing) is kept.
///
/// Design: fail-safe. Key names are matched by substring so new variants
/// (<c>refresh_token</c>, <c>X-Api-Key</c>, <c>mfaCode</c>) are covered
/// without a code change, and headers are logged by ALLOW-LIST only, so an
/// unknown header is never logged at all.
/// </summary>
public static class LogRedactor
{
    public const string Redacted = "[REDACTED]";

    private const int MaxValueLength = 256;

    // Compared against the key lower-cased with '-', '_' and '.' removed.
    private static readonly string[] SensitiveKeyFragments =
    {
        "password", "passwd", "passphrase", "passcode", "token", "secret",
        "authorization", "cookie", "apikey", "credential", "privatekey",
        "sessionid", "totp", "mfa", "otpcode",
    };

    private static readonly HashSet<string> SensitiveExactKeys = new(StringComparer.Ordinal)
    {
        "pwd", "otp", "pin", "auth",
    };

    // Only these request headers are ever logged (values still pass
    // through RedactValue). Everything else — Authorization, Cookie,
    // X-Api-Key, custom headers — is simply never written.
    private static readonly string[] SafeHeaderAllowList =
    {
        "User-Agent", "Content-Type", "Content-Length", "Accept",
    };

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private const RegexOptions Options = RegexOptions.CultureInvariant | RegexOptions.Compiled;

    private static readonly Regex BearerPattern =
        new(@"\bBearer\s+[A-Za-z0-9\-._~+/]+=*", Options | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex BasicPattern =
        new(@"\bBasic\s+[A-Za-z0-9+/]+=*", Options | RegexOptions.IgnoreCase, RegexTimeout);

    private static readonly Regex JwtPattern =
        new(@"\beyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]*", Options, RegexTimeout);

    // "Authorization: <scheme> <credential>": the first token and ONE following
    // token on the same line are replaced, whatever the scheme is (Bearer,
    // Basic, Negotiate, NTLM, Token, or any custom/unknown one), so the
    // credential can never be left behind; text after them survives.
    // Fail-safe: a bare credential with no scheme also costs the next token.
    // Digest carries comma-separated parameters, so it goes to end of line.
    private static readonly Regex AuthorizationPattern =
        new(@"(?<name>authorization)(?<sep>[""']?\s*:\s*)(?:Digest[^\r\n]*|[^\s,;]+(?:[ \t]+[^\s,;]+)?)",
            Options | RegexOptions.IgnoreCase, RegexTimeout);

    // "Cookie: a=1; b=2" / "Set-Cookie: sid=x; Path=/": the whole
    // ';'-separated cookie list is replaced, but not text after the next space.
    private static readonly Regex CookiePattern =
        new(@"(?<name>set[_\-]?cookie|cookie)(?<sep>[""']?\s*:\s*)[^\s;]+(?:;[ \t]*[^\s;]+)*;?",
            Options | RegexOptions.IgnoreCase, RegexTimeout);

    // Generic key=value / key: value / "key":"value" candidates, split in two
    // so a non-sensitive "key: value" never swallows the sensitive pair that
    // follows it ("failed: auth: X"). Whether a value is redacted is decided
    // by IsSensitiveKey(key) — the SAME policy used for query strings and
    // headers — so free text cannot drift out of sync with it.
    private static readonly Regex KeySeparatorPattern =
        new(@"(?<key>[A-Za-z0-9_.\-]+)(?<sep>[""']?\s*[=:]\s*)", Options, RegexTimeout);

    private static readonly Regex ValuePattern =
        new(@"\G(?:""[^""]*""|'[^']*'|[^\s&,;]+)", Options, RegexTimeout);

    public static bool IsSensitiveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var normalized = key.Replace("-", string.Empty).Replace("_", string.Empty).Replace(".", string.Empty)
            .ToLowerInvariant();

        if (SensitiveExactKeys.Contains(normalized))
        {
            return true;
        }

        foreach (var fragment in SensitiveKeyFragments)
        {
            if (normalized.Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Redacts by key name first, then scrubs credential-looking text inside the value.</summary>
    public static string RedactValue(string? key, string? value)
    {
        if (IsSensitiveKey(key))
        {
            return Redacted;
        }

        return RedactText(value);
    }

    /// <summary>
    /// Scrubs credential-looking material (Bearer/Basic values, JWTs,
    /// sensitive key/value pairs from free text, removes control
    /// characters (log forging) and bounds the length.
    /// </summary>
    public static string RedactText(string? text, int maxLength = MaxValueLength)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        try
        {
            var result = BearerPattern.Replace(text, "Bearer " + Redacted);
            result = BasicPattern.Replace(result, "Basic " + Redacted);
            result = JwtPattern.Replace(result, Redacted);
            result = AuthorizationPattern.Replace(result, m => m.Groups["name"].Value + m.Groups["sep"].Value + Redacted);
            result = CookiePattern.Replace(result, m => m.Groups["name"].Value + m.Groups["sep"].Value + Redacted);
            result = ScrubKeyValues(result);
            return Bound(StripControlCharacters(result), maxLength);
        }
        catch (RegexMatchTimeoutException)
        {
            // Fail safe: if we cannot prove the text is clean, do not log it.
            return Redacted;
        }
    }

    private static string ScrubKeyValues(string text)
    {
        var builder = new StringBuilder(text.Length);
        var position = 0;

        for (var match = KeySeparatorPattern.Match(text); match.Success; match = match.NextMatch())
        {
            var valueStart = match.Index + match.Length;
            if (match.Index < position || !IsSensitiveKey(match.Groups["key"].Value))
            {
                continue; // inside an already-redacted value, or a harmless key
            }

            var value = ValuePattern.Match(text, valueStart);
            builder.Append(text, position, valueStart - position).Append(Redacted);
            position = value.Success ? valueStart + value.Length : valueStart;
        }

        builder.Append(text, position, text.Length - position);
        return builder.ToString();
    }

    /// <summary>Returns "?a=b&amp;c=[REDACTED]" (or "" when there is no query). Fully materialized string.</summary>
    public static string RedactQuery(IQueryCollection query)
    {
        if (query.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder("?");
        var first = true;
        foreach (var pair in query)
        {
            foreach (var value in pair.Value)
            {
                if (!first)
                {
                    builder.Append('&');
                }

                first = false;
                builder.Append(RedactText(pair.Key)).Append('=').Append(RedactValue(pair.Key, value));
            }
        }

        return Bound(builder.ToString());
    }

    /// <summary>Allow-listed request headers as a single materialized string ("Name=value; ...").</summary>
    public static string SafeHeaders(IHeaderDictionary headers)
    {
        var parts = new List<string>();
        foreach (var name in SafeHeaderAllowList)
        {
            if (headers.TryGetValue(name, out var values) && values.Count > 0)
            {
                parts.Add(name + "=" + RedactValue(name, values.ToString()));
            }
        }

        return string.Join("; ", parts);
    }

    private const int MaxExceptionTextLength = 4096;
    private const int MaxExceptionChainLength = 16;

    /// <summary>
    /// Builds the redacted, human-readable description of an exception
    /// (type name + message for the exception and its inner exceptions,
    /// then stack traces). Messages pass through <see cref="RedactText"/>;
    /// exception TYPE names and stack traces (code locations only — they
    /// carry no argument values) are kept because they are the diagnostics.
    /// </summary>
    internal static string DescribeException(Exception exception)
    {
        var builder = new StringBuilder();
        var pending = new Stack<Exception>();
        pending.Push(exception);
        var count = 0;
        var isFirst = true;

        while (pending.Count > 0 && count < MaxExceptionChainLength)
        {
            var current = pending.Pop();
            count++;

            if (!isFirst)
            {
                builder.Append("---> ");
            }

            isFirst = false;
            builder.Append(current.GetType().FullName).Append(": ")
                .AppendLine(RedactText(current.Message, MaxExceptionTextLength));

            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                builder.AppendLine(current.StackTrace);
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.Reverse())
                {
                    pending.Push(inner);
                }
            }
            else if (current.InnerException is not null)
            {
                pending.Push(current.InnerException);
            }
        }

        return builder.ToString().TrimEnd();
    }

    /// <summary>Redaction-safe stand-in for an exception that is about to be logged.</summary>
    public static Exception ToSafeException(Exception exception) => new RedactedException(exception);

    private static string StripControlCharacters(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            builder.Append(char.IsControl(ch) ? ' ' : ch);
        }

        return builder.ToString();
    }

    private static string Bound(string value, int maxLength = MaxValueLength) =>
        value.Length <= maxLength ? value : value[..(maxLength - 3)] + "...";
}
