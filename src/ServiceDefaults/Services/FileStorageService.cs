using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.AI;
using ServiceDefaults.Constants;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;

namespace ServiceDefaults.Services;

public class FileStorageService : IStorageService
{
    private const string AttachmentsFolderName = "attachments";
    private const string GeneratedImagesFolderName = "generated-images";

    public async Task<ConversationAttachment> SaveAttachmentAsync(string userId, string fileName, string contentType, byte[] bytes)
    {
        string storedFileName = $"{Guid.CreateVersion7()}{Path.GetExtension(fileName)}";
        string path = GetAttachmentPath(userId, storedFileName);
        await File.WriteAllBytesAsync(path, bytes);
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
        StoredFile file = await RetrieveAttachmentAsync(attachment.UserId, attachment.StoredFileName);
        byte[] fileBytes = file.Bytes;
        string dataUri = $"data:{attachment.ContentType};base64,{Convert.ToBase64String(fileBytes)}";
        return new DataContent(dataUri, attachment.ContentType);
    }

    public async Task<StoredFile?> GetAttachmentAsync(string userId, string fileName)
    {
        string safeFileName = Path.GetFileName(fileName);
        string path = GetAttachmentPath(userId, safeFileName);
        return await RetrieveAsync(path);
    }

    public async Task<string> SaveGeneratedImageAsync(string userId, string contentType, ReadOnlyMemory<byte> data)
    {
        string fileName = $"{Guid.CreateVersion7()}.png";
        string path = GetGeneratedImagePath(userId, fileName);
        await File.WriteAllBytesAsync(path, data);
        return $"/generated-images/{fileName}";
    }

    public async Task<StoredFile?> GetGeneratedImageAsync(string userId, string fileName)
    {
        string safeFileName = Path.GetFileName(fileName);
        string path = GetGeneratedImagePath(userId, safeFileName);
        return await RetrieveAsync(path);
    }

    private async Task<StoredFile> RetrieveAttachmentAsync(string userId, string fileName)
    {
        StoredFile? file = await GetAttachmentAsync(userId, fileName);
        return file ?? throw new FileNotFoundException("File was not found.", fileName);
    }

    private async Task<StoredFile?> RetrieveAsync(string path)
    {
        byte[] bytes = await File.ReadAllBytesAsync(path);
        try
        {
            string extension = Path.GetExtension(path);
            string contentType = extension switch
            {
                "png" => "image/png",
                "pdf" => "application/pdf",
                _ => "application/octet-stream"
            };
            return new StoredFile(bytes, contentType);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    private static string GetAttachmentPath(string userId, string fileName)
    {
        string folder = Path.Combine(GetRootFolder(), AttachmentsFolderName, userId);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        return path;
    }

    private static string GetGeneratedImagePath(string userId, string fileName)
    {
        string folder = Path.Combine(GetRootFolder(), GeneratedImagesFolderName, userId);
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, fileName);
        return path;
    }

    private static string GetRootFolder()
    {
        return Path.Combine(Path.GetTempPath(), "chatbot");
    }

}
