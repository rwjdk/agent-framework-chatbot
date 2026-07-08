using AgentFrameworkToolkit.AzureOpenAI;
using Microsoft.Extensions.AI;
using OpenAI.Images;
using ServiceDefaults.Constants;

#pragma warning disable MEAI001

namespace ServiceDefaults.Services;

public class ImageGenerationService(AzureOpenAIAgentFactory azureOpenAIAgentFactory)
{
    public async Task<ImageGenerationResponse> GenerateAsync(string prompt)
    {
        ImageClient imageClient = azureOpenAIAgentFactory.Connection.GetClient().GetImageClient(AIModelIds.ImageModel);
        IImageGenerator imageGenerator = imageClient.AsIImageGenerator();
        ImageGenerationResponse response = await imageGenerator.GenerateAsync(new ImageGenerationRequest(prompt));
        return response;
    }
}