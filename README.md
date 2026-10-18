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

## 📌 Báo cáo đóng góp & Công việc thực hiện trong Milestone 3 — Nguyễn Bùi Đăng Khôi

> **Họ và tên:** Nguyễn Bùi Đăng Khôi  
> **Vai trò:** Architecture Lead & Core Backend Engineer  
> **Phạm vi Milestone 3:** Thiết kế & chuẩn hóa nền tảng Onion Architecture (.NET 10), triển khai hoàn chỉnh luồng nghiệp vụ cốt lõi Flow 1 (Question Bank & Rubric Matrix) phục vụ Live Demo, mô hình hóa toàn diện hệ thống UML (Sequence & State Machine Diagrams) cho 3 flows (1, 3, 4), và biên soạn Báo cáo Thiết kế Kiến trúc Phần mềm (Software Architecture Design Report).

Mục này ghi lại chi tiết toàn bộ các hạng mục công việc kỹ thuật, kiến trúc và tài liệu mà **Nguyễn Bùi Đăng Khôi** đã thực hiện để các thành viên trong nhóm, giảng viên hướng dẫn (thầy Phan Minh Tâm) và hội đồng thẩm định tiện theo dõi, đối chiếu.

### 1. Xây dựng nền tảng Kiến trúc Hệ thống (Clean / Onion Architecture on .NET 10)
* **Khởi tạo và cấu trúc 4 tầng chuẩn Enterprise:**
  * `Domain`: Định nghĩa các POCO Entities độc lập hoàn toàn (`Question`, `RubricCriterion`, `Course`, `User`, `ExamSession`, `Transcript`, `Assessment`), các Enums chuẩn hóa (`BloomTaxonomy`, `UserRole`, `ExamStatus`) và Domain Exceptions. Tầng này có **0 external dependencies**.
  * `Application`: Định nghĩa hợp đồng nghiệp vụ (Contracts/Interfaces), DTOs, AutoMapper mapping profiles, các nghiệp vụ dịch vụ (`QuestionBankService`), và tích hợp thư viện **FluentValidation** để kiểm soát tính hợp lệ của dữ liệu đầu vào.
  * `Infrastructure`: Triển khai EF Core `ApplicationDbContext`, **Generic Repository Pattern** và **Unit of Work Pattern** giúp quản lý giao dịch nguyên tử (Atomic Transactions) cấp cơ sở dữ liệu qua `SaveChangesAsync()`. Tích hợp mã hóa mật khẩu BCrypt và cấu hình xác thực JWT Bearer.
  * `WebApi (Presentation)`: Xây dựng RESTful API Controllers, tích hợp bộ xử lý ngoại lệ tập trung (**Global Exception Handling Middleware**) chuẩn RFC 7807 (`ProblemDetails` có đính kèm `traceId`), cấu hình CORS cho phép kết nối Frontend và kích hoạt giao diện **Swagger UI trực tiếp tại root URL (`/`)**.
* **Chuẩn hóa môi trường phát triển:** Cấu hình chính sách `global.json` cho phép .NET 10 SDK roll-forward ổn định, thiết lập Dependency Injection tập trung, sạch sẽ trong `Program.cs`.

