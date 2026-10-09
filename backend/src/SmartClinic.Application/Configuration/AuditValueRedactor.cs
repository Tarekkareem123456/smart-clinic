using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace SmartClinic.Application.Configuration;

public static partial class AuditValueRedactor
{
    public const string Redacted = "[REDACTED]";

    private static readonly string[] CredentialMarkers =
    [
        "password", "passwd", "pwd", "pass", "pw", "passphrase", "passcode",
        "token", "secret", "authorization", "bearer", "cookie", "credential", "session",
        "sid", "otp", "otpcode", "pin", "pincode", "apikey", "privatekey",
        "secretkey", "signingkey", "encryptionkey", "masterkey", "hmackey",
        "sshkey", "recoverycode", "recoverykey", "jwtkey", "sessionid", "accesstoken", "refreshtoken",
        "clientsecret", "clienttoken", "clientkey", "totpsecret", "accountkey", "sharedaccesskey",
        "accesskey", "authkey", "licensekey", "signature", "auth",
        "verificationcode", "securitycode", "2facode", "mfacode", "resetcode", "logincode", "backupcode",
        "mfaverificationcode", "mfaresetcode", "mfalogincode", "resetpasswordcode", "loginverificationcode",
        "backupcodes", "recoverycodes", "confirmationcode", "accesscode", "activationcode", "onetimecode",
        "كلمةالمرور", "كلمةالسر", "الرقمالسري", "الرمزالسري", "كلمهمرور", "رقمسري", "رمز", "مفتاح",
        "باسورد", "توكن", "الباسوورد", "باسوورد"
    ];
    private static readonly string[] NormalizedCredentialMarkers = CredentialMarkers
        .Select(NormalizeCredentialText)
        .ToArray();
    private static readonly string[] NormalizedArabicCredentialMarkers = NormalizedCredentialMarkers
        .Where(marker => marker.Any(character => character > 127))
        .ToArray();

    public static string? Redact(string? fieldName, string? value)
    {
        if (value is null)
        {
            return null;
        }

        if (string.Equals(value, Redacted, StringComparison.Ordinal))
        {
            return Redacted;
        }

        if (!IsSensitiveName(fieldName) && TryRedactSingleAccessTokenPair(value, out var redactedPair))
        {
            return redactedPair;
        }

        if (IsSensitiveName(fieldName) || ContainsSensitiveMaterial(value))
        {
            return RedactWholeValue(value);
        }

        return value;
    }

    private static bool TryRedactSingleAccessTokenPair(string value, out string redactedValue)
    {
        redactedValue = value;
        var matches = AccessTokenPairRegex().Matches(value);
        if (matches.Count != 1)
        {
            return false;
        }

        var match = matches[0];
        var secretValue = match.Groups["value"];
        if (secretValue.Value.Length == 0 || secretValue.Value[0] is '\"' or '\'')
        {
            return false;
        }

        var remainder = value.Remove(match.Index, match.Length);
        if (ContainsSensitiveMaterial(remainder))
        {
            return false;
        }

        redactedValue = string.Concat(
            value[..secretValue.Index],
            Redacted,
            value[(secretValue.Index + secretValue.Length)..]);
        return true;
    }

    private static bool IsSensitiveName(string? name) => ContainsCredentialMarker(name);

    private static bool ContainsSensitiveMaterial(string value) =>
        ContainsCredentialMarkerInValue(value) ||
        JwtRegex().IsMatch(value) ||
        BasicCredentialsRegex().IsMatch(value) ||
        KnownTokenPrefixRegex().IsMatch(value) ||
        CredentialUriRegex().IsMatch(value) ||
        FreeTextUserInfoRegex().IsMatch(value);

    private static bool ContainsCredentialMarker(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var compact = NormalizeCredentialText(text);
        return ContainsNormalizedCredentialMarker(compact);
    }

    private static bool ContainsCredentialMarkerInValue(string value)
    {
        string? previousWord = null;
        string? wordBeforePrevious = null;
        for (var index = 0; index < value.Length;)
        {
            while (index < value.Length && !IsCredentialCharacter(value[index]))
            {
                index++;
            }

            var start = index;
            while (index < value.Length && IsCredentialCharacter(value[index]))
            {
                index++;
            }

            if (index > start)
            {
                var word = NormalizeCredentialText(value[start..index]);
                if (word.Length == 0)
                {
                    continue;
                }

                if (ContainsNormalizedCredentialMarker(word))
                {
                    return true;
                }

                if (previousWord is not null && ContainsCompoundCredentialMarker(previousWord + word))
                {
                    return true;
                }

                if (previousWord is not null && ContainsArabicCredentialPhrase(previousWord + word))
                {
                    return true;
                }

                if (wordBeforePrevious is not null && previousWord is not null &&
                    ContainsCompoundCredentialMarker(wordBeforePrevious + previousWord + word))
                {
                    return true;
                }

                wordBeforePrevious = previousWord;
                previousWord = word;
            }
        }

        return false;
    }

