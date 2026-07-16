using System.Net;
using System.Text;
using System.Text.Json;

namespace MaIN.InferPage.IntegrationTests.Fakes;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    public HttpRequestMessage? LastRequest { get; private set; }
    public string? LastRequestBody { get; private set; }
    public JsonDocument? LastRequestJson { get; private set; }
    public HttpStatusCode ResponseStatusCode { get; set; } = HttpStatusCode.OK;
    public string ResponseBody { get; set; } = string.Empty;

    private readonly Queue<string> _responseQueue = new();

    public void EnqueueResponse(string body) => _responseQueue.Enqueue(body);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        LastRequest = request;
        if (request.Content is not null)
        {
            LastRequestBody = await request.Content.ReadAsStringAsync(ct);
            try
            {
                LastRequestJson = JsonDocument.Parse(LastRequestBody);
            }
            catch { }
        }

        var body = _responseQueue.Count > 0 ? _responseQueue.Dequeue() : ResponseBody;
        return new HttpResponseMessage(ResponseStatusCode)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
    }
}
