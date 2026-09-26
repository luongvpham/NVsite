using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Testcontainers.Minio;
using Vsite.Application.Common.Imaging;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Contract test cho <see cref="S3ObjectStorage"/> qua MinIO thật (Testcontainers, T2, MEDIA-001) —
/// CẦN Docker daemon. Máy không có Docker: ghi nợ vào <c>Docs/DOCKER-TEST-DEBT.md</c> theo quy ước ở
/// backend/CLAUDE.md, KHÔNG đánh dấu Skip.
/// </summary>
public sealed class S3ObjectStorageTests : ObjectStorageContractTests
{
    private const string BucketName = "vsite-media-test";

    private readonly MinioContainer _container = new MinioBuilder().Build();
    private AmazonS3Client _rawClient = null!;

    protected override async Task<IObjectStorage> CreateStorageAsync()
    {
        await _container.StartAsync();

        var s3Options = new S3StorageOptions
        {
            BucketName = BucketName,
            ServiceUrl = _container.GetConnectionString(),
            ForcePathStyle = true,
            Region = "us-east-1",
            AccessKey = _container.GetAccessKey(),
            SecretKey = _container.GetSecretKey(),
        };

        var config = new AmazonS3Config
        {
            ServiceURL = s3Options.ServiceUrl,
            ForcePathStyle = true,
            RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region),
        };
        _rawClient = new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config);
        await _rawClient.PutBucketAsync(BucketName);

        return new S3ObjectStorage(Options.Create(new StorageOptions
        {
            Provider = StorageProvider.S3,
            S3 = s3Options,
        }));
    }

    protected override async Task<int> CountStoredObjectsAsync()
    {
        var response = await _rawClient.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName });
        return response.S3Objects.Count;
    }

    public override async Task DisposeAsync()
    {
        _rawClient.Dispose();
        await _container.DisposeAsync();
    }
}