### 2. Hiện thực hóa Nghiệp vụ Cốt lõi Flow 1 (Question Bank & Rubric Management — Demo Flow)
* **Thiết kế & cài đặt trọn bộ API `/api/questions`:** Cung cấp đầy đủ các thao tác CRUD câu hỏi đi kèm danh sách tiêu chí Rubric tương ứng.
* **Xây dựng luật nghiệp vụ kiểm định ma trận Rubric nghiêm ngặt:**
  * Phân loại câu hỏi theo 6 bậc tư duy nhận thức của Thang đo Bloom (Bloom's Taxonomy).
  * Kiểm tra tính hợp lệ và trạng thái hoạt động (`IsActive`) của môn học (`CourseId`).
  * **Ràng buộc tiên quyết bất biến (Invariable Rule):** Tổng trọng số của toàn bộ tiêu chí Rubric thuộc câu hỏi bắt buộc phải bằng **100%** ($\sum \text{Rubric.Weight} = 100\%$). Nếu sai lệch dù chỉ $0.01\%$, request bị chặn ngay từ tầng Application và trả về lỗi `400 Bad Request` kèm thông báo chi tiết.
* **Cơ chế Xóa mềm (Soft Delete):** Triển khai cờ `IsDeleted = true` cho cả Question và Rubric Criterion, ngăn chặn việc xóa cứng trong DB làm phá hủy tính toàn vẹn dữ liệu và mất vết lịch sử chấm điểm của các kỳ thi đã diễn ra.
* **Đảm bảo tính nguyên tử (Atomicity):** Việc lưu Question và toàn bộ $N$ tiêu chí Rubric con được thực thi trong cùng một transaction của Unit of Work, bảo đảm không bao giờ xảy ra lỗi "câu hỏi đã lưu nhưng rubric bị thiếu".
* **Ý nghĩa đối với Milestone 3:** Đây là **Flow duy nhất được nhóm lựa chọn để thực hiện Live Demo** (tương tác trực quan từ giao diện React Frontend $\rightarrow$ Swagger API $\rightarrow$ CSDL SQL Server thật).

### 3. Quản trị Cơ sở dữ liệu & Đồng bộ Schema (Database & EF Core Migrations)
* Trực tiếp tạo lập, kiểm thử và đồng bộ các file Migrations của Entity Framework Core (`InitialCreate`, `AlignQuestionBankSchema`).
* Đồng bộ hóa Model Snapshot của EF Core với cấu trúc thực tế trên CSDL Azure SQL Database và SQL Server LocalDB, loại bỏ hoàn toàn hiện tượng lệch schema (schema drift).
* Giải quyết các xung đột CSDL và tích hợp schema từ nhánh của các thành viên khác vào nhánh `main`.

### 4. Mô hình hóa Thiết kế Kiến trúc (UML 2.x Sequence & State Machine Diagrams)
Đã thiết kế, chuẩn hóa và đóng gói thành các file `.drawio` chuyên nghiệp trong thư mục `docs/`, bao phủ trọn vẹn cả **3 Flows (1, 3, 4)** theo đúng định hướng kiến trúc:
* **Sequence Diagram Flow 1 (Basic) — [`docs/Sequence-CreateQuestion-Rubric-basic.drawio`](docs/Sequence-CreateQuestion-Rubric-basic.drawio):**
  * Thể hiện chi tiết 7 Lifelines: `Lecturer` $\rightarrow$ `:QuestionView (React)` $\rightarrow$ `:QuestionsController` $\rightarrow$ `:CreateQuestionValidator` $\rightarrow$ Khối `alt` (Xử lý nhánh lỗi 400 vs Nhánh thành công 201) $\rightarrow$ `:QuestionBankService` $\rightarrow$ `:UnitOfWork` $\rightarrow$ `SQL Server`. Khớp 100% với code C# .NET 10.
* **Sequence Diagram Flow 3 & 4 (Extended) — [`docs/Sequence-AIVivaExam-Workflow-extended.drawio`](docs/Sequence-AIVivaExam-Workflow-extended.drawio):**
  * Mô hình hóa quy trình tổng thể qua **5 Giai đoạn**:
    * *Giai đoạn 1:* Giảng viên cấu hình & mở ca thi (`ExamSession: Draft -> Open`).
    * *Giai đoạn 2:* Sinh viên kết nối SignalR `InterviewHub` thời gian thực và khởi tạo bài thi.
    * *Giai đoạn 3 (Flow 3):* Vòng lặp vấn đáp trực tiếp tương tác (Live Viva Q&A loop) qua Azure Speech (STT/TTS) và Azure OpenAI (GPT-4) sinh câu hỏi xoáy thích ứng theo ngữ cảnh sinh viên vừa trả lời.
    * *Giai đoạn 4 (Flow 4):* Nộp bài, trích xuất transcript và kích hoạt AI chấm điểm theo bộ tiêu chí Rubric (`Submitted` $\rightarrow$ `Evaluating` $\rightarrow$ `AwaitingReview`).
    * *Giai đoạn 5 (Flow 4):* Giảng viên xem lại dẫn chứng, điều chỉnh điểm số và chốt điểm chính thức (`FinalizeAssessment`).
* **State Machine Diagrams — [`docs/State-Machine-Diagrams.drawio`](docs/State-Machine-Diagrams.drawio):**
  * *Tab 1 (Flow 1 - Question Lifecycle):* `Initial` $\rightarrow$ `Draft` $\rightarrow$ `Validating` (Guard: `[TotalWeight == 100%]` vs `[TotalWeight != 100%]`) $\rightarrow$ `Approved` $\rightleftharpoons$ `InExamSession` (Khóa đóng băng khi ca thi đang mở) $\rightarrow$ `SoftDeleted` $\rightarrow$ `Final`.
  * *Tab 2 (Flow 3 & 4 - Exam Attempt & Assessment Lifecycle):* `Initial` $\rightarrow$ `Scheduled` $\rightarrow$ `Open` $\rightarrow$ `InProgress (Live Viva)` $\rightarrow$ `Submitted` $\rightarrow$ `Evaluating (AI Rubric Scoring)` $\rightarrow$ `AwaitingReview (Human-in-the-loop)` $\rightarrow$ `Finalized` $\rightarrow$ `Final`.

### 5. Biên soạn Báo cáo Thiết kế Kiến trúc Phần mềm (Software Architecture Design Report)
* Trực tiếp soạn thảo và hoàn thiện tài liệu **[`docs/Software_Architecture_Design_Report_AIVES_Group3.docx`](docs/Software_Architecture_Design_Report_AIVES_Group3.docx)** (kích thước ~40KB, 13 chương mục đầy đủ theo đúng Template chuẩn của Đại học FPT `Software Architecture Design_Report Template v.01.docx`):
  * **Business Context & Problem Statement:** Mô tả bối cảnh thi vấn đáp truyền thống và giải pháp tự động hóa bằng AI của AIVES.
  * **Use Case Realization & Quality Attributes:** Phân tích chi tiết các thuộc tính chất lượng (Performance, Scalability, Security, Modifiability).
  * **Mô hình 4+1 Architectural Views:** Trình bày chi tiết Logical View, Process View, Development View, Physical View và Scenarios.
  * **Architectural Rationale & Traceability Matrix:** Biện giải tính đúng đắn khi lựa chọn Onion Architecture, SignalR WebSockets, Azure Speech và OpenAI GPT-4.

### 6. Điều phối Kỹ thuật, Review Code & Đảm bảo Chất lượng (QA & Integration)
* **Code Review & Hỗ trợ kỹ thuật:** Thẩm định và sửa lỗi cú pháp trong `IUnitOfWork`, hướng dẫn và tích hợp mã nguồn từ các thành viên khác (Quang, Thái) vào nhánh `main`.
* **Kiểm thử tự động:** Duy trì hệ thống kiểm thử tự động với **263 checks regression / SQLite PASS** và **134 checks LocalDB / HTTP PASS**, bảo đảm toàn bộ solution build thành công với 0 warning và 0 error trước khi đóng gói nộp bài.

---

## ⚙️ Getting Started & Local Setup

Hiện đã hoàn thiện trọn vẹn Flow 1: CRUD User/Course/Question/Rubric, JSON Batch Import, AI Gemini sinh câu hỏi tự động (hỗ trợ xoay vòng key và fallback), xác thực JWT Bearer Role-based (Student/Lecturer) và Swagger UI tại root URL. Các phần phòng thi SignalR Speech và entity ca thi thuộc kế hoạch triển khai của Milestone 4. Đọc [hợp đồng database/API C03](docs/BACKEND_CONTRACTS.md) trước khi đổi entity, migration hoặc DTO.

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

Kiểm tra: mở Swagger, gọi GET `/api/questions/courses` và GET `/api/questions` để kiểm tra DB. Thử nghiệm tính năng tạo câu hỏi thủ công, import JSON hàng loạt (`POST /api/questions/import`) và sinh đề bằng AI (`POST /api/questions/ai-generate`). Nếu HTTPS bị chặn, trust dev certificate rồi khởi động lại.

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

### 👥 Nhóm tác giả thực hiện (SWD392 - Group 3)
* **Nguyễn Bùi Đăng Khôi** — Architecture Lead & Core Backend Engineer
* **Trần Thường Quang** — QuangTT SE196220
* **Vương Hoàng Giang** — GiangVH SE193455
* **Nguyễn Hùng Thái** — ThaiNH SE180436 (iamnhtf)

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

C12 có **261 checks PASS** tại phiên triển khai. Phiên kiểm tra M1 bổ sung đã đạt **263 regression checks + 134 SQL Server LocalDB checks PASS**, C07 được tick DONE (giữ credit Thai). Peer vẫn cần chạy lại trên máy mình; Azure SQL nhóm chưa xác minh. Các mốc C09–C12 và kiểm chứng M1 hiện nằm trên `quang`; peer cần lấy nhánh có các commit này để tích hợp.

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

## Kiểm chứng C07 và M1 trên SQL Server LocalDB

VS trên máy Quang đã cài LocalDB 17 nhưng SqlLocalDB.exe không nằm trong PATH. Chạy từ thư mục repo:

```powershell
& "C:/Program Files/Microsoft SQL Server/170/Tools/Binn/SqlLocalDB.exe" start MSSQLLocalDB
dotnet build
dotnet run --project tests/BackendChecks --no-build
dotnet run --project tests/BackendChecks --no-build -- --sql-server
```

Máy peer tìm đúng vị trí SqlLocalDB.exe hoặc dùng `sqllocaldb start MSSQLLocalDB` nếu đã có PATH. Nếu chưa cài SQL Server Express LocalDB, cài thành phần LocalDB rồi chạy lại; SQLite checks không thay thế SQL Server checks.

Lệnh `--sql-server` luôn dùng `(localdb)\MSSQLLocalDB` và tạo DB `AIVES_M1Checks_<Guid>` riêng, không đọc connection string trong appsettings hay dùng Azure SQL. Migration InitialCreate được apply trước, ghi Course mẫu, apply AddUserTable và xác minh dữ liệu vẫn còn, unique username index/Guid đúng, snapshot không drift và migration lặp idempotent. Sau đó chạy HTTP login/CRUD User/Course/Question-Rubric, phân quyền/soft delete và Swagger checks trên SQL Server. DB test được dọn trong finally; không giữ tài khoản test để demo.

Ngày 04/10/2026: **263 regression checks PASS**, **134 SQL Server checks PASS**, build 0 warning/error. C04–C12 đạt 9/9 mốc trên máy Quang; gate hai máy còn chờ peer xác nhận. Azure SQL nhóm và schema trong ảnh vẫn cần kiểm tra mapping read-only trước khi apply migration hoặc demo trên DB đó. Chạy bộ test này không thay đổi DB được WebApi cấu hình; để demo Swagger/FE cần DB dev thật đã chuẩn bị và tài khoản Lecturer của nhóm.