    private static bool ContainsCompoundCredentialMarker(string text) =>
        CredentialMarkers.Any(marker => IsCompoundCredentialMarker(marker) &&
            text.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool IsCompoundCredentialMarker(string marker) =>
        marker.EndsWith("key", StringComparison.Ordinal) ||
        marker.EndsWith("code", StringComparison.Ordinal) ||
        marker.EndsWith("token", StringComparison.Ordinal) ||
        marker.EndsWith("secret", StringComparison.Ordinal) ||
        (marker.Length > 3 && marker.EndsWith("id", StringComparison.Ordinal));

    private static string NormalizeCredentialText(string text)
    {
        var normalized = new System.Text.StringBuilder(text.Length);
        foreach (var character in text)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark || character == '\u0640')
            {
                continue;
            }

            var canonical = character switch
            {
                '\u0623' or '\u0625' or '\u0622' or '\u0671' => '\u0627',
                '\u0629' => '\u0647',
                '\u0649' => '\u064a',
                _ => character
            };

            if (char.IsLetterOrDigit(canonical))
            {
                normalized.Append(canonical);
            }
        }

        return normalized.ToString();
    }

    private static bool ContainsNormalizedCredentialMarker(string compact) =>
        string.Equals(compact, "sig", StringComparison.OrdinalIgnoreCase) ||
        NormalizedCredentialMarkers.Any(marker => compact.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static bool ContainsArabicCredentialPhrase(string compact) =>
        NormalizedArabicCredentialMarkers.Any(marker => compact.Contains(marker, StringComparison.OrdinalIgnoreCase));

    private static string RedactWholeValue(string value)
    {
        var firstContentIndex = 0;
        while (firstContentIndex < value.Length && char.IsWhiteSpace(value[firstContentIndex]))
        {
            firstContentIndex++;
        }

        var lastContentIndex = value.Length - 1;
        while (lastContentIndex >= firstContentIndex && char.IsWhiteSpace(value[lastContentIndex]))
        {
            lastContentIndex--;
        }

        return firstContentIndex <= lastContentIndex && IsJsonValue(value[firstContentIndex..(lastContentIndex + 1)])
            ? JsonSerializer.Serialize(Redacted)
            : Redacted;
    }

    private static bool IsJsonValue(string value)
    {
        if (value[0] is '{' or '[' or '"')
        {
            return true;
        }

        return value is "true" or "false" or "null" || JsonNumberRegex().IsMatch(value);
    }

    private static bool IsCredentialCharacter(char value) =>
        char.IsLetterOrDigit(value) || value == '\u0640' || CharUnicodeInfo.GetUnicodeCategory(value) is
            UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark;

    [GeneratedRegex(@"\bBasic\s+[A-Za-z0-9+/]{8,}={0,2}", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex BasicCredentialsRegex();

    [GeneratedRegex(@"(?:AKIA[A-Z0-9]{16}|gh[pousr]_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,}|sk_(?:live|test)_[A-Za-z0-9_-]{8,}|sk-proj-[A-Za-z0-9_-]{8,}|xox[baprs]-[A-Za-z0-9-]{10,}|sk-ant-[A-Za-z0-9-]{12,}|glpat-[A-Za-z0-9_-]{12,}|rk_(?:live|test)_[A-Za-z0-9]{8,}|AIzaSy[A-Za-z0-9_-]{16,}|whsec_[A-Za-z0-9_-]{8,})", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex KnownTokenPrefixRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]*\.[A-Za-z0-9_-]+(?:\.[A-Za-z0-9_-]*)?", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"\b[a-z][a-z0-9+.-]*://[^\s]*@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex CredentialUriRegex();

    [GeneratedRegex(@"(?:^|[^\p{L}\p{N}])[^/\s:@]*:[^@\s/]+@", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex FreeTextUserInfoRegex();

    [GeneratedRegex(@"^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex JsonNumberRegex();

    [GeneratedRegex(@"(?<key>[\p{L}\p{N}_.-]*access[_-]?token[\p{L}\p{N}_.-]*)\s*[:=]\s*(?<value>[^,\s;}&\]\)]+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex AccessTokenPairRegex();
}
