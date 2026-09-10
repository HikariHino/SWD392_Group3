using System.Threading.Tasks;
using Application.Interfaces.Services;

namespace Application.Services
{
    public class GradingService : IGradingService
    {
        public async Task<double> CalculateScoreAsync(string transcriptId, string rubricId)
        {
            // Logic so sánh transcript với rubric để chấm điểm
            return await Task.FromResult(8.5);
        }
    }
}
