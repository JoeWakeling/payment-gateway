using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace PaymentGateway.IntegrationTests;

public class StubAcquiringBankHandler : HttpMessageHandler
{
    private readonly ConcurrentQueue<ReceivedRequest> _requests = new();
    private HttpStatusCode _statusCode = HttpStatusCode.OK;
    private bool _authorized = true;
    private Exception? _exception;

    public IReadOnlyCollection<ReceivedRequest> Requests => _requests;

    public void RespondWithAuthorized(bool authorized)
    {
        _statusCode = HttpStatusCode.OK;
        _authorized = authorized;
    }

    public void RespondWithStatusCode(HttpStatusCode statusCode) => _statusCode = statusCode;

    public void ThrowOnSend(Exception exception) => _exception = exception;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : JsonNode.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        _requests.Enqueue(new ReceivedRequest(request.Method, request.RequestUri!, body));

        if (_exception is not null)
        {
            throw _exception;
        }

        if (_statusCode != HttpStatusCode.OK)
        {
            return new HttpResponseMessage(_statusCode);
        }

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                authorized = _authorized,
                authorization_code = _authorized ? Guid.NewGuid().ToString() : null
            })
        };
    }

    public record ReceivedRequest(HttpMethod Method, Uri Uri, JsonNode? Body);
}
