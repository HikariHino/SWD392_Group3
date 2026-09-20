# 🎓 AIVES - Backend API Service

> **SWD392 (Software Architecture and Design) - Group 3**  
> An intelligent oral examination platform (AI-powered Viva Exam System) built with **.NET 8 (LTS)**, **Onion Architecture**, **SignalR**, and **OpenAI / Azure Speech**.

---

## 🏛️ System Architecture: Onion Architecture

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

## 🚀 Key Features (Milestone 1 Scope)

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

### 1. Prerequisites

* [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
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
dotnet run
```

Sau khi chạy thành công, hệ thống sẵn sàng phục vụ tại:

* **API Base URL:** `https://localhost:7123`
* **SignalR Hub Endpoint:** `https://localhost:7123/interviewHub`
* **OpenAPI / Swagger:** `https://localhost:7123/openapi`

---
