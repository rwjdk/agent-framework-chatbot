using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using ServiceDefaults.Constants;
using ServiceDefaults.Models;

namespace ServiceDefaults.Services;

public class BlobStorageService(IConfiguration configuration)
{
    private const string AttachmentsContainerName = "attachments";
    private const string GeneratedImagesContainerName = "generated-images";
    private readonly BlobServiceClient _blobServiceClient = new(GetRequiredConnectionString(configuration));

    public async Task<ConversationAttachment> SaveAsync(string userId, string fileName, string contentType, byte[] bytes)
    {
        string storedFileName = $"{Guid.CreateVersion7()}{Path.GetExtension(fileName)}";
        string blobName = GetAttachmentBlobName(userId, storedFileName);
        await UploadAsync(AttachmentsContainerName, blobName, contentType, bytes);

        return new ConversationAttachment
        {
            UserId = userId,
            FileName = fileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            RelativePath = $"/attachments/{storedFileName}"
        };
    }

    public async Task<DataContent> CreateDataContentAsync(ConversationAttachment attachment)
    {
        BlobFile blobFile = await DownloadAttachmentAsync(attachment.UserId, attachment.StoredFileName);
        byte[] fileBytes = blobFile.Bytes;
        string dataUri = $"data:{attachment.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
        return new DataContent(dataUri, attachment.ContentType);
    }

    public async Task<BlobFile?> GetAttachmentAsync(string userId, string storedFileName)
    {
        string safeFileName = Path.GetFileName(storedFileName);
        string blobName = GetAttachmentBlobName(userId, safeFileName);
        return await DownloadAsync(AttachmentsContainerName, blobName);
    }

    public async Task<string> SaveGeneratedImageAsync(string userId, string contentType, ReadOnlyMemory<byte> data)
    {
        string fileName = $"{Guid.CreateVersion7()}.png";
        string blobName = GetUserScopedBlobName(userId, fileName);
        await UploadAsync(GeneratedImagesContainerName, blobName, contentType, data.ToArray());
        return $"/generated-images/{fileName}";
    }

    public async Task<BlobFile?> GetGeneratedImageAsync(string userId, string storedFileName)
    {
        string safeFileName = Path.GetFileName(storedFileName);
        string blobName = GetUserScopedBlobName(userId, safeFileName);
        return await DownloadAsync(GeneratedImagesContainerName, blobName);
    }

    private async Task<BlobFile> DownloadAttachmentAsync(string userId, string storedFileName)
    {
        BlobFile? blobFile = await GetAttachmentAsync(userId, storedFileName);
        return blobFile ?? throw new FileNotFoundException("Uploaded file was not found.", storedFileName);
    }

    private async Task UploadAsync(string containerName, string blobName, string contentType, byte[] bytes)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);

        BlobClient blobClient = containerClient.GetBlobClient(blobName);
        BinaryData data = BinaryData.FromBytes(bytes);
        BlobUploadOptions options = new()
        {
            HttpHeaders = new()
            {
                ContentType = contentType
            }
        };

        await blobClient.UploadAsync(data, options);
    }

    private async Task<BlobFile?> DownloadAsync(string containerName, string blobName)
    {
        BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
        BlobClient blobClient = containerClient.GetBlobClient(blobName);

        try
        {
            Response<BlobDownloadResult> response = await blobClient.DownloadContentAsync();
            BlobDownloadResult result = response.Value;
            string contentType = result.Details.ContentType;
            if (string.IsNullOrWhiteSpace(contentType))
            {
                contentType = "application/octet-stream";
            }

            return new(result.Content.ToArray(), contentType);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    private static string GetAttachmentBlobName(string userId, string storedFileName)
    {
        return GetUserScopedBlobName(userId, storedFileName);
    }

    private static string GetUserScopedBlobName(string userId, string storedFileName)
    {
        return $"{Path.GetFileName(userId)}/{Path.GetFileName(storedFileName)}";
    }

    private static string GetRequiredConnectionString(IConfiguration configuration)
    {
        string? connectionString = configuration[SecretKeys.BlobStorageConnectionString];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Missing required configuration value '{SecretKeys.BlobStorageConnectionString}'.");
        }

        return connectionString;
    }

    public sealed class BlobFile(byte[] bytes, string contentType)
    {
        public byte[] Bytes { get; } = bytes;
        public string ContentType { get; } = contentType;
    }
}
