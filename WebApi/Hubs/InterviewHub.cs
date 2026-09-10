using Microsoft.AspNetCore.SignalR;
using Application.Interfaces.Services;
using System.Threading.Tasks;

namespace WebApi.Hubs
{
    public class InterviewHub : Hub
    {
        private readonly IInterviewService _interviewService;

        // Bơm Service từ tầng Application vào (Dependency Injection)
        public InterviewHub(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        // Frontend (React/Vue) sẽ gọi hàm này khi sinh viên trả lời
        public async Task SendStudentAnswer(string studentId, string answerText)
        {
            // 1. Nhờ tầng Application gọi API OpenAI xử lý
            var aiResponse = await _interviewService.ProcessStudentAnswerAsync(studentId, answerText);

            // 2. Gửi câu trả lời của AI ngược lại cho đúng người vừa gửi
            // Bắt sự kiện "ReceiveAIFollowUp" ở dưới Frontend
            await Clients.Caller.SendAsync("ReceiveAIFollowUp", aiResponse);
        }
    }
}
