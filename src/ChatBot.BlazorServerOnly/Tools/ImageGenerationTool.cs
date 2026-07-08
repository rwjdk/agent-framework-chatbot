using AgentFrameworkToolkit.AzureOpenAI;
using Microsoft.Extensions.AI;
using ServiceDefaults.Models;
using ServiceDefaults.Services;
#pragma warning disable MEAI001

namespace ChatBot.BlazorServerOnly.Tools;

public class ImageGenerationTool(AzureOpenAIAgentFactory azureOpenAIAgentFactory, Conversation conversation) //todo: can this be done better to support dependency injection?
{
    public async Task<string> GenerateImageAsync(string prompt)
    {
        ImageGenerationResponse imageGenerationResponse = await new ImageGenerationService(azureOpenAIAgentFactory).GenerateAsync(prompt);
        if (imageGenerationResponse.Contents.FirstOrDefault() is not DataContent dataContent)
        {
            return "Failed to generate image (No image Content)";
        }
        
        string generatedImagesFolder = "generated-images";
        string directory = Path.Combine(
            Environment.CurrentDirectory,
            "wwwroot",
            generatedImagesFolder);

        Directory.CreateDirectory(directory);

        string fileName = $"{Guid.CreateVersion7()}.png";
        string path = Path.Combine(directory, fileName);

        await using (FileStream fileStream = File.Create(path))
        {
            await fileStream.WriteAsync(dataContent.Data);
        }
        
        conversation.Messages.Add(new ConversationMessage
        {
            ImagePath = $"{generatedImagesFolder}/{fileName}",
            Role = ChatRole.Assistant
        });

        return "Image Generated";
    }
}