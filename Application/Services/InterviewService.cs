using System.Threading.Tasks;
using Application.Interfaces.Services;

namespace Application.Services
{
    public class InterviewService : IInterviewService
    {
        // Nhúng IOpenAIService, IVoiceService vào đây qua constructor sau
        
        public async Task<string> ProcessStudentAnswerAsync(string studentId, string answerText)
        {
            // Logic: Gọi OpenAI tạo câu hỏi tiếp theo
            return await Task.FromResult("Đây là câu hỏi tiếp theo từ AI (Chưa code ruột)");
        }
    }
}
