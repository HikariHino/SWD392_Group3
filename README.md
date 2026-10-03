# 🎓 AIVES - Backend API Service

> Tiến độ thực tế, lỗi đang mở và quy tắc nhận việc backend: [docs/BACKEND_PROGRESS.md](docs/BACKEND_PROGRESS.md). Đọc và cập nhật file này trước/sau mỗi phiên coding chung.

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

Startup Development hiện gọi `EnsureCreatedAsync()` rồi seed Course/Question; **không tự áp dụng migration**. InitialCreate chỉ có Courses/Questions/Rubrics, còn model đã có Users. DB tạo bằng EnsureCreated không tự có migration history. Chưa chạy migration lên database có sẵn trước khi đối chiếu C03/C07; không xóa DB hoặc baseline đè dữ liệu. `[DatabaseSeeder Error]` không ngăn Swagger mở, nên cần kiểm tra CRUD riêng.

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
