using SmartClinic.Application.Configuration;
using System.Threading.Tasks;
using Xunit;

namespace SmartClinic.Configuration.Tests;

public sealed class AuditValueRedactorTests
{
    [Theory]
    [InlineData("password", "plain-secret")]
    [InlineData("smtp.access_token", "plain-secret")]
    [InlineData("api-key", "plain-secret")]
    public void Sensitive_configuration_values_are_fully_redacted(string key, string value)
    {
        AssertRedactedValue(value, AuditValueRedactor.Redact(key, value));
    }

    [Fact]
    public void Credential_pairs_and_bearer_tokens_inside_free_text_are_redacted()
    {
        var result = AuditValueRedactor.Redact(
            "setting",
            "user=alice password=hunter2 refresh_token=refresh-secret Authorization: Bearer access-secret");

        Assert.Equal(AuditValueRedactor.Redacted, result);
    }

    [Fact]
    public void Quoted_secret_values_with_spaces_are_fully_redacted()
    {
        var result = AuditValueRedactor.Redact("setting", "password=\"two word secret\"");

        Assert.Equal(AuditValueRedactor.Redacted, result);
    }

    [Fact]
    public void Unquoted_secret_values_with_spaces_and_bearer_prefixes_are_fully_redacted()
    {
        var result = AuditValueRedactor.Redact(
            "setting",
            "password=two words here; token=Bearer abc123xyz; refresh_token: abc def");

        Assert.Equal(AuditValueRedactor.Redacted, result);
    }

    [Theory]
    [InlineData("(password=abc123)", "abc123")]
    [InlineData("?token=abc123&u=1", "abc123")]
    [InlineData("&password=abc123", "abc123")]
    [InlineData("pwd=abc123", "abc123")]
    [InlineData("token2=abc123", "abc123")]
    [InlineData("password1=abc123", "abc123")]
    public void Credential_pairs_are_redacted_at_common_text_boundaries(string input, string secret)
    {
        Assert.DoesNotContain(secret, AuditValueRedactor.Redact("setting", input));
    }

    [Fact]
    public void Key_value_text_inside_a_quoted_secret_is_redacted_with_the_entire_value()
    {
        var result = AuditValueRedactor.Redact("setting", "password=\"correct horse key=battery\"");

        Assert.Equal(AuditValueRedactor.Redacted, result);
    }

    [Theory]
    [InlineData("mfa.required", "true")]
    public void Names_without_credential_markers_are_not_redacted(string key, string value)
    {
        Assert.Equal(value, AuditValueRedactor.Redact(key, value));
    }

    [Theory]
    [InlineData("password_confirmation", "hunter2")]
    [InlineData("secret_key", "abc123")]
    [InlineData("AWS_SECRET_ACCESS_KEY", "abc123")]
    [InlineData("token_value", "abc123")]
    [InlineData("password_hash", "hash-value")]
    [InlineData("password_old", "old-value")]
    [InlineData("db_password_plain", "plain-value")]
    [InlineData("passwords", "hunter2")]
    [InlineData("credentials", "credential-value")]
    public void Credential_markers_anywhere_in_a_name_redact_its_value(string key, string value)
    {
        AssertRedactedValue(value, AuditValueRedactor.Redact(key, value));
    }

    [Theory]
    [InlineData("api_key_value", "key-value")]
    [InlineData("api_key_header", "key-header")]
    [InlineData("x-api-key-prod", "prod-key")]
    [InlineData("private_key_pem", "private-key")]
    [InlineData("session_id_value", "session-value")]
    [InlineData("apikey", "key-value")]
    [InlineData("privatekey", "private-key")]
    [InlineData("sessionid", "session-value")]
    [InlineData("SESSIONID", "session-value")]
    [InlineData("accesstoken", "access-value")]
    [InlineData("ACCESSTOKEN", "access-value")]
    [InlineData("refreshtoken", "refresh-value")]
    [InlineData("clientsecret", "client-secret")]
    [InlineData("clienttoken", "client-token")]
    [InlineData("totpsecret", "totp-secret")]
    [InlineData("api_key_staging", "staging-key")]
    [InlineData("api_key_dev", "dev-key")]
    [InlineData("api_key_live", "live-key")]
    [InlineData("stripe.api.key.live", "live-key")]
    [InlineData("x-api-key-staging", "staging-key")]
    [InlineData("api_key_plain", "plain-key")]
    [InlineData("api_key_raw", "raw-key")]
    [InlineData("api_key_encrypted", "encrypted-key")]
    [InlineData("api_key_new", "new-key")]
    [InlineData("api_key_old", "old-key")]
    [InlineData("api_key_backup", "backup-key")]
    [InlineData("google_api_key_backend", "backend-key")]
    [InlineData("private_key_content", "private-content")]
    [InlineData("private_key_data", "private-data")]
    [InlineData("private_key_base64", "encoded-key")]
    [InlineData("private_key_der", "der-key")]
    [InlineData("session_id_new", "new-session")]
    public void Sensitive_name_pairs_with_suffixes_redact_the_value(string key, string value)
    {
        AssertRedactedValue(value, AuditValueRedactor.Redact(key, value));
    }

