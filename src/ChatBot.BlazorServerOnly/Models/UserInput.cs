namespace ChatBot.BlazorServerOnly.Models;

public class UserInput
{
    public string? Text { get; set; }
    public List<UserInputAttachment> Attachments { get; set; } = [];

    public void Reset()
    {
        Text = null;
        Attachments = [];
    }
}