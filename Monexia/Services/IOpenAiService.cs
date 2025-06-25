namespace Monexia.Services
{
    public interface IOpenAiService
    {
        Task<string> GetCompletionAsync(string prompt);
    }
}