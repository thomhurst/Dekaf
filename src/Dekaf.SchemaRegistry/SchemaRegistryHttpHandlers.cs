namespace Dekaf.SchemaRegistry;

internal sealed class NonDisposingHttpMessageHandler : HttpMessageHandler
{
    private readonly HttpMessageInvoker _invoker;

    internal NonDisposingHttpMessageHandler(HttpMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        _invoker = new HttpMessageInvoker(handler, disposeHandler: false);
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _invoker.SendAsync(request, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _invoker.Dispose();

        base.Dispose(disposing);
    }
}

internal sealed class NonDisposingHttpClientHandler : HttpMessageHandler
{
    private readonly HttpClient _httpClient;

    internal NonDisposingHttpClientHandler(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken) =>
        _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
}

internal sealed class CertificateOwningHttpMessageHandler(
    HttpMessageHandler innerHandler,
    SchemaRegistryCertificateMaterial certificateMaterial)
    : DelegatingHandler(innerHandler)
{
    protected override void Dispose(bool disposing)
    {
        if (!disposing)
        {
            base.Dispose(disposing);
            return;
        }

        try
        {
            base.Dispose(disposing);
        }
        finally
        {
            certificateMaterial.Dispose();
        }
    }
}
