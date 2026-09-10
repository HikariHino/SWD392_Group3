using System.Threading.Tasks;

namespace Application.Interfaces.ExternalServices
{
    public interface IOpenAIService
    {
        Task<string> AskChatGPTAsync(string prompt);
    }
}
