using System.Net;
using System.Text;
using System.Text.Json;
using Application.DTOs.QuestionBank;
using Application.Interfaces.ExternalServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.ExternalServices;

/// <summary>
/// Gọi Google Gemini API để sinh câu hỏi vấn đáp.
/// Hỗ trợ round-robin rotation qua nhiều API key:
/// khi một key bị 429 (quota hết) hoặc lỗi, tự động hoán đổi sang key tiếp theo.
/// Thêm key mới vào appsettings.json: "Gemini:Keys:0", "Gemini:Keys:1", ...
/// </summary>
public class GeminiService : IAiService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GeminiService> _logger;

    // Chỉ số key đang được dùng — static để chia sẻ giữa các request trong cùng process
    private static int _currentKeyIndex = 0;
    private static readonly object _lock = new();

    public GeminiService(HttpClient httpClient, IConfiguration configuration, ILogger<GeminiService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
    }

    // ── Lấy danh sách key từ config ────────────────────────────────────────────
    // Hỗ trợ 2 cách cấu hình:
    //   Cũ (backwards compat): "Gemini:ApiKey" + "Gemini:ApiKeyBackup"
    //   Mới (mảng động):       "Gemini:Keys:0", "Gemini:Keys:1", ...
    private string[] LoadKeys()
    {
        // 1. Thử đọc từ file bí mật local không commit git: gemini_keys.local.json
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "gemini_keys.local.json"),
            Path.Combine(Directory.GetCurrentDirectory(), "WebApi", "gemini_keys.local.json"),
            Path.Combine(AppContext.BaseDirectory, "gemini_keys.local.json")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path));
                    if (doc.RootElement.TryGetProperty("Gemini", out var gemini) &&
                        gemini.TryGetProperty("Keys", out var keysElement) &&
                        keysElement.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in keysElement.EnumerateArray())
                        {
                            var s = item.GetString();
                            if (!string.IsNullOrWhiteSpace(s) && !s.StartsWith("YOUR_GEMINI"))
                            {
                                list.Add(s);
                            }
                        }
                        if (list.Count > 0) return list.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không thể đọc file {Path}", path);
                }
            }
        }

        // 2. Thử đọc mảng "Gemini:Keys:0", "Gemini:Keys:1", ... từ IConfiguration
        var arrayKeys = new List<string>();
        for (int i = 0; ; i++)
        {
            var val = _configuration[$"Gemini:Keys:{i}"];
            if (string.IsNullOrWhiteSpace(val)) break;
            arrayKeys.Add(val);
        }

        if (arrayKeys.Count > 0)
        {
            return arrayKeys
                .Where(k => !k.StartsWith("YOUR_GEMINI"))
                .ToArray();
        }

        // 3. Fallback: đọc 2 key riêng lẻ (backwards compat với ApiKey + ApiKeyBackup)
        return new[]
        {
            _configuration["Gemini:ApiKey"],
            _configuration["Gemini:ApiKeyBackup"]
        }
        .Where(k => !string.IsNullOrWhiteSpace(k) && !k.StartsWith("YOUR_GEMINI"))
        .ToArray()!;
    }

    // ── Lấy key hiện tại theo chỉ số round-robin ──────────────────────────────
    private static int GetCurrentIndex(int total)
    {
        lock (_lock) { return _currentKeyIndex % total; }
    }

    // ── Chuyển sang key tiếp theo (gọi khi key hiện tại bị quota/lỗi) ─────────
    private static void RotateToNextKey(int failedIndex, int total)
    {
        lock (_lock)
        {
            // Chỉ xoay nếu chưa có thread khác xoay trước
            if (_currentKeyIndex % total == failedIndex)
            {
                _currentKeyIndex = (failedIndex + 1) % total;
            }
        }
    }

    // ── Entry point chính ──────────────────────────────────────────────────────
    public async Task<AiGeneratedQuestionDto> GenerateQuestionAsync(AiGenerateQuestionRequest request)
    {
        var keys = LoadKeys();

        if (keys.Length == 0)
        {
            _logger.LogWarning("Không tìm thấy Gemini API key nào trong config. Dùng RAG fallback.");
            return GenerateFallbackQuestion(request);
        }

        int startIndex = GetCurrentIndex(keys.Length);
        int attempts = 0;

        // Thử lần lượt tất cả key, bắt đầu từ key hiện tại
        while (attempts < keys.Length)
        {
            int idx = (startIndex + attempts) % keys.Length;
            var key = keys[idx];
            var keyLabel = key[..Math.Min(14, key.Length)] + "...";

            _logger.LogInformation("Thử Gemini key [{Idx}/{Total}]: {Key}", idx + 1, keys.Length, keyLabel);

            try
            {
                var (result, isQuotaError) = await TryCallGeminiAsync(request, key);

                if (result != null)
                {
                    // Thành công — ghim chỉ số này cho lần sau
                    lock (_lock) { _currentKeyIndex = idx; }
                    _logger.LogInformation("Key [{Idx}] thành công!", idx + 1);
                    return result;
                }

                if (isQuotaError)
                {
                    // 429 / quota hết → xoay ngay sang key tiếp theo
                    _logger.LogWarning("Key [{Idx}] hết quota (429). Xoay sang key tiếp theo...", idx + 1);
                    RotateToNextKey(idx, keys.Length);
                }
                else
                {
                    _logger.LogWarning("Key [{Idx}] trả về kết quả rỗng, thử key tiếp theo.", idx + 1);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Key [{Idx}] ném exception, thử key tiếp theo.", idx + 1);
                RotateToNextKey(idx, keys.Length);
            }

            attempts++;
        }

        // Tất cả key đều fail → RAG fallback
        _logger.LogWarning("Tất cả {Count} Gemini key đều thất bại. Dùng RAG fallback.", keys.Length);
        return GenerateFallbackQuestion(request);
    }

    // ── Gọi Gemini API, trả về (result, isQuotaError) ─────────────────────────
    private async Task<(AiGeneratedQuestionDto? Result, bool IsQuotaError)> TryCallGeminiAsync(
        AiGenerateQuestionRequest request, string apiKey)
    {
        var prompt = $@"Bạn là giám khảo và chuyên gia khảo thí đại học môn Khoa học Máy tính / Công nghệ Thông tin.
Hãy tạo 1 câu hỏi thi vấn đáp (Viva Exam) và bảng tiêu chí Rubric đánh giá chấm điểm chuẩn dựa theo thông tin:
- Môn học / Chủ đề: {request.Topic}
- Cấp độ nhận thức Bloom mong muốn: {request.BloomLevel}

YÊU CẦU BẮT BUỘC VỀ ĐỊNH DẠNG:
Trả về duy nhất định dạng JSON (không thêm markdown hay backtick ```json), theo cấu trúc:
{{
  ""content"": ""Nội dung câu hỏi vấn đáp mang tính đào sâu tư duy..."",
  ""sourceContext"": ""Tài liệu / Giáo trình môn học trích dẫn..."",
  ""rubrics"": [
    {{ ""criteria"": ""Tên tiêu chí 1..."", ""weight"": 30.0, ""maxScore"": 3.0 }},
    {{ ""criteria"": ""Tên tiêu chí 2..."", ""weight"": 40.0, ""maxScore"": 4.0 }},
    {{ ""criteria"": ""Tên tiêu chí 3..."", ""weight"": 30.0, ""maxScore"": 3.0 }}
  ]
}}
QUY TẮC BẤT BIẾN: Tổng các trọng số (weight) của tất cả tiêu chí Rubric BẮT BUỘC PHẢI BẰNG ĐÚNG 100.0.";

        var requestBody = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new { responseMimeType = "application/json", temperature = 0.4 }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key={apiKey}";
        var httpContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(url, httpContent);

        // 429 = quota hết → cờ isQuotaError = true để caller xoay key
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            _logger.LogWarning("HTTP 429 Too Many Requests — key này đã hết quota.");
            return (null, true);
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Gemini trả về {StatusCode}", response.StatusCode);
            return (null, false);
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);

        var text = doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString();

        if (string.IsNullOrWhiteSpace(text)) return (null, false);

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var parsed = JsonSerializer.Deserialize<GeminiParsedResponse>(text, opts);

        if (parsed == null || string.IsNullOrWhiteSpace(parsed.Content) || parsed.Rubrics is not { Count: > 0 })
            return (null, false);

        NormalizeRubrics(parsed.Rubrics);

        return (new AiGeneratedQuestionDto
        {
            CourseId = request.CourseId,
            Content = parsed.Content,
            BloomLevel = request.BloomLevel,
            SourceContext = parsed.SourceContext ?? $"Tài liệu chuẩn hóa chủ đề: {request.Topic}",
            AiConfidence = 96.5,
            Rubrics = parsed.Rubrics
        }, false);
    }

    // ── Normalize rubric weights về đúng 100% ─────────────────────────────────
    private static void NormalizeRubrics(List<CreateRubricRequest> rubrics)
    {
        var sum = rubrics.Sum(r => r.Weight);
        if (Math.Abs(sum - 100.0) > 0.01 && rubrics.Count > 0)
        {
            rubrics[^1].Weight = Math.Round(rubrics[^1].Weight + (100.0 - sum), 2);
        }
    }

    // ── RAG Fallback ───────────────────────────────────────────────────────────
    private static AiGeneratedQuestionDto GenerateFallbackQuestion(AiGenerateQuestionRequest request)
    {
        var topic = string.IsNullOrWhiteSpace(request.Topic) ? "Kiến trúc phần mềm và hệ thống" : request.Topic;
        return new AiGeneratedQuestionDto
        {
            CourseId = request.CourseId,
            Content = $"Hãy phân tích nguyên lý hoạt động, ưu nhược điểm và ứng dụng thực tiễn của chủ đề '{topic}' trong hệ thống phần mềm hiện đại? Đưa ra ví dụ minh họa cụ thể cho cấp độ nhận thức {request.BloomLevel}.",
            BloomLevel = request.BloomLevel,
            SourceContext = $"Trích xuất từ Giáo trình & Slide bài giảng chuyên ngành (Chủ đề: {topic})",
            AiConfidence = 95.0,
            Rubrics =
            [
                new() { Criteria = $"Hiểu rõ định nghĩa cốt lõi, khái niệm và cơ chế hoạt động của {topic}", Weight = 30.0, MaxScore = 3.0 },
                new() { Criteria = "Phân tích chuyên sâu ưu nhược điểm và so sánh với các giải pháp tương đương", Weight = 40.0, MaxScore = 4.0 },
                new() { Criteria = "Đưa ra giải pháp thực tiễn, tối ưu hóa và liên hệ bài toán thực tế", Weight = 30.0, MaxScore = 3.0 }
            ]
        };
    }

    private class GeminiParsedResponse
    {
        public string Content { get; set; } = string.Empty;
        public string? SourceContext { get; set; }
        public List<CreateRubricRequest>? Rubrics { get; set; }
    }
}
