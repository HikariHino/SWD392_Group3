namespace Application.Exceptions;

public abstract class AiProviderException(string message) : Exception(message);

public sealed class AiProviderUnavailableException(string message) : AiProviderException(message);
public sealed class AiProviderTimeoutException() : AiProviderException("The AI provider timed out.");
public sealed class AiProviderResponseException() : AiProviderException("The AI provider returned an invalid response.");
