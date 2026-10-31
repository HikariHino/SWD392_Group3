# AIVES Backend

.NET 10 Web API cho hệ thống vấn đáp. Backend dùng Onion Architecture, SQL Server, JWT, Gemini cho câu hỏi follow-up/đánh giá, và Azure AI Speech cho luồng trả lời bằng giọng nói. Hợp đồng tiến độ/ownership nằm trong [docs/BACKEND_PROGRESS.md](docs/BACKEND_PROGRESS.md); thiết kế schema và quy ước API xem [docs/BACKEND_CONTRACTS.md](docs/BACKEND_CONTRACTS.md).

## Kiến trúc

- `Domain`: entity, enum và quy tắc lõi.
- `Application`: DTO, service/use case, validation, interface repository/provider.
- `Infrastructure`: EF Core/SQL Server, repository, migration và adapter Gemini/Azure Speech.
- `WebApi`: controller, JWT, SignalR, middleware và composition root.

Dependency hướng vào trong: Domain không phụ thuộc tầng ngoài; Application khai báo interface; Infrastructure triển khai; WebApi nối dependency qua DI.

## Chạy local

Cần .NET 10 SDK. `WebApi` dùng User Secrets (`UserSecretsId` đã cấu hình); không đặt password, JWT key hay API key trong source/appsettings.

```powershell
cd WebApi
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=(localdb)\MSSQLLocalDB;Database=AIVES_Local;Trusted_Connection=True;TrustServerCertificate=True"
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(48)))
dotnet user-secrets set "Gemini:ApiKey" "<Gemini API key>"
dotnet user-secrets set "AzureSpeech:SubscriptionKey" "<Azure AI Speech key>"
dotnet user-secrets set "AzureSpeech:SpeechToTextEndpoint" "https://<speech-resource>.cognitiveservices.azure.com/"
cd ..
dotnet run --project WebApi/WebApi.csproj --launch-profile http
```

Swagger mở tại `http://localhost:5110/` theo profile HTTP. JWT `Issuer`/`Audience` có giá trị mặc định; signing key phải được cấu hình ngoài source và dài tối thiểu 32 UTF-8 bytes. CORS mặc định cho phép Vite `http://localhost:5173` và React `http://localhost:3000`.

### Database và migrations

Mặc định startup **không** chạy migration hoặc seed. Với database local mới, có thể bật `Database:ApplyMigrationsOnStartup=true` trong user-secrets rồi chạy API. Chỉ bật với database riêng đã xác định; không bật cho database Azure/shared trước khi review migration và backup. Tắt lại sau khi migration xong. Để tạo migration thủ công:

```powershell
dotnet ef database update --project Infrastructure/Infrastructure.csproj --startup-project WebApi/WebApi.csproj
```

Các migration hiện có trong `Infrastructure/Migrations`. Seeder chỉ chạy khi Development và `Database:SeedDevelopmentData=true`, sau khi schema đã được migrate. Seed dữ liệu mẫu dùng tài khoản giảng viên demo `lecturer` / `password123`; chỉ dành cho DB local dùng một mình, đổi/xóa trước khi chia sẻ. `GET /health/live` xác nhận process đang chạy; `GET /health/ready` kiểm tra khả năng kết nối DB và trả 503 khi DB chưa sẵn sàng.

### Speech configuration

