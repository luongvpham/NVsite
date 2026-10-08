using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Vsite.Application.Common.Imaging;

namespace Vsite.Infrastructure.Imaging;

/// <summary>
/// Implementation <see cref="IObjectStorage"/> trên S3 / S3-compatible (MinIO) (T2, MEDIA-001,
/// Quyết định #83). Không ghi đè (#75) dựa vào <c>PutObjectRequest.IfNoneMatch = "*"</c> — S3 trả
/// 412 PreconditionFailed nếu key đã tồn tại.
/// </summary>
public sealed class S3ObjectStorage : IObjectStorage
{
    private readonly IAmazonS3 _client;
    private readonly string _bucket;

    public S3ObjectStorage(IOptions<StorageOptions> options)
    {
        var s3Options = options.Value.S3;
        _bucket = s3Options.BucketName;

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(s3Options.Region),
            ForcePathStyle = s3Options.ForcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(s3Options.ServiceUrl))
        {
            config.ServiceURL = s3Options.ServiceUrl;
            // SigV4 cần AuthenticationRegion tường minh khi ký request cho endpoint tự host (MinIO)
            // — SDK không suy được region đúng từ hostname như với S3 thật.
            config.AuthenticationRegion = s3Options.Region;
        }

        _client = s3Options.AccessKey is not null && s3Options.SecretKey is not null
            ? new AmazonS3Client(s3Options.AccessKey, s3Options.SecretKey, config)
            : new AmazonS3Client(config);
    }

    public async Task PutAsync(string key, ReadOnlyMemory<byte> content, string contentType, CancellationToken ct)
    {
        ImagePaths.ValidateKey(key);

        using var stream = new MemoryStream(content.ToArray());
        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = stream,
            ContentType = contentType,
            IfNoneMatch = "*",
            AutoCloseStream = false,
        };

        try
        {
            await _client.PutObjectAsync(request, ct);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed)
        {
            throw new ObjectAlreadyExistsException(key);
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken ct)
    {
        ImagePaths.ValidateKey(key);

        try
        {
            await _client.GetObjectMetadataAsync(new GetObjectMetadataRequest { BucketName = _bucket, Key = key }, ct);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<StoredObject?> OpenReadAsync(string key, CancellationToken ct)
    {
        ImagePaths.ValidateKey(key);

        try
        {
            var response = await _client.GetObjectAsync(new GetObjectRequest { BucketName = _bucket, Key = key }, ct);
            return new StoredObject(response.ResponseStream, response.Headers.ContentType, response.Headers.ContentLength);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        ImagePaths.ValidateKey(key);

        await _client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
    }
}
