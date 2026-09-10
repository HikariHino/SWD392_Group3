using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IQuestionBankService
    {
        Task<bool> ImportQuestionsAsync(string filePath);
    }
}
