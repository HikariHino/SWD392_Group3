using Microsoft.AspNetCore.Authorization;
using WebApi.Hubs;

internal static class HubSecurityChecks
{
    public static Task Run(Action<bool, string> check)
    {
        var authorize = typeof(InterviewHub).GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>().SingleOrDefault();
        check(authorize?.Roles == "Student", "interview hub requires authenticated Student role");
        var method = typeof(InterviewHub).GetMethod(nameof(InterviewHub.RequestFollowUp));
        check(method != null && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(Guid), "hub accepts only server-scoped attempt id, not student identity or answer text");
        return Task.CompletedTask;
    }
}
