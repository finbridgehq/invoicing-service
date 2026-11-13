using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using LiquiLabs.Vankoo.Invoicing.Application.Interfaces;
using LiquiLabs.Vankoo.Invoicing.Domain.ValueObjects;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Configuration.Settings;
using LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage.Exceptions;

namespace LiquiLabs.Vankoo.Invoicing.Infrastructure.Storage;

public sealed class S3StorageService : IStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly ObjectStorageRuntimeSettings _settings;

    public S3StorageService(IAmazonS3 s3Client, ObjectStorageRuntimeSettings settings)
    {
        _s3Client = s3Client;
        _settings = settings;
    }

    public Task<Stream> GetFileStreamAsync(FileKey fileKey, CancellationToken ct = default)
        => DownloadAsync(fileKey.Value, ct);

    public async Task UploadAsync(string key, Stream content, string contentType, CancellationToken ct = default)
    {
        try
        {
            if (_settings.AutoCreateBucket)
                await EnsureBucketExistsAsync(ct);

            var request = new PutObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key,
                InputStream = content,
                ContentType = contentType
            };

            await _s3Client.PutObjectAsync(request, ct);
        }
        catch (AmazonServiceException ex)
        {
            throw new FileUploadException(key, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileUploadException(key, ex);
        }
    }

    public async Task<Stream> DownloadAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var request = new GetObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            };

            var response = await _s3Client.GetObjectAsync(request, ct);
            return response.ResponseStream;
        }
        catch (AmazonServiceException ex)
        {
            throw new FileDownloadException(key, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileDownloadException(key, ex);
        }
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        try
        {
            var request = new DeleteObjectRequest
            {
                BucketName = _settings.BucketName,
                Key = key
            };

            await _s3Client.DeleteObjectAsync(request, ct);
        }
        catch (AmazonServiceException ex)
        {
            throw new FileDeleteException(key, ex);
        }
        catch (HttpRequestException ex)
        {
            throw new FileDeleteException(key, ex);
        }
    }

    private async Task EnsureBucketExistsAsync(CancellationToken ct)
    {
        var exists = await AmazonS3Util.DoesS3BucketExistV2Async(_s3Client, _settings.BucketName);
        if (!exists)
            await _s3Client.PutBucketAsync(_settings.BucketName, ct);
    }
}
