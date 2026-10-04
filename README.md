# 🎓 AIVES - Backend API Service

> Tiến độ thực tế, lỗi đang mở và quy tắc nhận việc backend: [docs/BACKEND_PROGRESS.md](docs/BACKEND_PROGRESS.md). Đọc và cập nhật file này trước/sau mỗi phiên coding chung.

> Từ C11, cần cấu hình JWT ngoài source trước khi khởi động WebApi. Xem mục **C11: thiết lập đăng nhập JWT** cuối file. Thiếu cấu hình sẽ khiến startup dừng.

> **SWD392 (Software Architecture and Design) - Group 3**  
> An intelligent oral examination platform (AI-powered Viva Exam System) built with **.NET 10**, **Onion Architecture**, **SignalR**, and **OpenAI / Azure Speech**.

---

## 🏛️ System Architecture: Onion Architecture (cấu trúc mục tiêu)

Dự án áp dụng chặt chẽ mô hình **Onion Architecture (Clean Architecture)** nhằm bảo vệ phần Lõi nghiệp vụ (Domain & Application) độc lập hoàn toàn với Cơ sở dữ liệu và các API bên ngoài:

```text
SWD392_Group3/
├── Domain/                           # Enterprise Core (POCO, Zero Dependencies)
│   ├── Common/                       # BaseEntity, AuditableEntity
│   ├── Entities/                     # User, Question, Rubric, ExamSession, Transcript, Assessment
│   ├── Enums/                        # BloomLevel, ExamStatus, UserRole
│   └── Exceptions/                   # Custom Domain Exceptions
│
├── Application/                      # Business Logic Layer
│   ├── DTOs/                         # Data Transfer Objects
│   ├── Interfaces/                   # Abstractions & Contracts (DIP)
│   │   ├── Services/                 # IQuestionBankService, IInterviewService, IGradingService
│   │   ├── Repositories/             # Repository Contracts
│   │   └── ExternalServices/         # IOpenAIService, ISpeechService
│   ├── Mappings/                     # AutoMapper Profiles
│   └── Services/                     # QuestionBankService, InterviewService, GradingService
│
├── Infrastructure/                   # Concrete Technical Implementations
│   ├── Persistence/                  # EF Core, AivesDbContext, Configurations
│   ├── Repositories/                 # Repositories Implementation (DIP)
│   └── ExternalServices/             # OpenAIService (GPT-4), AzureSpeechService (STT/TTS)
│
└── WebApi/                           # Presentation Layer
    ├── Controllers/                  # RESTful API Endpoints (Auth, Questions, Grading)
    ├── Hubs/                         # SignalR Real-Time Hub (InterviewHub)
    ├── appsettings.json              # Connection Strings & Configs
    └── Program.cs                    # IoC Dependency Injection Container
```

---

## 🚀 Key Features (Milestone 1 Scope — mục tiêu triển khai)

* **Feature 1 - Question Bank & Rubric Management:**
  * Quản lý ngân hàng câu hỏi phân loại theo mức độ nhận thức (Bloom Level).
  * Cấu hình thang điểm và tiêu chí đánh giá (Rubric Criteria) đi kèm.
  * Thao tác dữ liệu qua Entity Framework Core trên SQL Server.

* **Feature 3 - Real-Time AI Viva Exam Core:**
  * Giao tiếp 2 chiều thời gian thực với **SignalR (WebSockets Full-Duplex)**.
  * Nhận diện giọng nói sinh viên (Speech-to-Text) qua Azure Speech Service.
  * AI phản xạ sinh câu hỏi xoáy thích ứng (OpenAI GPT-4) và tổng hợp giọng nói phản hồi (Text-to-Speech).

* **Feature 4 - AI Grading & Lecturer Decision (Human-in-the-loop):**
  * Tự động đối chiếu toàn bộ Transcript ca thi với tiêu chí Rubric.
  * Đề xuất điểm số và nhận xét chi tiết; Giảng viên xem xét và chốt điểm chính thức.

---

## ⚙️ Getting Started & Local Setup

Hiện có code CRUD User/Question/Rubric, danh sách Course và Swagger. Import/AI/interview/grading vẫn mock; Auth, Speech và entity ca thi/kết quả chưa triển khai. Cây kiến trúc phía trên mô tả cả phần dự kiến. Đọc [hợp đồng database/API C03](docs/BACKEND_CONTRACTS.md) trước khi đổi entity, migration hoặc DTO.

### Chính sách SDK và chạy trong Visual Studio — C02

`global.json` yêu cầu SDK stable **10.0.401 trở lên trong dòng 10.0**, cho phép feature band mới hơn, không tự chuyển sang .NET 11 hoặc preview. Cả 4 project target net10.0. Xem [chính sách global.json của Microsoft](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json).

Visual Studio 2026: mở `SWD392_Group3.slnx`, cài workload ASP.NET and web development, chọn **WebApi → Set as Startup Project**, chọn profile https rồi F5. Domain/Application/Infrastructure là thư viện. Kiểm tra `dotnet --version` và `dotnet --list-sdks`; nếu build báo DLL đang được dùng, dừng WebApi trước khi build.

