using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using SmartClinic.Api.Logging;
using Xunit;

namespace SmartClinic.Api.Tests;

/// <summary>T-007 — unit tests for the centralized redaction rules.</summary>
public sealed class LogRedactorTests
{
    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("new_password")]
    [InlineData("access_token")]
    [InlineData("refresh_token")]
    [InlineData("refreshToken")]
    [InlineData("id_token")]
    [InlineData("client_secret")]
    [InlineData("Authorization")]
    [InlineData("Cookie")]
    [InlineData("Set-Cookie")]
    [InlineData("X-Api-Key")]
    [InlineData("api_key")]
    [InlineData("totp")]
    [InlineData("totpSecret")]
    [InlineData("mfa_code")]
    [InlineData("otp")]
    public void Sensitive_keys_are_recognized(string key)
    {
        Assert.True(LogRedactor.IsSensitiveKey(key));
        Assert.Equal(LogRedactor.Redacted, LogRedactor.RedactValue(key, "anything-at-all"));
    }

    [Theory]
    [InlineData("page")]
    [InlineData("pageSize")]
    [InlineData("status")]
    [InlineData("footprint")]
    [InlineData("shipping")]
    [InlineData("code")]
    public void Ordinary_keys_are_not_redacted(string key)
    {
        Assert.False(LogRedactor.IsSensitiveKey(key));
        Assert.Equal("value-2", LogRedactor.RedactValue(key, "value-2"));
    }

    [Fact]
    public void Credential_looking_text_is_scrubbed_but_surrounding_text_survives()
    {
        var text = "Login failed for user 42. Authorization: Bearer abc.DEF-123_xyz password=hunter2 access_token=\"tok en\" retry=3";

        var result = LogRedactor.RedactText(text);

        Assert.DoesNotContain("abc.DEF-123_xyz", result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("tok en", result);
        Assert.Contains("Login failed for user 42", result);
        Assert.Contains("retry=3", result);
        Assert.Contains(LogRedactor.Redacted, result);
    }

    [Fact]
    public void Jwt_shaped_values_are_scrubbed_even_without_a_key_name()
    {
        var jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.c2lnbmF0dXJl";

        Assert.DoesNotContain(jwt, LogRedactor.RedactText("value was " + jwt));
    }

    [Fact]
    public void Control_characters_are_removed_to_prevent_log_forging()
    {
        var result = LogRedactor.RedactText("line1\r\nFAKE-LOG-ENTRY");

        Assert.DoesNotContain('\r', result);
        Assert.DoesNotContain('\n', result);
    }

    [Fact]
    public void Query_string_keeps_ordinary_keys_and_redacts_sensitive_ones()
    {
        var query = new QueryCollection(new Dictionary<string, StringValues>
        {
            ["page"] = "2",
            ["password"] = "hunter2",
            ["refresh_token"] = "r-token-value",
        });

        var result = LogRedactor.RedactQuery(query);

        Assert.Contains("page=2", result);
        Assert.Contains("password=" + LogRedactor.Redacted, result);
        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("r-token-value", result);
    }

    [Fact]
    public void Headers_are_logged_by_allow_list_only()
    {
        var headers = new HeaderDictionary
        {
            ["User-Agent"] = "test-agent/1.0",
            ["Authorization"] = "Bearer secret-bearer-value",
            ["Cookie"] = "session=secret-cookie-value",
            ["X-Api-Key"] = "secret-api-key-value",
            ["X-Custom-Unknown"] = "secret-unknown-header-value",
        };

        var result = LogRedactor.SafeHeaders(headers);

        Assert.Contains("User-Agent=test-agent/1.0", result);
        Assert.DoesNotContain("secret-", result);
        Assert.DoesNotContain("Authorization", result);
        Assert.DoesNotContain("Cookie", result);
    }

    // Free-text redaction must cover EVERY category IsSensitiveKey recognizes.
    [Theory]
    [InlineData("password")]
    [InlineData("passwd")]
    [InlineData("passphrase")]
    [InlineData("passcode")]
    [InlineData("access_token")]
    [InlineData("refresh-token")]
    [InlineData("client_secret")]
    [InlineData("api_key")]
    [InlineData("X-Api-Key")]
    [InlineData("credentials")]
    [InlineData("private_key")]
    [InlineData("session_id")]
    [InlineData("totp")]
    [InlineData("mfa_code")]
    [InlineData("otp_code")]
    [InlineData("otp")]
    [InlineData("pin")]
    [InlineData("pwd")]
    [InlineData("auth")]
    [InlineData("cookie")]
    [InlineData("Authorization")]
    public void Free_text_redaction_covers_every_sensitive_key_category(string key)
    {
        Assert.True(LogRedactor.IsSensitiveKey(key));

        foreach (var text in new[] { $"{key}=SECRET-VALUE", $"failed: {key}: SECRET-VALUE tail", $"{{\"{key}\":\"SECRET-VALUE\"}}" })
        {
            Assert.DoesNotContain("SECRET-VALUE", LogRedactor.RedactText(text));
        }
    }

    [Fact]
    public void A_sensitive_pair_inside_a_non_sensitive_value_is_still_redacted()
    {
        var result = LogRedactor.RedactText("Login failed: password=hunter2 for user 42");

        Assert.DoesNotContain("hunter2", result);
        Assert.Contains("Login failed", result);
        Assert.Contains("for user 42", result);
    }

    [Fact]
    public void Multi_value_cookie_and_set_cookie_lines_are_fully_redacted()
    {
        Assert.DoesNotContain("second-cookie", LogRedactor.RedactText("Cookie: a=first-cookie; b=second-cookie"));
        Assert.DoesNotContain("PathSecret", LogRedactor.RedactText("Set-Cookie: sid=x; Path=/PathSecret"));
    }

    [Fact]
    public void Redaction_replaces_only_the_credential_and_keeps_surrounding_text()
    {
        var authorization = LogRedactor.RedactText("retry=1 Authorization: Bearer SECRET-BEARER retry=2");
        Assert.DoesNotContain("SECRET-BEARER", authorization);
        Assert.Contains("retry=1", authorization);
        Assert.Contains("retry=2", authorization);

        var cookie = LogRedactor.RedactText("retry=1 Cookie: a=SECRET-A; b=SECRET-B retry=2");
        Assert.DoesNotContain("SECRET-A", cookie);
        Assert.DoesNotContain("SECRET-B", cookie);
        Assert.Contains("retry=1", cookie);
        Assert.Contains("retry=2", cookie);
    }

    [Theory]
    [InlineData("Custom")]
    [InlineData("AWS4-HMAC-SHA256")]
    [InlineData("Negotiate")]
    [InlineData("NTLM")]
    [InlineData("Token")]
    [InlineData("X-Unknown-Scheme")]
    public void Authorization_credential_is_redacted_for_any_scheme_and_surrounding_text_survives(string scheme)
    {
        var result = LogRedactor.RedactText($"retry=1 Authorization: {scheme} SECRET-VALUE retry=2");

        Assert.DoesNotContain("SECRET-VALUE", result);
        Assert.Contains("retry=1", result);
        Assert.Contains("retry=2", result);
    }

    [Theory]
    [InlineData("Authorization: Custom U0VDUkVULVZBTFVF== retry=2")]
    [InlineData("Authorization: SECRET-BARE-CREDENTIAL")]
    [InlineData("authorization: Custom SECRET-VALUE")]
    public void Authorization_credential_without_a_known_scheme_never_leaks(string text)
    {
        var result = LogRedactor.RedactText(text);

        Assert.DoesNotContain("U0VDUkVULVZBTFVF", result);
        Assert.DoesNotContain("SECRET-BARE-CREDENTIAL", result);
        Assert.DoesNotContain("SECRET-VALUE", result);
    }

    [Theory]
    [InlineData("Authorization: Basic dTpw")]
    [InlineData("Authorization: Basic YQ==")]
    [InlineData("header was Basic Zm9v")]
    public void Short_basic_credentials_are_redacted_too(string text)
    {
        var result = LogRedactor.RedactText(text);

        Assert.DoesNotContain("dTpw", result);
        Assert.DoesNotContain("YQ==", result);
        Assert.DoesNotContain("Zm9v", result);
    }

    [Fact]
    public void Redacted_exception_keeps_type_and_diagnostics_but_not_secrets()
    {
        Exception original;
        try
        {
            throw new InvalidOperationException(
                "diagnostic-marker password=SECRET-OUTER",
                new ArgumentException("token=SECRET-INNER"));
        }
        catch (Exception ex)
        {
            original = ex;
        }

        var safe = LogRedactor.ToSafeException(original);
        var text = safe.ToString();

        Assert.DoesNotContain("SECRET-OUTER", text);
        Assert.DoesNotContain("SECRET-INNER", text);
        Assert.DoesNotContain("SECRET-OUTER", safe.Message);
        Assert.Contains("diagnostic-marker", safe.Message);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("System.ArgumentException", text);
        Assert.Contains(nameof(Redacted_exception_keeps_type_and_diagnostics_but_not_secrets), safe.StackTrace);
        Assert.Equal("System.InvalidOperationException", ((RedactedException)safe).OriginalTypeName);
    }

    [Fact]
    public void Logged_values_never_exceed_the_maximum_length_of_256()
    {
        var result = LogRedactor.RedactText(new string('a', 1000));

        Assert.True(result.Length <= 256, $"Length was {result.Length}");
        Assert.EndsWith("...", result);
        Assert.Equal("short", LogRedactor.RedactText("short"));
    }

    [Fact]
    public void Aggregate_exceptions_have_every_inner_exception_redacted()
    {
        var aggregate = new AggregateException(
            "outer password=SECRET-AGG-OUTER",
            new InvalidOperationException("token=SECRET-AGG-ONE"),
            new ArgumentException("secret=SECRET-AGG-TWO"));

        var safe = LogRedactor.ToSafeException(aggregate);
        var text = safe.ToString();

        foreach (var secret in new[] { "SECRET-AGG-OUTER", "SECRET-AGG-ONE", "SECRET-AGG-TWO" })
        {
            Assert.DoesNotContain(secret, text);
            Assert.DoesNotContain(secret, safe.Message);
        }

        Assert.Contains("System.AggregateException", text);
        Assert.Contains("System.InvalidOperationException", text);
        Assert.Contains("System.ArgumentException", text);
    }
}
