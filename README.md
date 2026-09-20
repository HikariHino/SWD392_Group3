# 🎓 AIVES - Backend API Service (.NET 8)
> **SWD392 (Software Architecture and Design) - Group 3**  
> Backend core engine for the AI-powered Viva Exam System, built with **Onion Architecture**, **SignalR**, and **OpenAI/Azure Integration**.
```text
SWD392_Group3/
├── Domain/                           # Enterprise Core
│   ├── Common/                       # BaseEntity, AuditableEntity (Audit timestamps)
│   ├── Entities/                     # User, Question, Rubric, ExamSession, Transcript, Assessment
│   ├── Enums/                        # BloomLevel, ExamStatus, UserRole
│   └── Exceptions/                   # Custom Domain Exceptions
│
├── Application/                      # Business Logic Layer
│   ├── DTOs/                         # Data Transfer Objects
│   ├── Interfaces/                   # Abstractions & Contracts
│   │   ├── Services/                 # IQuestionBankService, IInterviewService, IGradingService
│   │   ├── Repositories/             # Repository Contracts (DIP)
│   │   └── ExternalServices/         # IOpenAIService, ISpeechService
│   ├── Mappings/                     # AutoMapper Profiles
│   └── Services/                     # QuestionBankService, InterviewService, GradingService
│
├── Infrastructure/                   # Concrete Technical Implementation
│   ├── Persistence/                  # EF Core, AivesDbContext, Fluent API Configurations
│   ├── Repositories/                 # Repository Implementations
│   └── ExternalServices/             # OpenAIService (GPT-4), AzureSpeechService (STT/TTS)
│
└── WebApi/                           # Presentation Layer
    ├── Controllers/                  # RESTful API Endpoints (Questions, Grading, Auth)
    ├── Hubs/                         # SignalR Real-time Hub (InterviewHub for Voice Streaming)
    ├── appsettings.json              # Connection strings & Configuration
    └── Program.cs                    # IoC Dependency Injection Container setup

🚀 Key Features Supported
Feature 1: Question Bank & Rubric Management

Quản lý câu hỏi theo phân loại mức độ nhận thức (Bloom Level).
Cấu hình thang điểm và tiêu chí đánh giá (Rubric Criteria) đi kèm.
Thao tác dữ liệu quan hệ chặt chẽ qua Entity Framework Core.
Feature 3: Real-Time AI Viva Exam Core

Sử dụng SignalR WebSockets (Full-duplex) tạo kết nối thời gian thực độ trễ thấp.
Chuyển đổi giọng nói sinh viên (Speech-to-Text) qua Azure Speech Service.
AI phản xạ sinh câu hỏi thích ứng (OpenAI GPT-4) và tổng hợp giọng nói phản hồi (Text-to-Speech).
Feature 4: AI Grading & Feedback (Human-in-the-loop)

Tự động đối chiếu toàn bộ Transcript ca thi với tiêu chí Rubric.
Đề xuất điểm gợi ý và nhận xét chi tiết; hỗ trợ Giảng viên xem xét và chốt điểm chính thức.

⚙️ Getting Started & Local Setup
1. Prerequisites
[.NET 8 SDK](https://dotnet.microsoft.com/fr-fr/download/dotnet/8.0)
[SQL Server (LocalDB or Express)](https://www.microsoft.com/vi-vn/sql-server)
2. Configure Connection String
Mở file WebApi/appsettings.json và cấu hình chuỗi kết nối Database của máy bạn:
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=AIVES_Db;Trusted_Connection=True;TrustServerCertificate=True"
}
3. Run the Backend API
bash


# Di chuyển vào thư mục WebApi
cd WebApi
# Restore các NuGet packages
dotnet restore
# Build dự án
dotnet build
# Chạy Server
dotnet run
Sau khi chạy thành công, API sẽ hoạt động tại:

API Base URL: https://localhost:7123
SignalR Hub Endpoint: https://localhost:7123/interviewHub
Swagger/OpenAPI: https://localhost:7123/openapi
