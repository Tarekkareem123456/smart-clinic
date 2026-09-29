
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SmartClinic.Api.Endpoints;
using SmartClinic.Api.Errors;
using SmartClinic.Api.OpenApi;
using Xunit;

namespace SmartClinic.Api.Tests;

/// <summary>
/// T-006 acceptance evidence. Sends REAL in-process HTTP requests (via
/// Microsoft.AspNetCore.TestHost — no Docker, no real socket) through the
/// SAME wiring Program.cs uses (AddSmartClinicOpenApi, AddExceptionHandler
/// &lt;GlobalExceptionHandler&gt;, UseExceptionHandler, UseStatusCodePages,
/// the T-006 probe endpoints), so these tests exercise the real mechanism.
/// </summary>
public sealed class ErrorEnvelopeTests
{
    private static readonly string[] AllFiveEnvelopeKeys = { "status", "code", "message", "fields", "correlationId" };

    [Fact]
    public async Task Known_application_error_returns_the_full_envelope_with_correct_status_and_code()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/known-error");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal(404, envelope.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("NOT_FOUND", envelope.RootElement.GetProperty("code").GetString());
        Assert.Equal(0, envelope.RootElement.GetProperty("fields").GetArrayLength());
        Assert.NotEmpty(envelope.RootElement.GetProperty("correlationId").GetString()!);
    }

    [Fact]
    public async Task Validation_error_returns_400_with_populated_fields()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.PostAsJsonAsync("/api/v1/_t006-probe/validation-error", new { });
        var envelope = await ReadEnvelopeAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal("VALIDATION_FAILED", envelope.RootElement.GetProperty("code").GetString());

        var fields = envelope.RootElement.GetProperty("fields");
        Assert.True(fields.GetArrayLength() > 0);
        var first = fields[0];
        Assert.Equal("Name", first.GetProperty("name").GetString());
        Assert.Equal("REQUIRED", first.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Known_application_error_with_unsafe_internal_message_never_leaks_it()
    {
        // Exercises the KNOWN-AppException path (ConflictAppException),
        // NOT the unknown-exception path — that path is covered separately
        // by Unknown_exception_returns_safe_generic_500_with_no_internal_details_leaked.
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/known-error-with-unsafe-message");
        var rawBody = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(rawBody);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal("CONFLICT", envelope.RootElement.GetProperty("code").GetString());

        // The response message must be EXACTLY the safe catalog default —
        // not the exception's own (deliberately unsafe) Message.
        Assert.Equal(
            "The request could not be completed due to a conflict.",
            envelope.RootElement.GetProperty("message").GetString());

        // None of the exception's real (unsafe) message text may appear
        // anywhere in the raw response body.
        var forbiddenFragments = new[]
        {
            "duplicate key", "patient_records_ssn_key", "ConflictCanary", "unique constraint",
        };
        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, rawBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Unknown_exception_returns_safe_generic_500_with_no_internal_details_leaked()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/unknown-error");
        var rawBody = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(rawBody);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal("INTERNAL_ERROR", envelope.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "An unexpected error occurred. Please try again.",
            envelope.RootElement.GetProperty("message").GetString());

        // The probe's real exception message contains all of these; NONE
        // of them may appear anywhere in the raw response body.
        var forbiddenFragments = new[]
        {
            "SELECT", "patient_records", "StackTraceCanary", "InvalidOperationException",
            "System.", "   at ", "StackTrace", "ssn",
        };
        foreach (var fragment in forbiddenFragments)
        {
            Assert.DoesNotContain(fragment, rawBody, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Unregistered_app_exception_code_is_never_emitted_and_yields_internal_error_envelope()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/unregistered-code-error");
        var rawBody = await response.Content.ReadAsStringAsync();
        var envelope = JsonDocument.Parse(rawBody);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal(500, envelope.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("INTERNAL_ERROR", envelope.RootElement.GetProperty("code").GetString());
        Assert.Equal(
            "An unexpected error occurred. Please try again.",
            envelope.RootElement.GetProperty("message").GetString());
        Assert.Equal(0, envelope.RootElement.GetProperty("fields").GetArrayLength());

        // Neither the unregistered code nor the exception's message may
        // appear anywhere in the raw response body.
        Assert.DoesNotContain("UNREGISTERED_CODE_CANARY", rawBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnregisteredMessageCanary", rawBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unmatched_route_still_returns_the_same_envelope_shape()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/this-path-does-not-exist");
        var envelope = await ReadEnvelopeAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        Assert.Equal("NOT_FOUND", envelope.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CorrelationId_in_the_response_matches_the_correlationId_in_the_server_log_entry()
    {
        using var harness = await BuildHarnessAsync();

        var response = await harness.Client.GetAsync("/api/v1/_t006-probe/known-error");
        var envelope = await ReadEnvelopeAsync(response);
        var responseCorrelationId = envelope.RootElement.GetProperty("correlationId").GetString();

        var loggedCorrelationIds = harness.Logs.Entries
            .SelectMany(e => e.State)
            .Where(kv => kv.Key == "CorrelationId")
            .Select(kv => kv.Value?.ToString())
            .ToList();

        Assert.Contains(responseCorrelationId, loggedCorrelationIds);
    }

    [Fact]
    public async Task All_error_producing_probes_share_the_exact_same_envelope_shape()
    {
        using var harness = await BuildHarnessAsync();

        var requests = new (HttpMethod Method, string Path)[]
        {
            (HttpMethod.Get, "/api/v1/_t006-probe/known-error"),
            (HttpMethod.Get, "/api/v1/_t006-probe/known-error-with-unsafe-message"),
            (HttpMethod.Post, "/api/v1/_t006-probe/validation-error"),
            (HttpMethod.Get, "/api/v1/_t006-probe/unknown-error"),
            (HttpMethod.Get, "/api/v1/_t006-probe/no-such-path"),
        };

        foreach (var (method, path) in requests)
        {
            var httpResponse = method == HttpMethod.Get
                ? await harness.Client.GetAsync(path)
                : await harness.Client.PostAsJsonAsync(path, new { });

            var envelope = await ReadEnvelopeAsync(httpResponse);
            AssertHasExactlyTheFiveEnvelopeKeys(envelope);
        }
    }

    // ---- helpers ------------------------------------------------------

    private static void AssertHasExactlyTheFiveEnvelopeKeys(JsonDocument envelope)
    {
        var actualKeys = envelope.RootElement.EnumerateObject().Select(p => p.Name).ToHashSet();
        Assert.Equal(AllFiveEnvelopeKeys.ToHashSet(), actualKeys);
    }

    private static async Task<JsonDocument> ReadEnvelopeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(body);
    }

    private sealed class TestHarness : IDisposable
    {
        public required WebApplication App { get; init; }
        public required HttpClient Client { get; init; }
        public required TestLogCapture Logs { get; init; }

        public void Dispose()
        {
            Client.Dispose();
            App.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }

    private static async Task<TestHarness> BuildHarnessAsync()
    {
        var logs = new TestLogCapture();

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);

        builder.Services.AddSmartClinicOpenApi();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

        var app = builder.Build();

        app.UseExceptionHandler(errorApp => errorApp.Run(async httpContext =>
        {
            var (fallbackStatus, fallbackMessage) = ErrorCatalog.Resolve(ErrorCatalog.InternalError);
            await ErrorEnvelopeWriter.WriteAsync(
                httpContext, fallbackStatus, ErrorCatalog.InternalError, fallbackMessage, fields: null);
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
        app.MapPipelineProbeEndpoints();
        app.MapErrorHandlingProbeEndpoints();

        await app.StartAsync();

        return new TestHarness { App = app, Client = app.GetTestClient(), Logs = logs };
    }
}
