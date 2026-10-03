-- Script tạo dữ liệu mẫu cho Azure SQL Database

-- Định nghĩa các Guid cố định để tái sử dụng
DECLARE @UserStudent1Id UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000001';
DECLARE @UserStudent2Id UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000002';
DECLARE @UserTeacher1Id UNIQUEIDENTIFIER = '10000000-0000-0000-0000-000000000003';

DECLARE @CourseSWD392Id UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';

DECLARE @Question1Id UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000001';
DECLARE @Question2Id UNIQUEIDENTIFIER = '20000000-0000-0000-0000-000000000002';

DECLARE @Session1Id UNIQUEIDENTIFIER = '30000000-0000-0000-0000-000000000001';

DECLARE @Transcript1Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000001';
DECLARE @Transcript2Id UNIQUEIDENTIFIER = '40000000-0000-0000-0000-000000000002';

-- Seed Users
INSERT INTO [Users] (UserId, Username, PasswordHash, FullName, Role) VALUES
(@UserStudent1Id, 'student1', 'pwd1', N'Nguyễn Văn A', 'Student'),
(@UserStudent2Id, 'student2', 'pwd2', N'Trần Thị B', 'Student'),
(@UserTeacher1Id, 'teacher1', 'pwd3', N'Lê Văn C', 'Teacher');

-- Seed Courses
INSERT INTO [Courses] (CourseId, Code, Name, Description) VALUES
(@CourseSWD392Id, 'SWD392', N'Software Architecture and Design', N'Học về thiết kế kiến trúc phần mềm');

-- Seed Questions
INSERT INTO [Questions] (QuestionId, CourseId, Content, ExpectedAnswer, QuestionType, BloomLevel, Points, AiModel) VALUES
(@Question1Id, @CourseSWD392Id, N'Trình bày về tính chất Đóng gói (Encapsulation) trong OOP?', N'Đóng gói là che giấu thông tin nội bộ của đối tượng và chỉ giao tiếp qua các phương thức public (getter/setter).', 'Essay', 2, 5.00, 'gemini-1.5-pro'),
(@Question2Id, @CourseSWD392Id, N'Kể tên 3 Design Pattern thuộc nhóm Creational?', N'Singleton, Factory Method, Abstract Factory, Builder, Prototype', 'Essay', 1, 5.00, 'gpt-4o');

-- Seed Rubrics
-- Câu 1 (BloomLevel = 2) được chia làm 2 Rubrics: 60% và 40% (Tổng = 100%)
INSERT INTO [Rubrics] (RubricId, QuestionId, Criteria, Weight, MaxScore, AiGradingPrompt) VALUES
(NEWID(), @Question1Id, N'Định nghĩa đúng Đóng gói', 60.0, 3.00, N'Check if the answer clearly defines encapsulation as hiding internal state and requiring interaction through methods. Penalize if vague.'),
(NEWID(), @Question1Id, N'Có nêu ví dụ về Getter/Setter', 40.0, 2.00, N'Check if they explicitly mentioned getter/setter or properties to access private fields.');

-- Câu 2 (BloomLevel = 1) chỉ có 1 Rubric chiếm 100%
INSERT INTO [Rubrics] (RubricId, QuestionId, Criteria, Weight, MaxScore, AiGradingPrompt) VALUES
(NEWID(), @Question2Id, N'Kể đúng 3 pattern nhóm Creational', 100.0, 5.00, N'Extract the patterns mentioned. Check them against standard Creational patterns. Give 1.66 points for each correct one.');

-- Seed ExamSessions
INSERT INTO [ExamSessions] (SessionId, CourseId, SessionTitle, StartTime, EndTime) VALUES
(@Session1Id, @CourseSWD392Id, N'Thi giữa kỳ Kiến trúc Phần mềm', '2026-10-10T08:00:00', '2026-10-10T09:30:00');

-- Seed Transcripts
INSERT INTO [Transcripts] (TranscriptId, UserId, SessionId, TotalScore, GradedAt) VALUES
(@Transcript1Id, @UserStudent1Id, @Session1Id, 8.50, '2026-10-10T09:35:00'),
(@Transcript2Id, @UserStudent2Id, @Session1Id, 4.00, '2026-10-10T09:35:00');

-- Seed Assessments
INSERT INTO [Assessments] (AssessmentId, TranscriptId, QuestionId, StudentAnswer, AiSuggestedScore, AiFeedback, AiConfidenceLevel, FinalScore, TeacherComment) VALUES
(NEWID(), @Transcript1Id, @Question1Id, N'Đóng gói là việc ẩn các thuộc tính bên trong class thành private, muốn truy cập phải qua getter và setter public.', 5.00, N'Câu trả lời chính xác, giải thích đúng khái niệm và có đề cập cơ chế getter/setter.', 98.50, 5.00, NULL),
(NEWID(), @Transcript1Id, @Question2Id, N'Em nhớ là Singleton, Builder và Observer.', 3.33, N'Bạn nêu đúng Singleton và Builder (thuộc Creational). Tuy nhiên, Observer thuộc nhóm Behavioral.', 95.00, 3.50, N'Cho thêm 0.17 điểm khuyến khích'),
(NEWID(), @Transcript2Id, @Question1Id, N'Đóng gói là đóng gói code lại thành 1 file .dll để người khác không sửa được.', 0.00, N'Sai hoàn toàn về khái niệm Encapsulation trong OOP. Câu trả lời giống với khái niệm Compile/Assembly hơn.', 90.00, 0.00, N'Đồng ý với AI, kiến thức hổng'),
(NEWID(), @Transcript2Id, @Question2Id, N'Singleton, Prototype', 3.33, N'Kể được 2 mẫu chính xác nhưng đề yêu cầu 3.', 99.00, 4.00, N'Sinh viên có cố gắng, châm chước cho điểm cao hơn AI đề xuất.');
