using Azure.Identity;
using DataverseSolutionCompiler.Domain.Abstractions;
using DataverseSolutionCompiler.Domain.Live;

namespace DataverseSolutionCompiler.Readers.Live;

public sealed class WebApiLiveSnapshotProvider : ILiveSnapshotProvider
{
    public LiveSnapshot Readback(ReadbackRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var credentialOptions = new DefaultAzureCredentialOptions();
        if (!string.IsNullOrWhiteSpace(request.Environment.TenantId))
        {
            credentialOptions.TenantId = request.Environment.TenantId;
        }

        using var httpClient = new HttpClient();
        var reader = new DataverseWebApiLiveReader(
            httpClient,
            new DefaultAzureCredential(credentialOptions),
            new DataverseWebApiLiveReaderOptions());

        return reader.ReadAsync(request, CancellationToken.None).GetAwaiter().GetResult();
    }
}
