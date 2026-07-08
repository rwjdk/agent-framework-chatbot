using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.Tools;
using Microsoft.Extensions.AI;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

#pragma warning disable MEAI001

namespace ServiceDefaults.Tools;

public class ImageGenerationTool(AzureOpenAIAgentFactory azureOpenAIAgentFactory, Conversation conversation, BlobStorageService blobStorageService) //todo: can this be done better to support dependency injection?
{
    [AITool("generate_image", "Generate an Image")]
    public async Task<string> GenerateImageAsync(string prompt)
    {
        ImageGenerationResponse imageGenerationResponse = await new ImageGenerationService(azureOpenAIAgentFactory).GenerateAsync(prompt);
        if (imageGenerationResponse.Contents.FirstOrDefault() is not DataContent dataContent)
        {
            return "Failed to generate image (No image Content)";
        }

        string imagePath = await blobStorageService.SaveGeneratedImageAsync(conversation.UserId, "image/png", dataContent.Data);

        conversation.Messages.Add(new ConversationMessage
        {
            ImagePath = imagePath,
            Role = ChatRole.Assistant
        });

        return "Image Generated";
    }
}
