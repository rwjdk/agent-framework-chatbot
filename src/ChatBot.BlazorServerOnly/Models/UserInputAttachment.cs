namespace ChatBot.BlazorServerOnly.Models;

public class UserInputAttachment(string fileName, string contentType, byte[] bytes, string? previewDataUri)
{
    public string FileName { get; } = fileName;
    public string ContentType { get; } = contentType;
    public byte[] Bytes { get; } = bytes;
    public string? PreviewDataUri { get; } = previewDataUri;
}