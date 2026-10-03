-- Script tạo bảng cho Azure SQL Database
-- Lưu ý: Phải kết nối trực tiếp vào đúng Database SWD392 trước khi chạy

CREATE TABLE [Users] (
    UserId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Username VARCHAR(50) NOT NULL UNIQUE,
    PasswordHash VARCHAR(255) NOT NULL,
    FullName NVARCHAR(100) NOT NULL,
    Role VARCHAR(50) NOT NULL, 
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE TABLE [Courses] (
    CourseId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    Code VARCHAR(50) NOT NULL UNIQUE,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX),
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsDeleted BIT NOT NULL DEFAULT 0
);

CREATE TABLE [Questions] (
    QuestionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CourseId UNIQUEIDENTIFIER NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    ExpectedAnswer NVARCHAR(MAX), -- Dữ liệu mẫu để AI dùng làm base so sánh
    QuestionType VARCHAR(50) NOT NULL,
    BloomLevel INT NOT NULL, -- 1: Remember, 2: Understand, 3: Apply, 4: Analyze, 5: Evaluate, 6: Create
    Points DECIMAL(5,2) NOT NULL,
    AiModel VARCHAR(50) DEFAULT 'gpt-4', -- Cho biết câu này dùng model AI nào để chấm
    CreatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Questions_Courses FOREIGN KEY (CourseId) REFERENCES [Courses](CourseId)
);

CREATE TABLE [Rubrics] (
    RubricId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    QuestionId UNIQUEIDENTIFIER NOT NULL,
    Criteria NVARCHAR(MAX) NOT NULL,
    Weight FLOAT NOT NULL, -- Trọng số %, tổng các rubric của 1 câu hỏi phải = 100%
    MaxScore DECIMAL(5,2) NOT NULL,
    AiGradingPrompt NVARCHAR(MAX), -- Lệnh prompt đưa cho AI để hướng dẫn chấm tiêu chí này
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Rubrics_Questions FOREIGN KEY (QuestionId) REFERENCES [Questions](QuestionId)
);

CREATE TABLE [ExamSessions] (
    SessionId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    CourseId UNIQUEIDENTIFIER NOT NULL,
    SessionTitle NVARCHAR(255) NOT NULL,
    StartTime DATETIME2 NOT NULL,
    EndTime DATETIME2 NOT NULL,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_ExamSessions_Courses FOREIGN KEY (CourseId) REFERENCES [Courses](CourseId)
);

CREATE TABLE [Transcripts] (
    TranscriptId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL,
    SessionId UNIQUEIDENTIFIER NOT NULL,
    TotalScore DECIMAL(5,2),
    GradedAt DATETIME2,
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Transcripts_Users FOREIGN KEY (UserId) REFERENCES [Users](UserId),
    CONSTRAINT FK_Transcripts_ExamSessions FOREIGN KEY (SessionId) REFERENCES [ExamSessions](SessionId)
);

CREATE TABLE [Assessments] (
    AssessmentId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    TranscriptId UNIQUEIDENTIFIER NOT NULL,
    QuestionId UNIQUEIDENTIFIER NOT NULL,
    StudentAnswer NVARCHAR(MAX) NOT NULL, -- Do không có bảng bài làm, lưu trực tiếp bài của SV vào đây
    AiSuggestedScore DECIMAL(5,2),        -- Điểm do AI tự động chấm
    AiFeedback NVARCHAR(MAX),             -- Lời phê sinh ra bởi AI
    AiConfidenceLevel DECIMAL(5,2),       -- Độ tự tin của mô hình AI (vd: 95.5%)
    FinalScore DECIMAL(5,2),              -- Điểm cuối cùng (nếu GV đồng ý với AI, hoặc GV sửa lại)
    TeacherComment NVARCHAR(MAX),         -- Nhận xét bổ sung của GV (nếu có)
    EvaluatedAt DATETIME2 DEFAULT SYSUTCDATETIME(),
    IsDeleted BIT NOT NULL DEFAULT 0,
    CONSTRAINT FK_Assessments_Transcripts FOREIGN KEY (TranscriptId) REFERENCES [Transcripts](TranscriptId),
    CONSTRAINT FK_Assessments_Questions FOREIGN KEY (QuestionId) REFERENCES [Questions](QuestionId)
);
