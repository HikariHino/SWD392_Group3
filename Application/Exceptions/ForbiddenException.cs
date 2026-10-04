namespace Application.Exceptions;

public sealed class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to perform this action.") { }
}