    [Theory]
    [InlineData("api.key.rotation.days", "30")]
    [InlineData("private.key.path", "/keys/private.pem")]
    [InlineData("session.id.length", "32")]
    [InlineData("api_key_id", "primary")]
    [InlineData("password.min_length", "12")]
    [InlineData("passwordMinLength", "12")]
    [InlineData("token.expiry.minutes", "30")]
    [InlineData("api.key.header", "X-Api-Key")]
    [InlineData("Smtp:Password", "mail-secret")]
    [InlineData("Jwt:Secret", "jwt-secret")]
    [InlineData("ConnectionStrings:Password", "db-secret")]
    [InlineData("SMTPPassword", "mail-secret")]
    [InlineData("DBPassword", "db-secret")]
    [InlineData("JWTSecret", "jwt-secret")]
    [InlineData("JWTSecretKeyBase", "jwt-secret")]
    [InlineData("HTTPAuthorization", "bearer-secret")]
    [InlineData("SMTPPasswordHash", "mail-secret")]
    [InlineData("DBPasswordConfirmation", "db-secret")]
    [InlineData("dbpassword", "db-secret")]
    [InlineData("smtppassword", "mail-secret")]
    [InlineData("newpassword", "new-secret")]
    [InlineData("userpassword", "user-secret")]
    [InlineData("secretkey", "secret-value")]
    [InlineData("SECRETKEY", "secret-value")]
    [InlineData("apisecret", "api-secret")]
    [InlineData("appsecret", "app-secret")]
    [InlineData("authtoken", "auth-token")]
    [InlineData("idtoken", "id-token")]
    [InlineData("bearertoken", "bearer-token")]
    [InlineData("passwordhash", "hash-value")]
    [InlineData("passwordconfirmation", "password-value")]
    [InlineData("tokenvalue", "token-value")]
    [InlineData("secretvalue", "secret-value")]
    [InlineData("apikeyvalue", "api-key")]
    [InlineData("dbpasswordhash", "db-hash")]
    [InlineData("dbpasswords", "db-passwords")]
    [InlineData("apisecrets", "api-secrets")]
    [InlineData("pass", "pass-value")]
    [InlineData("pw", "pw-value")]
    [InlineData("passwordless", "enabled")]
    public void Names_with_credential_markers_are_redacted_even_when_policy_like(string key, string value)
    {
        AssertRedactedValue(value, AuditValueRedactor.Redact(key, value));
    }

    [Fact]
    public void Colon_separated_credential_pairs_inside_values_are_redacted()
    {
        var result = AuditValueRedactor.Redact("setting", "Smtp:Password=mail-secret; Jwt:Secret=jwt-secret");

        Assert.DoesNotContain("mail-secret", result);
        Assert.DoesNotContain("jwt-secret", result);
    }

    [Theory]
    [InlineData("bypass", "true")]
    [InlineData("footprint", "5")]
    [InlineData("cache.bypass", "true")]
    [InlineData("ssl.bypass", "true")]
    [InlineData("compass", "enabled")]
    [InlineData("trespass", "enabled")]
    [InlineData("overpass", "enabled")]
    [InlineData("spin", "enabled")]
    [InlineData("dbpass", "db-secret")]
    [InlineData("userpass", "user-secret")]
    [InlineData("adminpass", "admin-secret")]
    [InlineData("rootpass", "root-secret")]
    [InlineData("smtppass", "mail-secret")]
    [InlineData("wifipass", "wifi-secret")]
    [InlineData("storepass", "store-secret")]
    [InlineData("keypass", "key-secret")]
    [InlineData("dbpwd", "db-secret")]
    [InlineData("userpwd", "user-secret")]
    [InlineData("adminpw", "admin-secret")]
    [InlineData("smtppw", "mail-secret")]
    [InlineData("mypin", "1234")]
    [InlineData("bankpin", "1234")]
    [InlineData("smtppin", "1234")]
    [InlineData("appotp", "123456")]
    [InlineData("secretaries", "contacts")]
    [InlineData("secretariat", "office")]
    [InlineData("secretion", "value")]
    [InlineData("tokenizer", "setting")]
    [InlineData("cookiejar", "setting")]
    [InlineData("passport", "number")]
    [InlineData("passenger", "count")]
    [InlineData("passwordless", "enabled")]
    [InlineData("secretary.email", "contact")]
    public void Any_credential_marker_substring_redacts_the_entire_value(string key, string value)
    {
        AssertRedactedValue(value, AuditValueRedactor.Redact(key, value));
    }

