using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Testcontainers.LocalStack;
using Vsite.Application.Common.Imaging;
using Vsite.Infrastructure.Imaging;

namespace Vsite.IntegrationTests.Imaging;

/// <summary>
/// Contract test cho <see cref="S3ObjectStorage"/> qua LocalStack thật (Testcontainers, T2,
/// MEDIA-001) — CẦN Docker daemon. Máy không có Docker: ghi nợ vào
/// <c>Docs/DOCKER-TEST-DEBT.md</c> theo quy ước ở backend/CLAUDE.md, KHÔNG đánh dấu Skip.
///
/// Đổi từ MinIO (2026-09-27): minio/minio không còn publish image public trên Docker Hub lẫn
/// quay.io (cả hai đều từ chối pull — xác nhận thủ công). LocalStack's S3 emulation (dịch vụ `s3`
/// trong image chung `localstack/localstack`) hỗ trợ conditional write qua header
/// <c>If-None-Match: *</c> — verify bằng test <see cref="ObjectStorageContractTests.Put_twice_same_key_throws_and_keeps_original_content"/>,
/// pin <c>4.0.3</c>, đã pull-verify tồn tại làm tag thật trên Docker Hub (2026-09-27). Xem ghi chú
/// ở backend/docs/modules/media.md §6 cho danh sách image đã thử.
/// </summary>
public sealed class S3ObjectStorageTests : ObjectStorageContractTests
{
    private const string BucketName = "vsite-media-test";
    private const string LocalStackImage = "localstack/localstack:4.0.3";

    private readonly LocalStackContainer _container = new LocalStackBuilder()
        .WithImage(LocalStackImage)
        .WithEnvironment("SERVICES", "s3")
        .Build();

    private AmazonS3Client? _rawClient;

    protected override async Task<IObjectStorage> CreateStorageAsync()
    {
        await _container.StartAsync();

        var s3Options = new S3StorageOptions
        {
            BucketName = BucketName,
            ServiceUrl = _container.GetConnectionString(),
            ForcePathStyle = true,
            Region = "us-east-1",
            AccessKey = "test",
            SecretKey = "test",
        };

        // ⚠️ Thứ tự set property ở đây quan trọng: AmazonS3Config coi RegionEndpoint và ServiceURL là
        // hai cách khai endpoint loại trừ nhau — set RegionEndpoint SAU sẽ âm thầm xoá ServiceURL đã
        // set trước đó (SDK rơi về endpoint AWS thật, https://s3.amazonaws.com — tự bắt được lúc debug
        // spike: request thật sự đi tới AWS, LocalStack không hề nhận được request, lỗi trả về là
        // "AWS Access Key Id ... does not exist" từ chính AWS thật vì credential "test/test" không
        // tồn tại ở đó). Set RegionEndpoint TRƯỚC, ServiceURL SAU — cùng thứ tự với production code
        // (<see cref="S3ObjectStorage"/> constructor).
        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region),
            ForcePathStyle = true,
        };
        config.ServiceURL = s3Options.ServiceUrl;
        config.AuthenticationRegion = s3Options.Region;
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
        var response = await _rawClient!.ListObjectsV2Async(new ListObjectsV2Request { BucketName = BucketName });
        return response.S3Objects.Count;
    }

    public override async Task DisposeAsync()
    {
        // _rawClient chỉ null nếu _container.StartAsync() (CreateStorageAsync) ném trước khi gán được
        // — container không lên được thì cũng không có gì để dispose ở phía client.
        _rawClient?.Dispose();
        await _container.DisposeAsync();
    }
}
