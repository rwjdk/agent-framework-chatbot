namespace ServiceDefaults.Models;

public class StoredFile(byte[] bytes, string contentType)
{
    public byte[] Bytes { get; } = bytes;
    public string ContentType { get; } = contentType;
}