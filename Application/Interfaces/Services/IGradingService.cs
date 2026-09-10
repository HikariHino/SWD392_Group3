using System.Threading.Tasks;

namespace Application.Interfaces.Services
{
    public interface IGradingService
    {
        Task<double> CalculateScoreAsync(string transcriptId, string rubricId);
    }
}
