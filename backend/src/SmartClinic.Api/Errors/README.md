# Error Envelope & Centralized Handling (T-006)

One JSON shape for every API error, always these five keys:

```json
{
  "status": 404,
  "code": "NOT_FOUND",
  "message": "The requested resource was not found.",
  "fields": [],
  "correlationId": "0HN7QK2F4:00000001"
}
```

`fields` is always an array — `[]` when not a validation error, populated
(`{ "name", "code", "message" }` per entry) when it is. Nothing else is
ever added to this shape; there is no ProblemDetails fallback.

## Where things live

- `SmartClinic.Application/Errors/` — `ErrorCodes` (the string constants:
  `VALIDATION_FAILED`, `NOT_FOUND`, `CONFLICT`, `UNAUTHORIZED`,
  `FORBIDDEN`) and `AppException` + its five subclasses. Pure C#, no HTTP
  knowledge — Application must not depend on ASP.NET Core.
- `SmartClinic.Api/Errors/` — `ErrorCatalog` (maps each code to an HTTP
  status + default message; also owns the two Api-only fallback codes,
  `INTERNAL_ERROR` and `METHOD_NOT_ALLOWED`), `ErrorEnvelope`/`ErrorFieldDto`
  (the response shape), `ErrorEnvelopeWriter` (the one function that
  writes it), `GlobalExceptionHandler` (ASP.NET Core's built-in
  `IExceptionHandler`, registered in `Program.cs`).

## The two paths every error takes

1. **A thrown exception** → `GlobalExceptionHandler.TryHandleAsync`.
   `AppException` → mapped via `ErrorCatalog`. Anything else → the fixed
   `INTERNAL_ERROR` entry only; the real exception (message, type, stack
   trace) is logged server-side, never in the response.
2. **A status-code response with no exception** (unmatched route/method)
   → `UseStatusCodePages` in `Program.cs`, using the exact same
   `ErrorEnvelopeWriter`.

No controller/service/endpoint writes its own error JSON or hard-codes a
user-facing error string — every one of them either returns success or
throws one of the five `AppException` subclasses.

## correlationId

Reuses `HttpContext.TraceIdentifier` — the identifier ASP.NET Core already
assigns to every request. `GlobalExceptionHandler` passes the exact same
value as a structured logging property (`CorrelationId={CorrelationId}`)
when it logs, so the id in the response body and the id in the
corresponding log entry are always identical. **T-006 does not build a
new correlation-ID system** — that, plus redaction and correlating IDs
across all layers/services, is T-007's job.

## Probe endpoints (temporary)

`Endpoints/ErrorHandlingProbeEndpoints.cs` — three non-business routes
under `/api/v1/_t006-probe/*` used only by
`SmartClinic.Api.Tests.ErrorEnvelopeTests` to exercise the mechanism
against real HTTP requests. Same role as T-005's `PipelineProbeEndpoints`.
Safe to delete once real endpoints exist that naturally throw
`AppException`s.

## Explicitly out of scope (not built here)

- Auth-related codes (`UNAUTHORIZED`/`FORBIDDEN`) are reserved in the
  catalog but nothing currently throws them — no authentication/
  authorization middleware exists yet.
- Structured logging, log redaction, and a correlation ID that spans
  multiple services/layers: T-007.
- Any business/domain exception (e.g. a real "patient not found") — added
  by whichever task introduces that feature, by throwing the existing
  `NotFoundAppException`/etc.; no changes to this mechanism required.
