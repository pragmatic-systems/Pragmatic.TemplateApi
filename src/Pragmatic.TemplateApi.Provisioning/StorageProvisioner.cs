using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Polly;

namespace Pragmatic.TemplateApi.Provisioning;

public static class StorageProvisioner
{
    // Retries transport-level failures (endpoint not up yet). AWS wraps those in AmazonS3Exception.
    private static readonly AsyncPolicy RetryPolicy = Policy
            .Handle<AmazonS3Exception>()
            .Or<HttpRequestException>()
            .Or<IOException>()
            .WaitAndRetryAsync(
                10,
                i => TimeSpan.FromSeconds(2),
                (e, t) => Console.WriteLine("Retrying... Waiting for storage"));

    public static async Task EnsureBucketAsync(string endpoint, string accessKeyId, string secretAccessKey, string region, string bucket)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            throw new ArgumentException("endpoint is required.");
        if (string.IsNullOrWhiteSpace(accessKeyId) || string.IsNullOrWhiteSpace(secretAccessKey))
            throw new ArgumentException("accessKeyId and secretAccessKey are required.");
        if (string.IsNullOrWhiteSpace(bucket))
            throw new ArgumentException("bucket is required.");

        await RetryPolicy.ExecuteAsync(async () =>
        {
            var config = new AmazonS3Config
            {
                ServiceURL = endpoint,
                AuthenticationRegion = region,
                ForcePathStyle = true,
            };

            using var s3 = new AmazonS3Client(new BasicAWSCredentials(accessKeyId, secretAccessKey), config);

            try
            {
                await s3.HeadBucketAsync(new HeadBucketRequest { BucketName = bucket });
                Console.WriteLine($"Bucket '{bucket}' already exists.");
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                await s3.PutBucketAsync(new PutBucketRequest { BucketName = bucket });
                Console.WriteLine($"Bucket '{bucket}' created.");
            }
        });
    }
}
