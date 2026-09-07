using System;
using System.Net;

namespace Nona.Client;

public sealed class NonaClientException : Exception
{
    public NonaClientException(
        string message,
        HttpStatusCode statusCode,
        string method,
        Uri? requestUri,
        string? responseBody,
        Exception? innerException = null,
        string? errorCode = null,
        string? detail = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        Method = method;
        RequestUri = requestUri;
        ResponseBody = responseBody;
        ErrorCode = errorCode;
        Detail = detail;
    }

    public HttpStatusCode StatusCode { get; }

    public string Method { get; }

    public Uri? RequestUri { get; }

    public string? ResponseBody { get; }

    public string? ErrorCode { get; }

    public string? Detail { get; }
}
