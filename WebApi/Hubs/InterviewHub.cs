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

        // C15 persists the answer first. C18 adds Hub authorization and ownership checks.
        public async Task RequestFollowUp(Guid attemptId)
        {
            var followUp = await _interviewService.GenerateFollowUpAsync(attemptId, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveAIFollowUp", followUp, Context.ConnectionAborted);
        }
    }
}
