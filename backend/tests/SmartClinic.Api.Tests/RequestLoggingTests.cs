using System.Net;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartClinic.Api.Endpoints;
using SmartClinic.Api.Errors;
using SmartClinic.Api.Logging;
using SmartClinic.Api.OpenApi;
using Xunit;

namespace SmartClinic.Api.Tests;

/// <summary>
/// T-007 acceptance evidence: correlation ID, sanitized request logging,
/// redaction, server-side-only exception detail, and no retained request
/// state. Real in-process HTTP requests through the same wiring as
/// Program.cs (request logging registered BEFORE the T-006 exception
/// handler). The T-006 tests are untouched.
/// </summary>
public sealed class RequestLoggingTests
{
    private const string ExceptionMarker = "diagnostic-exception-marker";
    private const string DiagnosticMarker = "diag-marker-ok";

    private static readonly string MiddlewareCategory = typeof(RequestLoggingMiddleware).FullName!;
    private static readonly string HandlerCategory = typeof(GlobalExceptionHandler).FullName!;

    [Fact]
    public async Task Correlation_id_is_the_same_for_endpoint_logs_the_summary_entry_and_the_response()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/t007/ok");
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var responseId = body.RootElement.GetProperty("correlationId").GetString();

        Assert.False(string.IsNullOrWhiteSpace(responseId));

        var endpointEntry = harness.Logs.Entries.Single(e => e.Category == "T007.Endpoint");
        var summaryEntry = harness.Logs.Entries.Single(e => e.Category == MiddlewareCategory);

