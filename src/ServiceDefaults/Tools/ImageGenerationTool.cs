using AgentFrameworkToolkit.AzureOpenAI;
using AgentFrameworkToolkit.Tools;
using JetBrains.Annotations;
using Microsoft.Extensions.AI;
using ServiceDefaults.Interfaces;
using ServiceDefaults.Models;
using ServiceDefaults.Services;

#pragma warning disable MEAI001

namespace ServiceDefaults.Tools;

public class ImageGenerationTool(AzureOpenAIAgentFactory azureOpenAIAgentFactory, Conversation conversation, IStorageService storageService)
{
    [AITool("generate_image", "Generate an Image")]
    [UsedImplicitly]
    public async Task<string> GenerateImageAsync(string prompt)
    {
        ImageGenerationResponse imageGenerationResponse = await new ImageGenerationService(azureOpenAIAgentFactory).GenerateAsync(prompt);
        if (imageGenerationResponse.Contents.FirstOrDefault() is not DataContent dataContent)
        {
            return "Failed to generate image (No image Content)";
        }

        string imagePath = await storageService.SaveGeneratedImageAsync(conversation.UserId, "image/png", dataContent.Data);

        conversation.Messages.Add(new ConversationMessage
        {
            ImagePath = imagePath,
            Role = ChatRole.Assistant
        });

        return "Image Generated";
    }
}
