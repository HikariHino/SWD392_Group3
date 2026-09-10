using System.Threading.Tasks;
using Application.Interfaces.Services;

namespace Application.Services
{
    public class QuestionBankService : IQuestionBankService
    {
        public async Task<bool> ImportQuestionsAsync(string filePath)
        {
            // Logic đọc file, map vào Entity và lưu DB
            return await Task.FromResult(true);
        }
    }
}
