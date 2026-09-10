using System.Threading.Tasks;
using Application.Interfaces.ExternalServices;

namespace Infrastructure.ExternalServices
{
    public class OpenAIService : IOpenAIService
    {
        public async Task<string> AskChatGPTAsync(string prompt)
        {
            // Cấu hình HttpClient gọi tới OpenAI API ở đây
            return await Task.FromResult("Phản hồi từ ChatGPT...");
        }
    }
}
