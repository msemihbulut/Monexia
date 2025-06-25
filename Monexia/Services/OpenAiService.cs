using OpenAI.Interfaces;
using OpenAI.ObjectModels;
using OpenAI.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Monexia.Services
{
    public class OpenAiService : IOpenAiService
    {
        private readonly IOpenAIService _openAiApiService;

        public OpenAiService(IOpenAIService openAiApiService)
        {
            _openAiApiService = openAiApiService;
        }

        public async Task<string> GetCompletionAsync(string prompt)
        {
            try
            {
                var completionResult = await _openAiApiService.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest
                {
                    Messages = new List<ChatMessage>
                    {
                        ChatMessage.FromSystem("You are a helpful financial assistant."),
                        ChatMessage.FromUser(prompt)
                    },
                    Model = "gpt-4o",
                });

                if (completionResult.Successful)
                {
                    return completionResult.Choices.First().Message.Content;
                }

                var error = completionResult.Error == null ? "Unknown error" : $"{completionResult.Error.Code}: {completionResult.Error.Message}";
                // Log the error
                Console.WriteLine($"OpenAI API error: {error}");
                return $"Error: Could not get a response from the AI service. Details: {error}";

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // Log the exception
                return "An unexpected error occurred while communicating with the AI service.";
            }
        }
    }
}