        Assert.Equal(responseId, ScopeCorrelationId(endpointEntry));
        Assert.Equal(responseId, ScopeCorrelationId(summaryEntry));
    }

    [Fact]
    public async Task Each_request_gets_its_own_stable_correlation_id()
    {
        using var harness = await BuildHarnessAsync();

        var first = await GetCorrelationIdAsync(harness, "/t007/ok");
        var second = await GetCorrelationIdAsync(harness, "/t007/ok");

        Assert.NotEqual(first, second);

        var summaryIds = harness.Logs.Entries
            .Where(e => e.Category == MiddlewareCategory)
            .Select(ScopeCorrelationId)
            .ToList();
        Assert.Equal(new[] { first, second }, summaryIds);
    }

    [Theory]
    [InlineData("/api/v1/_t006-probe/known-error")]
    [InlineData("/api/v1/_t006-probe/unknown-error")]
    public async Task Error_envelope_correlation_id_matches_the_server_log_correlation_id(string path)
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync(path);
        var envelope = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var envelopeId = envelope.RootElement.GetProperty("correlationId").GetString();

        var handlerEntry = harness.Logs.Entries.Single(e => e.Category == HandlerCategory);
        var summaryEntry = harness.Logs.Entries.Single(e => e.Category == MiddlewareCategory);

        var stateId = handlerEntry.State.Single(kv => kv.Key == "CorrelationId").Value as string;

        Assert.Equal(envelopeId, stateId);
        Assert.Equal(envelopeId, ScopeCorrelationId(handlerEntry));
        Assert.Equal(envelopeId, ScopeCorrelationId(summaryEntry));
    }

    [Fact]
    public async Task Sensitive_authentication_values_never_appear_in_captured_logs_but_diagnostics_do()
    {
        using var harness = await BuildHarnessAsync();

        const string jwt = "eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJzZWNyZXQifQ.c2lnbmF0dXJl";
        var forbidden = new[]
        {
            jwt, "SECRET-COOKIE-VALUE", "SECRET-API-KEY-VALUE", "SECRET-REFRESH-HEADER",
            "SECRET-PASSWORD", "SECRET-ACCESS", "SECRET-REFRESH", "SECRET-TOTP", "SECRET-MFA",
        };

        foreach (var path in new[] { "/t007/ok", "/t007/throw" })
        {
            var request = new HttpRequestMessage(
                HttpMethod.Get,
                path + "?password=SECRET-PASSWORD&access_token=SECRET-ACCESS&refresh_token=SECRET-REFRESH"
                     + "&totp=SECRET-TOTP&mfa_secret=SECRET-MFA&page=2");
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + jwt);
            request.Headers.TryAddWithoutValidation("Cookie", "session=SECRET-COOKIE-VALUE");
            request.Headers.TryAddWithoutValidation("X-Api-Key", "SECRET-API-KEY-VALUE");
            request.Headers.TryAddWithoutValidation("X-Refresh-Token", "SECRET-REFRESH-HEADER");
            request.Headers.TryAddWithoutValidation("User-Agent", "t007-test-agent/1.0");

            await harness.Client.SendAsync(request);
        }

        var entries = harness.Logs.Entries;
        Assert.NotEmpty(entries);

        foreach (var entry in entries)
        {
            var text = ScopeAwareTestLogCapture.AllText(entry);
            foreach (var secret in forbidden)
            {
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            }
        }

        // Ordinary diagnostic information is still available.
        Assert.Contains(entries, e => e.Message.Contains(DiagnosticMarker));

        var okSummary = entries.First(e => e.Category == MiddlewareCategory);
        Assert.Contains("GET", okSummary.Message);
        Assert.Contains("/t007/ok", okSummary.Message);
        Assert.Contains("page=2", okSummary.Message);
        Assert.Contains("200", okSummary.Message);
        Assert.Contains("password=" + LogRedactor.Redacted, okSummary.Message);
        Assert.Contains("User-Agent=t007-test-agent/1.0", okSummary.Message);
    }

    [Fact]
    public async Task Exception_details_stay_in_the_server_log_and_are_not_exposed_by_the_api()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/t007/throw");
        var rawBody = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(rawBody);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("INTERNAL_ERROR", envelope.RootElement.GetProperty("code").GetString());
        foreach (var fragment in new[] { ExceptionMarker, "SELECT", "patient_records", "InvalidOperationException", "StackTrace", "   at " })
        {
            Assert.DoesNotContain(fragment, rawBody, StringComparison.OrdinalIgnoreCase);
        }

        var errorEntry = harness.Logs.Entries.Single(e => e.Level == LogLevel.Error && e.Category == HandlerCategory);
        Assert.NotNull(errorEntry.Exception);
        Assert.Contains(ExceptionMarker, errorEntry.Exception!.Message);
        Assert.Contains("InvalidOperationException", errorEntry.Exception.ToString());
    }

    [Theory]
    [InlineData("/t007/throw-secret")]
    [InlineData("/t007/throw-app-secret")]
    public async Task Secrets_inside_exception_messages_are_redacted_in_logs_while_diagnostics_stay_server_side(string path)
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync(path);
        var rawBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        var secrets = new[] { "SECRET-EXCEPTION-PASSWORD", "SECRET-INNER-TOKEN", "SECRET-APP-PASSWORD" };
        var entries = harness.Logs.Entries;

        foreach (var entry in entries)
        {
            var text = ScopeAwareTestLogCapture.AllText(entry);
            foreach (var secret in secrets)
            {
                Assert.DoesNotContain(secret, text, StringComparison.Ordinal);
            }
        }

        var handlerEntry = entries.Single(e => e.Category == HandlerCategory && e.Exception is not null);
        Assert.IsType<RedactedException>(handlerEntry.Exception);
        Assert.Contains("password=" + LogRedactor.Redacted, handlerEntry.Exception!.ToString());

        if (path == "/t007/throw-secret")
        {
            // Useful, non-sensitive diagnostics remain in the server-side log.
            var exceptionText = handlerEntry.Exception.ToString();
            Assert.Contains(ExceptionMarker, exceptionText);
            Assert.Contains("InvalidOperationException", exceptionText);
            Assert.Contains("ArgumentException", exceptionText);
            Assert.Equal("System.InvalidOperationException", ((RedactedException)handlerEntry.Exception).OriginalTypeName);
        }

        // Nothing about the exception reaches the client.
        var envelope = JsonDocument.Parse(rawBody);
        Assert.Equal("INTERNAL_ERROR", envelope.RootElement.GetProperty("code").GetString());
        foreach (var fragment in new[]
                 {
                     ExceptionMarker, "password", "SECRET", "InvalidOperationException", "ArgumentException",
                     "SecretAppException", "UNREGISTERED_CODE_CANARY", "StackTrace", "   at ",
                 })
        {
            Assert.DoesNotContain(fragment, rawBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Log_state_is_materialized_and_retains_no_request_scoped_objects()
    {
        var harness = await BuildHarnessAsync();

        await harness.Client.GetAsync("/t007/ok?page=1");
        await harness.Client.GetAsync("/api/v1/_t006-probe/known-error");
        await harness.Client.GetAsync("/t007/throw");

        // Dispose the whole app (and with it every request/connection
        // object) BEFORE inspecting what the log provider retained.
        harness.Dispose();

        // Only the logs OWNED by T-006/T-007 (and the explicit test endpoint)
        // are in scope: framework categories (for example routing's
        // EndpointName/RouteEndpoint) are not ours and are left untouched.
        var ownedCategories = new[] { MiddlewareCategory, HandlerCategory, "T007.Endpoint" };
        var entries = harness.Logs.Entries.Where(e => ownedCategories.Contains(e.Category)).ToList();
        Assert.Contains(entries, e => e.Category == MiddlewareCategory);
        Assert.Contains(entries, e => e.Category == HandlerCategory);
        Assert.Contains(entries, e => e.Category == "T007.Endpoint");

        foreach (var entry in entries)
        {
            foreach (var kv in entry.State.Concat(entry.ScopeValues))
            {
                Assert.False(IsRequestScoped(kv.Value), $"Log state '{kv.Key}' retained a request-scoped {kv.Value?.GetType().Name}.");
                Assert.True(
                    kv.Value is null || kv.Value is string || kv.Value.GetType().IsPrimitive,
                    $"Log state '{kv.Key}' is a {kv.Value?.GetType().Name}, expected a plain string/number.");
            }

            // Formatting after disposal must not throw (no disposed-object access).
            var ex = Record.Exception(() => ScopeAwareTestLogCapture.AllText(entry));
            Assert.Null(ex);
        }
    }

    // ---- helpers ------------------------------------------------------

    private static bool IsRequestScoped(object? value) =>
        value is HttpContext or HttpRequest or HttpResponse or IHeaderDictionary or IQueryCollection
            or ClaimsPrincipal or Stream or ConnectionInfo;

    private static string? ScopeCorrelationId(ScopeAwareTestLogCapture.Entry entry) =>
        entry.ScopeValues
            .Where(kv => kv.Key == RequestCorrelation.LogPropertyName)
            .Select(kv => kv.Value as string)
            .Distinct()
            .Single();

    private static async Task<string?> GetCorrelationIdAsync(TestHarness harness, string path)
    {
        var response = await harness.Client.GetAsync(path);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("correlationId").GetString();
    }

    // AppException with an unregistered code and a credential-like message.
    private sealed class SecretAppException : SmartClinic.Application.Errors.AppException
    {
        public SecretAppException()
            : base("UNREGISTERED_CODE_CANARY", "password=SECRET-APP-PASSWORD")
        {
        }
    }

    private sealed class TestHarness : IDisposable
    {
        public required WebApplication App { get; init; }
        public required HttpClient Client { get; init; }
        public required ScopeAwareTestLogCapture Logs { get; init; }

        public void Dispose()
        {
            Client.Dispose();
            App.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static async Task<TestHarness> BuildHarnessAsync()
    {
        var logs = new ScopeAwareTestLogCapture();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Logging.AddSmartClinicLogging();

        builder.Services.AddSmartClinicOpenApi();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var app = builder.Build();

        // Same order as Program.cs: T-007 request logging wraps the T-006 handler.
        app.UseSmartClinicRequestLogging();
        app.UseExceptionHandler(errorApp => errorApp.Run(async httpContext =>
        {
            var (status, message) = ErrorCatalog.Resolve(ErrorCatalog.InternalError);
            await ErrorEnvelopeWriter.WriteAsync(httpContext, status, ErrorCatalog.InternalError, message, fields: null);
        }));
        app.UseStatusCodePages(async statusCodeContext =>
        {
            var httpContext = statusCodeContext.HttpContext;
            var status = (HttpStatusCode)httpContext.Response.StatusCode;
            var code = ErrorCatalog.DefaultCodeForStatus(status);
            var (_, message) = ErrorCatalog.Resolve(code);
            await ErrorEnvelopeWriter.WriteAsync(httpContext, status, code, message, fields: null);
        });

        app.UseSmartClinicOpenApi();
        app.MapErrorHandlingProbeEndpoints();

        app.MapGet("/t007/ok", (HttpContext context, ILoggerFactory loggerFactory) =>
        {
            loggerFactory.CreateLogger("T007.Endpoint").LogInformation("Ordinary diagnostic entry {Marker}", DiagnosticMarker);
            return Results.Ok(new { correlationId = context.TraceIdentifier });
        });

        app.MapGet("/t007/throw", (ILoggerFactory loggerFactory) =>
        {
            loggerFactory.CreateLogger("T007.Endpoint").LogInformation("Ordinary diagnostic entry {Marker}", DiagnosticMarker);
            throw new InvalidOperationException(ExceptionMarker + " SELECT * FROM patient_records");
        });

        app.MapGet("/t007/throw-secret", () =>
        {
            throw new InvalidOperationException(
                ExceptionMarker + " password=SECRET-EXCEPTION-PASSWORD",
                new ArgumentException("token=SECRET-INNER-TOKEN"));
        });

        app.MapGet("/t007/throw-app-secret", () =>
        {
            throw new SecretAppException();
        });

        await app.StartAsync();

        return new TestHarness { App = app, Client = app.GetTestClient(), Logs = logs };
    }
}