Từ gốc repo có thể dùng:

```powershell
dotnet restore SWD392_Group3.slnx
dotnet build SWD392_Group3.slnx --no-restore
dotnet dev-certs https --trust
$env:ConnectionStrings__DefaultConnection = 'Server=(localdb)\MSSQLLocalDB;Database=AIVES_Quang_Dev;Trusted_Connection=True;TrustServerCertificate=True'
dotnet run --project WebApi/WebApi.csproj --launch-profile https
```

Dùng instance SQL Server và DB dev riêng của mỗi người; biến môi trường trên chỉ áp dụng cho terminal/tiến trình con đó. Visual Studio cần nhận biến môi trường trước khi mở hoặc dùng cấu hình local riêng. Không commit secret hay cấu hình máy cá nhân.

Startup Development hiện gọi `EnsureCreatedAsync()` rồi seed Course/Question; **không tự áp dụng migration**. InitialCreate chỉ có Courses/Questions/Rubrics; AddUserTable của Thai bổ sung Users. DB tạo bằng EnsureCreated không tự có migration history. Chưa chạy migration lên database có sẵn trước khi đối chiếu C03/C07; không xóa DB hoặc baseline đè dữ liệu. `[DatabaseSeeder Error]` không ngăn Swagger mở, nên cần kiểm tra CRUD riêng.

Thai đã bổ sung C04–C07: hash password, middleware lỗi, User validator và migration Users. Kiểm tra bổ sung không cần SQL:

```powershell
dotnet run --project tests/BackendChecks/BackendChecks.csproj
```

Kiểm tra hash/verify, validator, lỗi 400/404/409/500 có traceId, migration discovery/SQL và snapshot. Exit code khác 0 khi thất bại. Chưa thay thế kiểm tra migration/CRUD SQL thật; C07 cần xác minh trên DB dev riêng. Quyền cấp/đổi role được bảo vệ tại C12.

C09 thêm SQLite in-memory để kiểm tra lưu/thay rubric và soft delete, không kết nối DB của bạn. Có 79 checks đã pass ngày 04/10; hai checks query relational thêm sau chưa chạy được do Windows Application Control. Nếu gặp 0x800711C7, ghi nhận test bị chặn, không xem là pass hoặc tắt chính sách bảo vệ để vượt kiểm tra.

C10 có code CRUD Course ở `/api/courses`; `/api/questions/courses` trả CourseDto cùng kiểu với route mới. Tạo/cập nhật chuẩn hóa code uppercase, kiểm tra trùng môn active và độ dài input; xóa môn có câu hỏi active trả 409, còn lại soft-delete. Runtime tests C10 đang bị Application Control chặn; endpoint chưa có auth (C12). Không dùng API quản lý này như bản production.

Profile https dùng **https://localhost:7035** và **http://localhost:5110**; HTTP có thể redirect HTTPS. Swagger Development ở `/`, JSON ở `/swagger/v1/swagger.json`, SignalR ở `/interviewHub`. Profile http chỉ nghe 5110. Luôn dùng cổng trong log `Now listening on`.

Kiểm tra: mở Swagger, gọi GET `/api/questions/courses` và GET `/api/questions` để kiểm tra DB. User cần schema Users phù hợp. Import còn mock nên không dùng làm bằng chứng hoàn thành. Nếu HTTPS bị chặn, trust dev certificate rồi khởi động lại.

### 1. Prerequisites

