using System.Net;
using System.Text;

namespace EventOrganizer.Tests;

public sealed class FakeHttpHandler : HttpMessageHandler
{
    public sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Body);

    private sealed record Route(HttpMethod Method, string Path, Func<HttpResponseMessage> CreateResponse);

    private readonly List<Route> _routes = [];

    public List<RecordedRequest> Requests { get; } = [];

    public HttpMethod? RequestMethod => Requests.LastOrDefault()?.Method;
    public Uri? RequestUri => Requests.LastOrDefault()?.Uri;
    public string? RequestBody => Requests.LastOrDefault()?.Body;

    public FakeHttpHandler When(
        HttpMethod method,
        string path,
        HttpStatusCode status,
        string? json = null,
        string? location = null)
    {
        _routes.Add(new Route(method, path, () =>
        {
            var response = new HttpResponseMessage(status);
            if (json is not null)
                response.Content = new StringContent(json, Encoding.UTF8, "application/json");
            if (location is not null)
                response.Headers.Location = new Uri(location, UriKind.RelativeOrAbsolute);
            return response;
        }));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);

        Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, body));

        var path = request.RequestUri!.AbsolutePath;
        var route = _routes.LastOrDefault(r => r.Method == request.Method && r.Path == path)
            ?? throw new InvalidOperationException(
                $"FakeHttpHandler: для запиту {request.Method} {path} не підготовлено відповіді.");

        return route.CreateResponse();
    }
}
