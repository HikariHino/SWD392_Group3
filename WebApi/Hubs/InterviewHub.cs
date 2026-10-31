using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Application.Interfaces.Services;
using System.Threading.Tasks;

namespace WebApi.Hubs
{
    [Authorize(Roles = "Student")]
    public class InterviewHub : Hub
    {
        private readonly IInterviewService _interviewService;

        // Bơm Service từ tầng Application vào (Dependency Injection)
        public InterviewHub(IInterviewService interviewService)
        {
            _interviewService = interviewService;
        }

        // C15 persists the answer first. The service derives ownership from the authenticated principal.
        public async Task RequestFollowUp(Guid attemptId)
        {
            if (!Guid.TryParse(Context.User?.FindFirst("sub")?.Value, out var studentId))
                throw new HubException("Authentication required.");
            var followUp = await _interviewService.GenerateFollowUpForStudentAsync(attemptId, studentId, Context.ConnectionAborted);
            await Clients.Caller.SendAsync("ReceiveAIFollowUp", followUp, Context.ConnectionAborted);
        }
    }
}
