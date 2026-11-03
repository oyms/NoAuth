using System.Net;
using Microsoft.Extensions.Primitives;
using Skaar.NoAuth.Exceptions;

namespace Skaar.NoAuth.Services;

/// <summary>
/// Parameters from the form (if any) with fallback to the query string.
/// </summary>
public sealed class RequestParameters
{
    private readonly IFormCollection? form;
    private readonly IQueryCollection query;

    private RequestParameters(IFormCollection? form, IQueryCollection query)
    {
        this.form = form;
        this.query = query;
    }

    public StringValues this[string key] =>
        form is not null && form.TryGetValue(key, out var formValue) ? formValue
        : query.TryGetValue(key, out var queryValue) ? queryValue
        : StringValues.Empty;

    public string? GetOptional(string key)
    {
        var value = this[key].ToString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>All non-empty values of a parameter that may be repeated, like <c>resource</c>.</summary>
    public List<string> GetAll(string key) =>
        this[key].OfType<string>().Where(v => !string.IsNullOrWhiteSpace(v)).ToList();

    public string GetRequired(string key) =>
        GetOptional(key) ?? throw new StatusCodeException(HttpStatusCode.BadRequest, $"Missing parameter '{key}'");

    public static async Task<RequestParameters> Read(HttpContext context)
    {
        var form = context.Request.HasFormContentType ? await context.Request.ReadFormAsync() : null;
        return new RequestParameters(form, context.Request.Query);
    }
}
