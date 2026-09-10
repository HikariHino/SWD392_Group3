using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IInterviewService
    {
        // Hàm ví dụ: Xử lý câu trả lời của sinh viên và nhờ AI tạo câu hỏi xoáy tiếp theo
        Task<string> ProcessStudentAnswerAsync(string studentId, string answerText);
    }
}
