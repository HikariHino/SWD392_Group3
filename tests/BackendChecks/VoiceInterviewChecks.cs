using Application.DTOs.Interviews;
using Application.Exceptions;
using Application.Interfaces.ExternalServices;
using Application.Interfaces.Services;
using Application.Services;

internal static class VoiceInterviewChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var attemptId = Guid.NewGuid(); var questionId = Guid.NewGuid(); var clientId = Guid.NewGuid(); var studentId = Guid.NewGuid();
        var student = new TurnDto(Guid.NewGuid(), attemptId, 1, "Student", "Câu trả lời", DateTime.UtcNow, questionId, clientId);
        var ai = new FollowUpTurnDto(Guid.NewGuid(), attemptId, 2, "Hãy giải thích thêm.", DateTime.UtcNow, questionId);

        var forbiddenStt = new FakeStt();
        var forbidden = new VoiceInterviewService(new FakeUser(false, null), new FakeAttempts(null, []), new FakeInterview(ai), forbiddenStt, new FakeTts(false));
        var denied = false;
        try { await forbidden.AnswerAsync(attemptId, questionId, clientId, Clip()); } catch (ForbiddenException) { denied = true; }
        check(denied && forbiddenStt.Calls == 0, "voice answer rejects non-student before STT");

        var stt = new FakeStt(); var attempts = new FakeAttempts(student, []); var interviews = new FakeInterview(ai);
        var flow = new VoiceInterviewService(new FakeUser(true, studentId), attempts, interviews, stt, new FakeTts(false));
        var result = await flow.AnswerAsync(attemptId, questionId, clientId, Clip());
        check(result.Transcript == "Câu trả lời" && attempts.AnswerCalls == 1 && interviews.Calls == 1, "voice flow transcribes, persists answer and creates AI follow-up");
        check(result.AudioContentType == "audio/wav" && result.AudioBase64 == Convert.ToBase64String([1, 2]), "voice flow returns WAV payload for frontend");

        var retryStudent = student with { ContentText = "Câu đã lưu" };
        var retryAi = new TurnDto(ai.Id, attemptId, 2, "AI", ai.ContentText, ai.Timestamp, questionId, null);
        var retryStt = new FakeStt(); var retryInterview = new FakeInterview(ai);
        var retry = new VoiceInterviewService(new FakeUser(true, studentId), new FakeAttempts(retryStudent, [retryStudent, retryAi]), retryInterview, retryStt, new FakeTts(false));
        var retryResult = await retry.AnswerAsync(attemptId, questionId, clientId, Clip());
        check(retryStt.Calls == 0 && retryInterview.Calls == 0 && retryResult.AiTurn?.ContentText == ai.ContentText, "voice retry reuses persisted turns without duplicate provider calls");

        var fallback = new VoiceInterviewService(new FakeUser(true, studentId), new FakeAttempts(student, []), new FakeInterview(ai), new FakeStt(), new FakeTts(true));
        var fallbackResult = await fallback.AnswerAsync(attemptId, questionId, clientId, Clip());
        check(fallbackResult.SpeechOutputUnavailable && fallbackResult.AudioBase64 is null && fallbackResult.AiTurn is not null, "TTS outage keeps persisted text answer and AI response");
    }

    static SpeechAudioClip Clip() => new([.. "RIFF0000WAVEdata"u8.ToArray()], "a.wav", "audio/wav");
    sealed class FakeUser(bool student, Guid? id) : ICurrentUser { public Guid? Id => id; public bool IsStudent => student; public bool IsLecturer => false; }
    sealed class FakeStt : ISpeechToTextService
    {
        public int Calls { get; private set; }
        public Task<SpeechTranscription> TranscribeAsync(SpeechAudioClip audio, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(new SpeechTranscription("Câu trả lời", "vi-VN")); }
    }
    sealed class FakeTts(bool fail) : ITextToSpeechService
    {
        public Task<SpeechAudio> SynthesizeAsync(string text, CancellationToken cancellationToken = default) => fail
            ? Task.FromException<SpeechAudio>(new AiProviderUnavailableException("offline"))
            : Task.FromResult(new SpeechAudio([1, 2], "audio/wav", "a.wav"));
    }
    sealed class FakeInterview(FollowUpTurnDto result) : IInterviewService
    {
        public int Calls { get; private set; }
        public Task<FollowUpTurnDto> GenerateFollowUpAsync(Guid attemptId, CancellationToken cancellationToken = default) => Task.FromResult(result);
        public Task<FollowUpTurnDto> GenerateFollowUpForStudentAsync(Guid attemptId, Guid studentId, CancellationToken cancellationToken = default) { Calls++; return Task.FromResult(result); }
    }
    sealed class FakeAttempts(TurnDto? answer, IReadOnlyList<TurnDto> existingTurns) : IAttemptService
    {
        public int AnswerCalls { get; private set; }
        public Task<(AttemptDto Attempt, bool Created)> Start(Guid sessionId) => throw new NotImplementedException();
        public Task<AttemptDto> Get(Guid id) => throw new NotImplementedException();
        public Task<IReadOnlyList<AttemptQuestionDto>> Questions(Guid id) => throw new NotImplementedException();
        public Task<IReadOnlyList<TurnDto>> Turns(Guid id) => Task.FromResult(existingTurns);
        public Task<(TurnDto Turn, bool Created)> Answer(Guid id, AnswerRequest request) { AnswerCalls++; return Task.FromResult((answer!, true)); }
        public Task ValidateAnswerAsync(Guid id, Guid questionId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<TurnDto?> FindExistingAnswerAsync(Guid id, Guid questionId, Guid clientMessageId, CancellationToken cancellationToken = default) => Task.FromResult(existingTurns.FirstOrDefault(x => x.Speaker == "Student"));
        public Task<AttemptDto> Submit(Guid id) => throw new NotImplementedException();
    }
}
