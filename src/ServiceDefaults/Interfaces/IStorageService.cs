using Microsoft.Extensions.AI;
using ServiceDefaults.Models;

namespace ServiceDefaults.Interfaces;

public interface IStorageService
{
    Task<ConversationAttachment> SaveAsync(string userId, string fileName, string contentType, byte[] bytes);
    Task<DataContent> CreateDataContentAsync(ConversationAttachment attachment);
    Task<StoredFile?> GetAttachmentAsync(string userId, string fileName);
    Task<string> SaveGeneratedImageAsync(string userId, string contentType, ReadOnlyMemory<byte> data);
    Task<StoredFile?> GetGeneratedImageAsync(string userId, string fileName);
}