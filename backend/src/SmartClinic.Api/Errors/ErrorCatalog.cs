using System.Net;
using SmartClinic.Application.Errors;

namespace SmartClinic.Api.Errors;

/// <summary>
/// T-006 — maps each <see cref="ErrorCodes"/> (Application) code to an
/// HTTP status and a default, safe, user-facing message. This is the ONE
/// place in the whole codebase that owns that mapping — no controller,
/// service, or endpoint hard-codes a status code or a user-facing error
/// string.
///
/// Also defines the two Api-only fallback codes for cases where there is
/// no Application exception at all: an unmapped/unknown exception
/// (<see cref="InternalError"/>) and a request that never reached any
/// endpoint (<see cref="MethodNotAllowed"/>, unmatched-route 404 reuses
/// <see cref="ErrorCodes.NotFound"/>).
/// </summary>
public static class ErrorCatalog
{
    public const string InternalError = "INTERNAL_ERROR";
    public const string MethodNotAllowed = "METHOD_NOT_ALLOWED";

    public static readonly IReadOnlyDictionary<string, (HttpStatusCode Status, string DefaultMessage)> Entries =
        new Dictionary<string, (HttpStatusCode Status, string DefaultMessage)>
        {
            [ErrorCodes.ValidationFailed] = (HttpStatusCode.BadRequest, "One or more fields are invalid."),
            [ErrorCodes.NotFound] = (HttpStatusCode.NotFound, "The requested resource was not found."),
            [ErrorCodes.Conflict] = (HttpStatusCode.Conflict, "The request could not be completed due to a conflict."),
            [ErrorCodes.Unauthorized] = (HttpStatusCode.Unauthorized, "Authentication is required."),
            [ErrorCodes.Forbidden] = (HttpStatusCode.Forbidden, "You do not have permission to perform this action."),
            [MethodNotAllowed] = (HttpStatusCode.MethodNotAllowed, "This HTTP method is not supported for this resource."),

            // Fallback for ANYTHING unmapped. Deliberately generic — this
            // message is the only thing that may ever reach the client for
            // an exception that isn't one of the AppException types above.
            [InternalError] = (HttpStatusCode.InternalServerError, "An unexpected error occurred. Please try again."),
        };

    /// <summary>Looks up a code, falling back to <see cref="InternalError"/> for anything unrecognized (defensive — should not normally happen).</summary>
    public static (HttpStatusCode Status, string DefaultMessage) Resolve(string code) =>
        Entries.TryGetValue(code, out var entry) ? entry : Entries[InternalError];

    /// <summary>Best-effort default code for a raw HTTP status with no exception behind it (e.g. an unmatched route).</summary>
    public static string DefaultCodeForStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => ErrorCodes.NotFound,
        HttpStatusCode.MethodNotAllowed => MethodNotAllowed,
        HttpStatusCode.Unauthorized => ErrorCodes.Unauthorized,
        HttpStatusCode.Forbidden => ErrorCodes.Forbidden,
        _ => InternalError,
    };
}
