namespace MaIN.InferPage.IntegrationTests.Fakes;

public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    public FakeHttpMessageHandler Handler { get; } = new();

    public HttpClient CreateClient(string name) => new(Handler, false);
}
