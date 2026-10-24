using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Pragmatic.TemplateApi.Instrumentation.HostedServices;

public class BucketInitService : IHostedService
{
    private readonly AmazonS3Client _s3;
    private readonly StorageOptions _options;

    public BucketInitService(AmazonS3Client s3, IOptions<StorageOptions> options)
    {
        _s3 = s3;
        _options = options.Value;
    }

    // NOTE: This implementation is temporary - moving provisioning to infra.
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _s3.HeadBucketAsync(new HeadBucketRequest { BucketName = _options.Bucket }, cancellationToken);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            await _s3.PutBucketAsync(new PutBucketRequest { BucketName = _options.Bucket }, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
