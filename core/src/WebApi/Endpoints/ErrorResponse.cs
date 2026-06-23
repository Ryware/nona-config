namespace Nona.WebApi.Endpoints;

internal sealed record ErrorResponse(string Error, string? ErrorCode = null);