Azure Speech adapter dùng [REST fast transcription](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/fast-transcription-create) (API `2025-10-15`) cho STT; mặc định locale `vi-VN`. TTS dùng [REST text-to-speech](https://learn.microsoft.com/en-us/azure/ai-services/speech-service/rest-text-to-speech), mặc định giọng `vi-VN-HoaiMyNeural` và WAV PCM 24 kHz mono. Có thể ghi đè `AzureSpeech:Region`, `Locale`, `Voice`, `OutputFormat`, `TimeoutSeconds` bằng user-secrets/environment. STT nhận WAV/MP3/OGG/WebM/MP4 tối đa 10 MiB; TTS tối đa 2.000 ký tự. Chưa có key/resource thì API voice trả lỗi provider 503; phần text vẫn dùng endpoint hiện có. Không commit speech key.

## API chính

Swagger là hợp đồng chi tiết, các route chính:

| Chức năng | Route |
|---|---|
| Login / user hiện tại | `POST /api/auth/login`, `GET /api/auth/me` |
| Question bank, course, course rubric | `GET /api/questions/courses`, `GET/POST /api/questions`, `PUT/DELETE /api/questions/{id}`, `PUT /api/courses/{id}/rubrics` |
| CSV question import | `POST /api/questions/import` (`multipart/form-data`: `courseId`, `file`) |
| Tạo/mở/đóng/hủy exam session | `/api/exam-sessions` |
| Bắt đầu attempt, xem câu hỏi/transcript | `POST /api/exam-sessions/{id}/attempts`, `GET /api/attempts/{id}/questions`, `GET /api/attempts/{id}/turns` |
| Trả lời text, follow-up text, nộp bài | `POST /api/attempts/{id}/answers`, `POST /api/attempts/{id}/follow-up`, `POST /api/attempts/{id}/submit` |
| Trả lời bằng giọng nói | `POST /api/attempts/{id}/voice-answers` (`multipart/form-data`: `questionId`, `clientMessageId`, `audio`) |
| Chấm/duyệt và kết quả | `POST /api/attempts/{id}/grade`, `GET /api/attempts/{id}/assessments`, `PUT /api/assessments/{id}/final-score`, `GET /api/attempts/{id}/result` |

Các route cần JWT theo role được Swagger đánh dấu. Voice response trả transcript, student turn, AI follow-up turn, `audioBase64` và `audioContentType`; nếu TTS lỗi sau khi lượt AI đã lưu, vẫn trả text/turn với `speechOutputUnavailable=true`. `clientMessageId` dùng để retry idempotent; client nên giữ nguyên id khi retry. Upload giọng nói xác thực Student, quyền sở hữu attempt, trạng thái phiên và question trước khi gửi audio sang provider. Text answer/follow-up vẫn hoạt động nếu không cấp quyền microphone hoặc Azure Speech tạm lỗi.

### SignalR

Hub URL là `/interviewHub`, dùng JWT bearer. Client gửi `RequestFollowUp(attemptId)`; server kiểm tra role/owner/trạng thái, lưu AI turn và gửi `ReceiveAIFollowUp` cho kết nối gọi. HTTP `POST /api/attempts/{id}/follow-up` vẫn là phương án thay thế khi SignalR chưa kết nối.

## Build và checks

```powershell
dotnet restore SWD392_Group3.slnx
dotnet build SWD392_Group3.slnx --configuration Release --no-restore
dotnet run --project tests/BackendChecks/BackendChecks.csproj --configuration Release
```

GitHub Actions workflow `.github/workflows/backend.yml` chạy restore, Release build và `BackendChecks` trên .NET 10, không cần SQL, Gemini hay Azure Speech secret. Các provider checks dùng HTTP handler giả. Tùy chọn `--sql-server` chạy nhóm kiểm tra riêng cần SQL Server được cấu hình.

## Demo acceptance

1. Tạo User/Student và Lecturer riêng; đăng nhập, lấy bearer token.
2. Lecturer tạo course/rubric cấp môn, question và exam session; mở session.
3. Student bắt đầu attempt, trả lời text; kiểm tra transcript được lưu và gọi follow-up.
4. Với Azure Speech resource đã cấu hình, gửi voice multipart; kiểm tra transcript, student turn, AI turn, WAV audio. Retry cùng `clientMessageId` không tạo thêm student turn/AI call. Tắt quyền mic hoặc bỏ speech config để xác nhận luồng text còn dùng được.
5. Nộp bài; Lecturer chấm, xem đề xuất, chốt điểm; tải result/CSV nếu cần.
6. Mở hai tab client để xác nhận SignalR `ReceiveAIFollowUp`; kiểm tra health live/ready.

Các kiểm tra SQLite/mocked HTTP không thay thế demo Azure SQL/Gemini/Azure Speech thật. Chưa coi migration trên Azure database, Azure credential, CORS ngoài localhost, audio thiết bị người dùng hoặc SignalR trên môi trường deploy là đã nghiệm thu nếu chưa chạy các thao tác đó trên môi trường tương ứng. Appsettings mặc định không chứa secret; nếu repo cũ từng chứa credential trong lịch sử Git, cần rotate credential đó tại provider.
