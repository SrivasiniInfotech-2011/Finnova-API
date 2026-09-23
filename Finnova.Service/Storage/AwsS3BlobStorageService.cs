using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace Finnova.Service.Storage;

/// <summary>
/// <see cref="IBlobStorageService"/> implementation backed by AWS S3.
/// Container names map to S3 buckets and blob names map to object keys.
/// A custom <see cref="AwsBlobStorageOptions.ServiceUrl"/> can point the client at a
/// LocalStack/MinIO endpoint for local development.
/// </summary>
public sealed class AwsS3BlobStorageService : IBlobStorageService
{
    private readonly IAmazonS3 _client;
    private readonly string _baseUrl;

    public AwsS3BlobStorageService(IOptions<BlobStorageOptions> options)
    {
        var aws = options.Value.Aws;

        var config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(aws.Region)
        };

        // When targeting a custom endpoint (LocalStack/MinIO), use path-style addressing.
        if (!string.IsNullOrWhiteSpace(aws.ServiceUrl))
        {
            config.ServiceURL = aws.ServiceUrl;
            config.ForcePathStyle = true;
            _baseUrl = aws.ServiceUrl.TrimEnd('/');
        }
        else
        {
            _baseUrl = $"https://s3.{aws.Region}.amazonaws.com";
        }

        var credentials = new BasicAWSCredentials(aws.AccessKey, aws.SecretKey);
        _client = new AmazonS3Client(credentials, config);
    }

    public async Task<BlobUploadResult> UploadAsync(
        string containerName,
        string blobName,
        Stream content,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        await EnsureBucketExistsAsync(containerName, cancellationToken);

        var request = new PutObjectRequest
        {
            BucketName = containerName,
            Key = blobName,
            InputStream = content,
            AutoCloseStream = false
        };

        if (!string.IsNullOrWhiteSpace(contentType))
        {
            request.ContentType = contentType;
        }

        await _client.PutObjectAsync(request, cancellationToken);

        var metadata = await _client.GetObjectMetadataAsync(
            new GetObjectMetadataRequest { BucketName = containerName, Key = blobName },
            cancellationToken);

        return new BlobUploadResult
        {
            ContainerName = containerName,
            BlobName = blobName,
            Uri = $"{_baseUrl}/{containerName}/{blobName}",
            SizeInBytes = metadata.ContentLength,
            ContentType = metadata.Headers.ContentType
        };
    }

    public async Task<Stream> DownloadAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(
                new GetObjectRequest { BucketName = containerName, Key = blobName },
                cancellationToken);

            // Copy to a seekable memory stream so the caller isn't tied to the HTTP response lifetime.
            var memory = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memory, cancellationToken);
            memory.Position = 0;
            return memory;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException(
                $"Object '{blobName}' was not found in bucket '{containerName}'.");
        }
    }

    public async Task<bool> DeleteAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        if (!await ExistsAsync(containerName, blobName, cancellationToken))
        {
            return false;
        }

        await _client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = containerName, Key = blobName },
            cancellationToken);

        return true;
    }

    public async Task<bool> ExistsAsync(
        string containerName,
        string blobName,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _client.GetObjectMetadataAsync(
                new GetObjectMetadataRequest { BucketName = containerName, Key = blobName },
                cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> ListAsync(
        string containerName,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var names = new List<string>();
        var request = new ListObjectsV2Request
        {
            BucketName = containerName,
            Prefix = prefix
        };

        ListObjectsV2Response response;
        do
        {
            try
            {
                response = await _client.ListObjectsV2Async(request, cancellationToken);
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Array.Empty<string>();
            }

            names.AddRange(response.S3Objects.Select(o => o.Key));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated == true);

        return names;
    }

    private async Task EnsureBucketExistsAsync(string bucketName, CancellationToken cancellationToken)
    {
        var exists = await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(_client, bucketName);
        if (!exists)
        {
            await _client.PutBucketAsync(
                new PutBucketRequest { BucketName = bucketName },
                cancellationToken);
        }
    }
}