* [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
* [SQL Server (LocalDB / Express / Developer)](https://www.microsoft.com/sql-server)

### 2. Database Configuration

Mở file `WebApi/appsettings.json` và cập nhật chuỗi kết nối SQL Server:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=AIVES_Db;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

### 3. Run the Backend API

Mở Terminal và chạy các lệnh sau:

```bash
# Di chuyển vào thư mục WebApi
cd WebApi

# Cài đặt các gói NuGet
dotnet restore

# Build dự án
dotnet build

# Khởi chạy Server
dotnet run --launch-profile https
```

Sau khi chạy thành công, hệ thống sẵn sàng phục vụ tại:

* **API Base URL:** `https://localhost:7035`
* **SignalR Hub Endpoint:** `https://localhost:7035/interviewHub`
* **OpenAPI / Swagger:** `https://localhost:7035/`

---

Trần Thường Quang - QuangTT SE196220
Vương Hoàng Giang - GiangVH SE193455

## C11: thiết lập đăng nhập JWT

WebApi yêu cầu JWT config trước startup. Development dùng user-secrets (không commit vào repo). Chạy PowerShell tại thư mục solution:

```powershell
dotnet user-secrets set "Jwt:Issuer" "AIVES" --project WebApi
dotnet user-secrets set "Jwt:Audience" "AIVES-Client" --project WebApi
$aivesSigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(48))
dotnet user-secrets set "Jwt:SigningKey" "$aivesSigningKey" --project WebApi
dotnet user-secrets set "Jwt:ExpiryMinutes" "30" --project WebApi
dotnet run --project WebApi --launch-profile https
```

Mỗi máy dev tự tạo khóa riêng; không gửi khóa qua note/commit. Môi trường triển khai dùng secret store hoặc environment `Jwt__Issuer`, `Jwt__Audience`, `Jwt__SigningKey`, `Jwt__ExpiryMinutes`. Khóa cần ít nhất 32 UTF-8 bytes; thời hạn 1–120 phút, mặc định 30. Thiếu/sai config thì startup dừng với hướng dẫn. Xem [hướng dẫn JWT Bearer của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0).

POST `/api/auth/login` với `{"username":"<user đã có trong DB>","password":"<mật khẩu>"}`. Thành công trả `accessToken`, `expiresAt`, `user` (không có password hash). GET `/api/auth/me` với header `Authorization: Bearer <accessToken>` trả id/username/role. Login sai trả 401 ProblemDetails, input sai 400; token thiếu/sai/hết hạn trả 401. Từ C12, Swagger tự gửi Bearer cho endpoint được bảo vệ và mỗi request đối chiếu account/role với DB.

Tài khoản cần BCrypt hash hợp lệ; plaintext cũ không đăng nhập được. C11/C12 không tự tạo tài khoản hay chạy migration mới. C12 đã bảo vệ API quản lý theo role và từ chối token khi account bị xóa/đổi role. Chưa có refresh token hoặc danh sách revoke riêng.

Kiểm chứng ngày 04/10/2026: `dotnet build` 0 warning/error; `dotnet run --project tests/BackendChecks` **134 checks PASS** (HTTP login/me, BCrypt/JWT/Bearer và SQLite C08–C10). Auth tests dùng repository giả, không cần user-secrets hay SQL Server. Migration và login trên SQL Server dev thật vẫn cần kiểm chứng C07.

## C12: demo CRUD M1 bằng Swagger hoặc FE

C12 đã có **261 checks PASS**, gồm HTTP + SQLite CRUD User/Course/Question-Rubric và phân quyền; SQL Server thật/Gate M1 vẫn cần nhóm xác minh C07. Các mốc C09–C12 hiện nằm trên `quang`; peer cần lấy nhánh có các commit này để tích hợp.

Trước demo, cấu hình JWT ở trên và connection string tới DB dev đã có schema đúng. Nhóm cần provision tài khoản Lecturer đầu tiên với BCrypt hash qua quy trình DB dev được kiểm soát. API tạo user yêu cầu Lecturer; không có public signup hay tài khoản/mật khẩu mặc định.

1. Chạy WebApi với profile `https`, mở Swagger ở `https://localhost:7035/` (JSON `/swagger/v1/swagger.json`). Dùng URL thực tế trong log nếu đổi profile/port.
2. POST `/api/auth/login` với tài khoản Lecturer. Copy `accessToken`, bấm **Authorize**, dán riêng token (không thêm chữ `Bearer`), xác nhận. Swagger tự thêm header vào API cần xác thực.
3. POST `/api/courses` với `{"code":"DEMO392","name":"Môn demo"}`; copy `id` trả về.
4. POST `/api/questions` với JSON dưới đây, thay courseId bằng id vừa tạo; thử GET list/detail, PUT thay câu hỏi/rubric, DELETE câu hỏi rồi DELETE môn. Môn còn câu hỏi hoạt động sẽ trả 409 khi xóa.
5. Thử User CRUD với Lecturer; tạo Student rồi login Student và Authorize lại. Student đọc Course, GET user chính mình và PUT `{"fullName":"Tên mới"}` được; đọc user khác/ngân hàng câu hỏi, tạo/xóa hoặc gửi Role bị 403. Bỏ token trả 401. Lecturer không được đổi role chính mình, được cấp/đổi role người khác.

```json
{
  "courseId": "<id từ bước tạo môn>",
  "content": "Giải thích Onion Architecture",
  "bloomLevel": 2,
  "rubrics": [{ "criteria": "Giải thích đúng", "weight": 100, "maxScore": 10 }]
}
```

FE repo riêng dùng base URL backend, gọi login, gửi `Authorization: Bearer <accessToken>` ở các request tiếp theo. CORS hiện cho phép `http://localhost:5173`, `http://localhost:3000`, `https://localhost:5173`; port/origin khác cần cập nhật cấu hình CORS. FE xử lý 401 bằng login lại, 403 bằng thông báo thiếu quyền. Role đổi hoặc account deleted khiến token hiện tại bị từ chối từ request kế tiếp.

Quyền và ownership User được kiểm tra tại Application qua ICurrentUser; ASP.NET chỉ cung cấp identity đã xác thực. Xem [ma trận quyền C12](docs/BACKEND_CONTRACTS.md#72-c12-http-authorization-đã-triển-khai-04102026) và [hướng dẫn role authorization của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/roles?view=aspnetcore-10.0). Import file còn placeholder (C25), Hub/thi AI/chấm điểm nằm ở các mốc tiếp theo; chưa thuộc demo CRUD M1.