    [Theory]
    [InlineData("jwt_key", "neutral")]
    [InlineData("JwtKey", "neutral")]
    public void Jwt_key_names_are_sensitive_even_when_the_value_has_no_marker(string key, string value)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact(key, value));
    }

    [Fact]
    public void Jwt_key_pairs_in_text_are_redacted_without_marker_in_the_value()
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", "jwt_key=neutral"));
    }

    [Fact]
    public void Access_token_pair_redaction_preserves_unrelated_text_without_the_token_value()
    {
        Assert.Equal(
            "status=active access_token=[REDACTED]",
            AuditValueRedactor.Redact("setting", "status=active access_token=new-secret"));
    }

    [Theory]
    [InlineData("status=active access_token=new-secret password=other-secret")]
    [InlineData("status=active access_token=\"new-secret\"")]
    public void Access_token_pair_falls_back_to_full_redaction_when_other_sensitive_content_is_present(string value)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Theory]
    [InlineData("password=\"abc token=longsecretvalue123\"", "[REDACTED]")]
    [InlineData("password=\"my token=abc\"", "[REDACTED]")]
    public void Nested_sensitive_pairs_inside_a_quoted_secret_do_not_overlap_replacements(string input, string expected)
    {
        Assert.Equal(expected, AuditValueRedactor.Redact("setting", input));
    }

    [Theory]
    [InlineData("Reason: password=abc123")]
    [InlineData("user: token=longsecretvalue")]
    [InlineData("note=\"password=abc\"")]
    [InlineData("{\"conn\":\"password=abc;host=x\"}")]
    [InlineData("[\"token=abc\"]")]
    [InlineData("user[password]=abc&x=1")]
    [InlineData("password=abc,def")]
    [InlineData("api key=abc")]
    [InlineData("secret key=abc")]
    [InlineData("private key=-----BEGIN RSA PRIVATE KEY-----")]
    [InlineData("session id=abc")]
    [InlineData("Cookie: sid=abc; session=xyz")]
    [InlineData("{\"tokens\":[\"a\",\"b\"]}")]
    [InlineData("private_key=-----BEGIN PRIVATE KEY-----\nMIIEsecret\nmore-secret")]
    [InlineData("signing_key=neutral")]
    [InlineData("encryption_key=neutral")]
    [InlineData("master_key=neutral")]
    [InlineData("hmac_key=neutral")]
    [InlineData("ssh_key=neutral")]
    [InlineData("jwt_key=neutral")]
    [InlineData("recovery_code=neutral")]
    [InlineData("verification code: 482913")]
    [InlineData("2fa code=482913")]
    [InlineData("security code: 4471")]
    [InlineData("recovery key: ABCD-EFGH")]
    [InlineData("mfa code: 482913")]
    [InlineData("reset code: 9921")]
    [InlineData("login code: 482913")]
    [InlineData("backup codes: 1234 5678")]
    [InlineData("confirmation code: 482913")]
    [InlineData("access code: 4471")]
    [InlineData("activation code: ABCD1234")]
    [InlineData("one time code: 482913")]
    [InlineData("myapi key: abc")]
    [InlineData("webapi key: abc")]
    [InlineData(":hunter2@host/0")]
    [InlineData("postgres://app:pa/x9@db")]
    public void Sensitive_pairs_are_redacted_across_text_boundaries(string input)
    {
        AssertRedactedValue(input, AuditValueRedactor.Redact("setting", input));
    }

    [Theory]
    [InlineData("api - key=abc")]
    [InlineData("private / key: abc")]
    public void Credential_pairs_with_intervening_punctuation_are_redacted(string input)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", input));
    }

    [Theory]
    [InlineData("api keys: sk_live_x")]
    [InlineData("ssh keys: AAAAB3Nza")]
    [InlineData("recovery codes: 8812 4471")]
    [InlineData("api key1=abc")]
    public void Credential_name_suffixes_are_redacted(string input)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", input));
    }

    [Theory]
    [InlineData("api key value=sk_live_51Habc")]
    [InlineData("Rotated api key to sk_live_51Habc")]
    [InlineData("private key (base64): MIIE")]
    [InlineData("api key -> sk_live_51Habc")]
    [InlineData("Storage:AccountKey=Zm9vYmFy")]
    [InlineData("ServiceBus:SharedAccessKey=abc")]
    [InlineData("AccountKey=Zm9vYmFy")]
    [InlineData("?sv=2022&sig=abc%3D&se=2030")]
    [InlineData("authkey=abc")]
    [InlineData("accesskey=abc")]
    [InlineData("licensekey=abc")]
    [InlineData("client_key=abc")]
    [InlineData("Basic YWxpY2U6aHVudGVy")]
    [InlineData("AKIA1234567890ABCDEF")]
    [InlineData("ghp_123456789012345678901234567890123456")]
    [InlineData("sk_live_51Habc123456789")]
    [InlineData("xoxb-123456789012-abcdefghijkl")]
    [InlineData("sk-ant-api03-AbCdEfGh12345678")]
    [InlineData("AIzaSyDemo1234567890abcdefghijklmnop")]
    [InlineData("sk-proj-AbCdEfGhIjKl1234")]
    [InlineData("whsec_AbCdEfGh1234")]
    [InlineData("glpat-AbCdEfGhIjKlMnOpQrSt")]
    [InlineData("rk_live_AbCdEfGh1234")]
    [InlineData("//alice:hunter2@host/db")]
    [InlineData("alice:hunter2@host:5432")]
    public void Common_secret_aliases_and_formats_are_redacted(string input)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", input));
    }

    [Theory]
    [InlineData("كلمة المرور: Hunter2!")]
    [InlineData("الرقم السري: 1234")]
    [InlineData("رمز التحقق: 123456")]
    [InlineData("مفتاح الحساب: abc123")]
    [InlineData("كَلِمَةُ المُرُورِ: x")]
    [InlineData("كلمه المرور: x")]
    [InlineData("كلمة مرور: x")]
    [InlineData("الرقم السرى: 1234")]
    [InlineData("رقم سري: 1234")]
    [InlineData("كلمـــة المرور: x")]
    [InlineData("الباسوورد: x")]
    [InlineData("باسورد: Hunter2")]
    [InlineData("توكن: abc123")]
    public void Arabic_credential_terms_are_redacted(string input)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", input));
    }

    [Fact]
    public void Verification_code_field_name_is_sensitive()
    {
        AssertRedactedValue("482913", AuditValueRedactor.Redact("VerificationCode", "482913"));
    }

    [Theory]
    [InlineData("MfaCode")]
    [InlineData("ResetCode")]
    [InlineData("LoginCode")]
    [InlineData("BackupCodes")]
    [InlineData("ConfirmationCode")]
    public void Verification_code_aliases_are_sensitive_field_names(string fieldName)
    {
        AssertRedactedValue("482913", AuditValueRedactor.Redact(fieldName, "482913"));
    }

    [Theory]
    [InlineData("كلمه المرور")]
    [InlineData("الرقم السرى")]
    [InlineData("كلمـــة المرور")]
    public void Arabic_credential_field_names_are_normalized(string fieldName)
    {
        Assert.Equal("\"[REDACTED]\"", AuditValueRedactor.Redact(fieldName, "1234"));
    }

    [Fact]
    public void Redacting_a_nested_pair_returns_valid_redacted_json()
    {
        var result = AuditValueRedactor.Redact("setting", "{\"note\":\"login password=abc\",\"ok\":true}");

        Assert.Equal("\"[REDACTED]\"", result);
    }

    [Fact]
    public void Redacting_a_sensitive_json_array_returns_valid_redacted_json()
    {
        var result = AuditValueRedactor.Redact("setting", "{\"tokens\":[\"a\",\"b\"]}");

        Assert.Equal("\"[REDACTED]\"", result);
    }

    [Fact]
    public void Redacting_a_sensitive_root_json_array_returns_valid_json()
    {
        const string value = "[\"password\",\"hunter2\"]";
        var result = AuditValueRedactor.Redact("setting", value);

        Assert.Equal("\"[REDACTED]\"", result);
        using var document = System.Text.Json.JsonDocument.Parse(result!);
        Assert.Equal(AuditValueRedactor.Redacted, document.RootElement.GetString());
    }

    [Fact]
    public void Uri_passwords_and_private_key_pem_blocks_are_redacted()
    {
        var value = "postgres://app:hunter2@db/x -----BEGIN RSA PRIVATE KEY-----\nMIIEsecret\n-----END RSA PRIVATE KEY-----";
        var result = AuditValueRedactor.Redact("setting", value);

        Assert.DoesNotContain("hunter2", result);
        Assert.DoesNotContain("MIIEsecret", result);
    }

    [Fact]
    public void A_private_key_after_a_certificate_pem_block_is_redacted()
    {
        const string value = "-----BEGIN CERTIFICATE-----\nMIIB\n-----END CERTIFICATE-----\n" +
            "-----BEGIN PRIVATE KEY-----\nMIIE\n-----END PRIVATE KEY-----";

        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Theory]
    [InlineData("redis://:hunter2@host/0")]
    [InlineData("https://0123abcd@sentry.example/1")]
    [InlineData("connect to alice:hunter2@host:5432")]
    public void Uri_userinfo_is_redacted_even_without_a_username_password_pair(string value)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Theory]
    [InlineData("Authorization: Basic YWxpY2U6aHVudGVyMg==")]
    [InlineData("Authorization: Bearer abc==")]
    [InlineData("token: YWxp==")]
    [InlineData("password=it's-secret")]
    [InlineData("{\"note\":\"login password=ab\\\"c\",\"ok\":true}")]
    [InlineData("{\"password\":123}")]
    [InlineData("{\"pin\":true,\"ok\":1}")]
    [InlineData("{\"payload\":\"{\\\"password\\\":\\\"abc\\\"}\"}")]
    [InlineData("redis://:secret@host")]
    [InlineData("https://TOKEN@github.com/x")]
    [InlineData("postgres://app:p@ss@db")]
    [InlineData("data=password=abc")]
    [InlineData("AWS secret access key=abc")]
    public void Reviewer_reported_secret_shapes_are_redacted_without_partial_output(string input)
    {
        var result = AuditValueRedactor.Redact("setting", input);

        if (input.StartsWith('{') || input.StartsWith('['))
        {
            using var document = System.Text.Json.JsonDocument.Parse(result!);
            Assert.Equal(System.Text.Json.JsonValueKind.String, document.RootElement.ValueKind);
            Assert.Equal(AuditValueRedactor.Redacted, document.RootElement.GetString());
        }
        else
        {
            Assert.Equal(AuditValueRedactor.Redacted, result);
        }
    }

    [Theory]
    [InlineData("password=\"unclosed-secret", "[REDACTED]")]
    [InlineData("password=\"escaped \\\"secret\\\" value\"", "[REDACTED]")]
    public void Quoted_values_with_unclosed_or_escaped_quotes_are_redacted(string input, string expected)
    {
        Assert.Equal(expected, AuditValueRedactor.Redact("setting", input));
    }

    [Fact]
    public void Standalone_bearer_tokens_and_jwts_are_redacted()
    {
        var result = AuditValueRedactor.Redact("setting", "Bearer abc123xyz eyJhbGciOiJIUzI1Ni.eyJzdWIiOiIxMjM.abc123signature");

        Assert.DoesNotContain("abc123xyz", result);
        Assert.DoesNotContain("eyJhbGciOiJIUzI1Ni", result);
    }

    [Fact]
    public void Null_values_remain_null()
    {
        Assert.Null(AuditValueRedactor.Redact("password", null));
    }

    [Fact]
    public void Unpaired_surrogate_in_sensitive_value_is_redacted_without_throwing()
    {
        var value = "password=" + new string('\uD800', 1);

        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Fact]
    public void Deep_json_redaction_keeps_a_valid_json_string_result()
    {
        var deepJson = new string('[', 100) + new string(']', 100);
        var result = AuditValueRedactor.Redact("password", deepJson);

        using var document = System.Text.Json.JsonDocument.Parse(result!);
        Assert.Equal(AuditValueRedactor.Redacted, document.RootElement.GetString());
    }

    [Fact]
    public void Very_long_values_with_many_separators_complete_without_quadratic_backtracking()
    {
        var value = new string(':', 200_000);

        Assert.Equal(value, AuditValueRedactor.Redact("setting", value));
    }

    [Fact(Timeout = 15000)]
    public async Task Long_values_with_many_words_and_a_single_long_word_complete()
    {
        var manyWords = string.Join(' ', Enumerable.Repeat("ordinary", 50_000));
        var oneLongWord = new string('x', 200_000);

        await Task.Run(() =>
        {
            Assert.Equal(manyWords, AuditValueRedactor.Redact("setting", manyWords));
            Assert.Equal(oneLongWord, AuditValueRedactor.Redact("setting", oneLongWord));
        });
    }

    [Fact]
    public void Reversed_unpaired_surrogates_are_redacted_without_throwing()
    {
        var value = "password=" + new string(new[] { '\uDC00', '\uD800' });

        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Theory]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9")]
    [InlineData("x_eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9")]
    [InlineData("eyJhbGciOiJub25lIn0.eyJzdWIiOiJhZG1pbiJ9.")]
    public void Unsigned_jwts_are_redacted(string token)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", token));
    }

    [Theory]
    [InlineData("Group work")]
    [InlineData("Stop in")]
    [InlineData("Was identical")]
    [InlineData("Not play")]
    [InlineData("Professional")]
    [InlineData("design")]
    public void Ordinary_phrases_without_a_compound_credential_marker_are_preserved(string value)
    {
        Assert.Equal(value, AuditValueRedactor.Redact("setting", value));
    }

    [Fact]
    public void Compound_credential_marker_prefixes_are_detected_across_words()
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", "to kenya"));
    }

    [Fact]
    public void Formatting_only_words_do_not_break_a_compound_credential_match()
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", "api \u0301 \u0301 key: neutral"));
    }

    [Theory]
    [InlineData("Possession")]
    [InlineData("Shipping")]
    [InlineData("Spine")]
    [InlineData("President")]
    [InlineData("Inside")]
    [InlineData("Cookies")]
    [InlineData("Passed")]
    [InlineData("Bearer of")]
    [InlineData("Bearer ab")]
    [InlineData("Bearer:abc")]
    [InlineData("bearer=abc")]
    public void Credential_marker_substrings_in_values_are_intentionally_redacted(string value)
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", value));
    }

    [Theory]
    [InlineData("\"ordinary text\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData(" 42 ")]
    public void Sensitive_json_scalars_are_replaced_with_a_valid_json_string(string value)
    {
        var result = AuditValueRedactor.Redact("password", value);

        Assert.Equal("\"[REDACTED]\"", result);
        using var document = System.Text.Json.JsonDocument.Parse(result!);
        Assert.Equal(AuditValueRedactor.Redacted, document.RootElement.GetString());
    }

    [Fact]
    public void Non_sensitive_values_are_preserved()
    {
        Assert.Equal("clinic display name", AuditValueRedactor.Redact("clinic.name", "clinic display name"));
    }

    [Fact]
    public void Redacted_value_is_idempotent_with_or_without_a_sensitive_field_name()
    {
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("password", AuditValueRedactor.Redacted));
        Assert.Equal(AuditValueRedactor.Redacted, AuditValueRedactor.Redact("setting", AuditValueRedactor.Redacted));
    }

    [Theory]
    [InlineData("[invalid json with password]")]
    [InlineData("{invalid password}")]
    [InlineData("\"unclosed password")]
    public void Json_like_invalid_redacted_values_have_a_valid_json_string_shape(string input)
    {
        Assert.Equal("\"[REDACTED]\"", AuditValueRedactor.Redact("setting", input));
    }

    [Fact]
    public void Non_sensitive_json_values_are_preserved()
    {
        const string value = "{\"a\":1}";

        Assert.Equal(value, AuditValueRedactor.Redact("clinic.name", value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Empty_or_missing_field_names_do_not_redact_non_sensitive_values(string? fieldName)
    {
        const string value = "ordinary value";

        Assert.Equal(value, AuditValueRedactor.Redact(fieldName, value));
    }

    private static void AssertRedactedValue(string originalValue, string? actualValue)
    {
        var originalIsJson = true;
        try
        {
            using var _ = System.Text.Json.JsonDocument.Parse(originalValue);
        }
        catch (System.Text.Json.JsonException)
        {
            originalIsJson = false;
        }

        if (!originalIsJson)
        {
            Assert.Equal(AuditValueRedactor.Redacted, actualValue);
            return;
        }

        using var redactedDocument = System.Text.Json.JsonDocument.Parse(actualValue!);
        Assert.Equal(System.Text.Json.JsonValueKind.String, redactedDocument.RootElement.ValueKind);
        Assert.Equal(AuditValueRedactor.Redacted, redactedDocument.RootElement.GetString());
    }
}
