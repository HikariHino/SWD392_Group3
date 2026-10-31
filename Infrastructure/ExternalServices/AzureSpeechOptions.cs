namespace Infrastructure.ExternalServices;

public sealed class AzureSpeechOptions
{
    public string SubscriptionKey { get; set; } = "";
    public string Region { get; set; } = "southeastasia";
    public string SpeechToTextEndpoint { get; set; } = "";
    public string Locale { get; set; } = "vi-VN";
    public string Voice { get; set; } = "vi-VN-HoaiMyNeural";
    public string OutputFormat { get; set; } = "riff-24khz-16bit-mono-pcm";
    public int TimeoutSeconds { get; set; } = 30;

    public static AzureSpeechOptions Load(Microsoft.Extensions.Configuration.IConfiguration configuration)
    {
        var section = configuration.GetSection("AzureSpeech");
        var options = new AzureSpeechOptions
        {
            SubscriptionKey = section[nameof(SubscriptionKey)] ?? "",
            Region = section[nameof(Region)] ?? "southeastasia",
            SpeechToTextEndpoint = section[nameof(SpeechToTextEndpoint)] ?? "",
            Locale = section[nameof(Locale)] ?? "vi-VN",
            Voice = section[nameof(Voice)] ?? "vi-VN-HoaiMyNeural",
            OutputFormat = section[nameof(OutputFormat)] ?? "riff-24khz-16bit-mono-pcm"
        };
        if (int.TryParse(section[nameof(TimeoutSeconds)], out var timeout)) options.TimeoutSeconds = timeout;
        return options;
    }
}
