# AIVES — Hợp đồng database và API (C03)

Baseline: `quang` sau C02 `fd7f6b2`, 03/10/2026. Người lập: Quang cùng Codex.
Đây là quyết định thiết kế cho các mốc C04 trở đi; phần “hiện có” phản ánh code, phần “mục tiêu” chưa được triển khai. Nếu đổi contract, cập nhật file này trong cùng commit và thông báo peer qua bàn giao. Không coi endpoint dự kiến là endpoint đang chạy.

## 1. Nguồn đối chiếu và quyết định schema

### Đối chiếu draw.io cập nhật ngày 03/10/2026

Đã đọc lớp, thuộc tính, phương thức và các cạnh trong [AIVES-UML-Class-Diagram.drawio](AIVES-UML-Class-Diagram.drawio). Sơ đồ là thiết kế đề xuất; dòng “đồng bộ 100%” trong hình chưa phản ánh đúng source. Bản cập nhật không tự thay đổi contract C03 hoặc chứng minh feature đã triển khai.

| Điểm | Draw.io mới | Source / contract cần dùng |
| --- | --- | --- |
| Phiên bản | Tiêu đề và ghi chú đã đổi sang .NET 10 | Đồng bộ C02; các khác biệt implementation phía dưới vẫn cần cập nhật |
| Controller | Chỉ inject IQuestionBankService; ImportQuestions(IFormFile) | Source còn inject 2 validator; ImportQuestions() không nhận file thật. Upload là C25, chưa có trong code |
| Service | Inject IQuestionRepository trực tiếp | Source inject IUnitOfWork + IMapper, dùng Questions/Courses và SaveChangesAsync của UoW. Không refactor sang repository trực tiếp chỉ vì hình khác |
| Repository | Có AddRangeAsync và SaveChangesAsync | AddRangeAsync đã có qua IGenericRepository; SaveChangesAsync chỉ ở IUnitOfWork, không ở IQuestionRepository. Sơ đồ cần thêm generic repository/UoW nếu muốn mô tả implementation |
| Domain inheritance | Các entity nối thẳng BaseEntity | Course/Question/Rubric thực tế qua AuditableEntity rồi BaseEntity; User qua BaseEntity. Audit/soft delete và navigation chưa được hình thể hiện đủ |
| Domain methods | UpdateProfile, IsOpen, SubmitAnswer, FinalizeEvaluation... | Chưa có trong entity source; là định hướng đóng gói nghiệp vụ cho mốc sau, không phải feature đã xong |
| DbContext | 7 DbSet, gồm ExamSessions/Transcripts/Assessments | Source mới có Users/Courses/Questions/Rubrics. Các DbSet thi/chấm điểm triển khai C13/C20 |
| Transcript / điểm | Transcript.TotalScore decimal; Rubric double | C03 giữ Transcript là bài thi; TotalScore phải nullable trước chốt. Decimal cho điểm/rubric là chuyển đổi dự kiến C20, không làm mất trạng thái chưa chấm |
| Luồng thi đầy đủ | Chưa có owner/status/turn/snapshot/concurrency | Bổ sung theo phần 2–7: TranscriptTurns, ExamSessionQuestions, trạng thái, audit người chốt, snapshot rubric và version |

Các quan hệ Course→Question/ExamSession, User/ExamSession→Transcript, Transcript/Question→Assessment và Question→Rubric phù hợp hướng C03. Guid và BloomLevel 1..6 cũng thống nhất. Các phần Question.Points/ExpectedAnswer/AiModel, rubric prompt, ownership và hội thoại vẫn là mở rộng dự kiến từ C03/ảnh DB, không được xem là đã chốt bởi draw.io. Sơ đồ hiện chỉ mô tả đường Question Bank và entity, chưa phải toàn bộ API/User/Auth/AI/SignalR của AIVES.

Thứ tự sử dụng: source/Swagger để biết hành vi đang chạy; tài liệu C03 để triển khai mục tiêu đã ghi; draw.io để tham khảo thiết kế và cập nhật theo các mốc. Khi chủ động thay quyết định C03, ghi lý do và ảnh hưởng DTO/schema/peer trong cùng commit.

Đã đọc entity, EF configuration, InitialCreate/snapshot, DTO, controller và ảnh DB người dùng gửi. Ảnh không cung cấp kiểu dữ liệu, index hay migration history; chưa truy vấn SQL Server đang mở. Conceptual_ERD.md dùng int ở mức khái niệm, không phải kiểu khóa chuẩn của implementation.

| Bảng | Code / migration hiện có | Ảnh DB | Baseline cho triển khai |
| --- | --- | --- | --- |
| Courses | PK `Id` Guid; Code, Name, Description; audit/soft delete | PK CourseId; cùng các trường mô tả | Giữ `Id` trong schema code mới, API id dạng UUID; DB trong ảnh cần mapping trước migration |
| Questions | PK Id Guid; CourseId; Content; BloomLevel int; audit | PK QuestionId; thêm ExpectedAnswer, QuestionType, Points, AiModel | Giữ model hiện tại; thêm ExpectedAnswer nullable, Points decimal > 0 (mặc định 10), QuestionType=Oral, AiModel nullable khi use case cần |
| Rubrics | PK Id Guid; QuestionId; Criteria; Weight/MaxScore double | PK RubricId; thêm AiGradingPrompt | Giữ quan hệ; chuyển số điểm/trọng số sang decimal ở mốc grading; prompt override nullable |
| Users | Model/config có UserId Guid, Username unique, PasswordHash, FullName, Role, CreatedAt nullable, IsDeleted; migration chưa có | Cùng tên trường hiển thị | C07 bổ sung schema; không dùng password nguyên văn; DTO không trả PasswordHash |
| ExamSessions | Chưa có | SessionId, CourseId, SessionTitle, StartTime, EndTime, IsDeleted | C13 thêm SessionId Guid, CreatedByUserId, Status, thời gian UTC, audit |
| Transcripts | Chưa có | TranscriptId, UserId, SessionId, TotalScore, GradedAt, IsDeleted | Một bài thi của sinh viên, không phải một câu hội thoại; thêm Status, StartedAt, SubmittedAt |
| Assessments | Chưa có | AssessmentId, TranscriptId, QuestionId, StudentAnswer, AiSuggestedScore, AiFeedback, AiConfidenceLevel, FinalScore, TeacherComment, EvaluatedAt, IsDeleted | Một kết quả chấm cho câu được tính điểm của bài thi; thêm FinalizedByUserId/FinalizedAt và snapshot rubric |
| TranscriptTurns | Chưa có/không thấy trong ảnh | Không thấy | C13 thêm TurnId Guid, TranscriptId, Sequence, Speaker, ContentText, Timestamp, QuestionId nullable; lưu hội thoại thật |
| ExamSessionQuestions | Chưa có/không thấy trong ảnh | Không thấy | Chốt bộ câu hỏi, thứ tự và snapshot điểm/rubric cho phiên trước khi mở; không phụ thuộc câu hỏi bị sửa sau đó |

Đối với **DB dev mới**, migrations của repo là nguồn schema; không đổi PK Id của bảng có sẵn chỉ để giống ảnh. Với **DB có dữ liệu trong ảnh**, phải xác minh kiểu PK/FK, tên cột, snapshot/history trước khi chọn mapping Fluent API hoặc migration chuyển đổi. Nếu khóa là int, cần bảng ánh xạ và kế hoạch chuyển đổi dữ liệu; không ép Guid vào cột int. Đây là bước bắt buộc trước áp dụng C07/C13 vào DB đó, không phải yêu cầu tạo lại DB.

## 2. Kiểu dữ liệu và quan hệ mục tiêu

- Khóa mới: Guid / SQL uniqueidentifier; JSON UUID string. UTC DateTime / SQL datetime2, JSON ISO 8601 có Z. Username tối đa 50, FullName 100; Course Code 50, Name 200, Description 1000; Question Content 2000, Rubric Criteria 500 (theo config hiện tại).
- BloomLevel giữ integer 1..6: Remember, Understand, Apply, Analyze, Evaluate, Create. API hiện trả cả bloomLevel và bloomLevelName.
- Role canonical: `Lecturer`, `Student`. Không có tự đăng ký được chọn Lecturer; tài khoản lecturer được cấp bởi quy trình nhóm/quản trị, chưa thêm role Admin vào MVP. Khi C06/C12 triển khai, thao tác cấp/đổi quyền không công khai.
- Một Course có nhiều Question và ExamSession. Một Question có nhiều Rubric. Một User(Student) có nhiều Transcript; một ExamSession có nhiều Transcript. Unique `(SessionId, UserId)` cho một lần thi MVP.
- Một Transcript có nhiều TranscriptTurn, Sequence tăng dần, unique `(TranscriptId, Sequence)`. Một question được tính điểm có tối đa một Assessment trong bài thi: unique `(TranscriptId, QuestionId)`; lượt hỏi tiếp là hội thoại hỗ trợ, không tự tạo câu tính điểm mới.
- Assessment lưu đáp án tổng hợp và snapshot đề/rubric/Points tại phiên thi. Lecturer chốt phải là người có quyền quản lý phiên (CreatedByUserId), không chỉ kiểm tra role.
- Soft delete không xóa lịch sử bài thi. Khóa ngoại lịch sử dùng Restrict/NoAction; không cascade xóa kết quả khi xóa course/user/question. Model hiện cascade Rubric theo Question khi hard delete: phải giữ snapshot lịch sử trước khi mở ca thi.
- Decimal mục tiêu: Points/MaxScore/Weight/scores `(18,4)`; confidence `(5,4)` trong [0,1], nullable khi provider không trả. FinalScore/AiSuggestedScore nullable để phân biệt chưa chấm với điểm 0. Confidence là output tự báo của provider, không coi là xác suất được hiệu chuẩn.

## 3. Vòng đời và quyền

| Đối tượng | Trạng thái mục tiêu | Quy tắc chuyển |
| --- | --- | --- |
| ExamSession | Draft → Open → Closed; Draft/Open → Cancelled | Lecturer sở hữu mở/đóng/hủy; chỉ mở khi có course, bộ câu hỏi/rubric hợp lệ và StartTime < EndTime; không mở lại Closed/Cancelled |
| Transcript | InProgress → Submitted → Grading → AwaitingReview → Finalized | Student chỉ trả lời bài mình trong phiên Open và khoảng thời gian hợp lệ; Submitted đóng input; AI chấm đủ mới AwaitingReview; lecturer chốt đủ mới Finalized |
| Grading failure | Trở lại Submitted, ghi lỗi/retry | Không lưu điểm giả thành công; retry idempotent theo attempt/question, không nhân đôi Assessment |

Đóng phiên: bài InProgress được submit bằng transaction/use case thống nhất; xử lý câu chưa trả lời là điểm 0 kèm lý do, không bỏ khỏi mẫu số. Hủy phiên không chấm/chốt tiếp, giữ lịch sử. Thiết kế MVP cho phép một student một bài/phiên, không retake. Auth principal là nguồn UserId; server không tin studentId truyền từ client. Cập nhật/chốt có concurrency token để trả 409 khi ghi đè trạng thái cũ.

## 4. Công thức chấm điểm — C20/C23

Rubric có Weight > 0, tổng Weight = 100 (tolerance hiện tại 0.01); MaxScore > 0. Điểm tiêu chí `earned_i` trong [0, MaxScore_i]. Điểm câu:

```text
questionScore = Points * SUM((earned_i / MaxScore_i) * (Weight_i / 100))
attemptTotal  = 10 * SUM(questionScore) / SUM(Points của toàn bộ câu tính điểm)
```

Ví dụ Points=10; rubric (Weight=40, MaxScore=4, earned=3) và (Weight=60, MaxScore=6, earned=3): questionScore=6. Không nhân trọng số hai lần hoặc cộng raw rubric score rồi lại coi là điểm thang 10.

- Dùng decimal, chỉ làm tròn AwayFromZero 2 chữ số ở kết quả trả về/lưu tổng. Không làm tròn từng phép tính trung gian.
- AI trả scores theo rubricId, feedback và confidence tùy chọn. Server kiểm tra rubricId đầy đủ/không trùng, finite/range, tự tính điểm; không tin tổng do AI tự trả. Output sai trả lỗi provider để retry, không tự gán 8.5.
- Lecturer chốt `FinalScore` trong [0, Points], comment và audit. Tổng chính thức chỉ có khi tất cả câu có FinalScore; trả aiSuggestedTotal riêng nếu có đủ đề xuất. Không trộn điểm AI và điểm chốt thành TotalScore chính thức.

## 5. API đang có trong code

Tên JSON camelCase, route không version trong baseline; giữ route hiện tại để peer không tự đổi client.

| Method / route | Request | Response thành công hiện tại |
| --- | --- | --- |
| GET /api/questions | courseId?, bloomLevel?, searchTerm?, pageIndex=1, pageSize=10 | 200 PagedResponse<QuestionDto> |
| GET /api/questions/{id} | UUID | 200 QuestionDto; 404 khi thiếu |
| POST /api/questions | content, courseId, bloomLevel, rubrics[] | 201 QuestionDto + Location |
| PUT /api/questions/{id} | content, bloomLevel, rubrics[]; thay toàn bộ rubric | 200 QuestionDto; 404 khi thiếu |
| DELETE /api/questions/{id} | UUID | 204; soft delete |
| GET /api/questions/courses | Không | 200 collection Domain Course (sẽ đổi CourseDto tại C10) |
| POST /api/questions/import | Không có file thật | 200 success=true + message (mock) |
| GET /api/Users | Không | 200 UserDto[] |
| GET /api/Users/{id} | UUID | 200 UserDto |
| POST /api/Users | username, password, fullName, role | 201 UserDto + Location |
| PUT /api/Users/{id} | fullName?, role? | 204 |
| DELETE /api/Users/{id} | UUID | 204; soft delete |

UserDto: id, username, fullName, role, createdAt. QuestionDto: id, content, bloomLevel, bloomLevelName, courseId, courseCode, courseName, createdAt, updatedAt, rubrics[{id,criteria,weight,maxScore}]. PagedResponse: items, totalCount, pageIndex, pageSize, totalPages, hasPreviousPage, hasNextPage. C08 giới hạn pageIndex>=1 và pageSize 1..100. C10 CourseDto: id, code, name, description; không navigation/audit nội bộ.

Ví dụ request tạo câu hỏi hợp lệ với course mẫu (chỉ dùng nếu course đã seed):

```json
{
  "courseId": "11111111-1111-1111-1111-111111111111",
  "content": "Giải thích Dependency Inversion Principle",
  "bloomLevel": 2,
  "rubrics": [{ "criteria": "Giải thích đúng", "weight": 100, "maxScore": 10 }]
}
```

Auth và lỗi hiện chưa thống nhất: User bắt generic Exception thành 400/404, Question validator trả message/errors, lỗi hệ thống có thể 500. Đây là hành vi baseline, không phải tiêu chí cho C05.

## 6. API mục tiêu cho các mốc sau

| Mốc / method route | Request chính | Success / quyền |
| --- | --- | --- |
| C11 POST /api/auth/login | username, password | 200 {accessToken, expiresAt, user: UserDto}; sai thông tin 401 |
| C10 GET/POST /api/courses; GET/PUT/DELETE /api/courses/{id} | Create/Update {code,name,description?} | GET 200; POST 201; PUT 200; DELETE 204; mutation lecturer |
| C14 POST /api/exam-sessions | courseId, sessionTitle, startTime, endTime, questionIds[] | 201 SessionDto; lecturer; owner lấy từ principal |
| C14 GET /api/exam-sessions/{id} | UUID | 200 SessionDto; lecturer owner/student được tham gia |
| C14 POST /api/exam-sessions/{id}/open hoặc /close hoặc /cancel | Không | 200 SessionDto; lecturer owner; sai state 409 |
| C15 POST /api/exam-sessions/{id}/attempts | Không; user từ principal | 201 AttemptDto, trả attempt có sẵn 200 khi retry; student |
| C15 GET /api/attempts/{id} | UUID | 200 AttemptDto; student owner/lecturer owner phiên |
| C15 POST /api/attempts/{id}/answers | questionId, answerText, clientMessageId UUID | 201 TurnDto; retry cùng message trả bản ghi cũ; student owner |
| C15 POST /api/attempts/{id}/submit | Không | 200 AttemptDto; student owner; idempotent |
| C21 POST /api/attempts/{id}/grade | Không | 202 {attemptId,status}; lecturer owner; queue/use case chấm; không chấm đôi |
| C22 GET /api/attempts/{id}/assessments | Không | 200 AssessmentDto[]; lecturer owner |
| C22 PUT /api/assessments/{id}/final-score | finalScore, teacherComment?, version | 200 AssessmentDto; lecturer owner |
| C23 GET /api/attempts/{id}/result | Không | 200 ResultDto; student owner chỉ khi Finalized, lecturer owner xem draft |

SessionDto: id, courseId, sessionTitle, startTime, endTime, status, createdByUserId, version. AttemptDto: id, sessionId, userId, status, startedAt, submittedAt, version. TurnDto: id, attemptId, sequence, speaker, contentText, timestamp, questionId?, clientMessageId?. AssessmentDto: id, attemptId, questionId, studentAnswer, aiSuggestedScore?, aiFeedback?, aiConfidenceLevel?, finalScore?, teacherComment?, finalizedByUserId?, finalizedAt?, version. ResultDto: attemptId, status, aiSuggestedTotal?, totalScore?, gradedAt?, assessments[]; không gửi expectedAnswer/prompt cho student trong lúc thi.

Upload C25: multipart file, chọn CSV UTF-8 với header courseCode,content,bloomLevel,criteria,weight,maxScore; nhiều dòng cùng câu cần questionGroupKey để gom rubric. Trước triển khai C25 chốt template mẫu và semantics key; atomic toàn file, lỗi 400 có row/field/message, thành công {importedQuestions,importedRubrics}. Không nhận filePath local từ client. Speech C26/C27 chọn provider/định dạng khi bắt đầu mốc, chưa khóa Azure/model cụ thể.

C05 chuẩn lỗi: application/problem+json với type,title,status,detail an toàn,traceId; validation thêm errors keyed by field. 400 input sai, 401 thiếu token, 403 thiếu quyền, 404 resource không thuộc phạm vi truy cập, 409 conflict/trùng username/sai state/concurrency, 502 output provider sai, 503 provider chưa cấu hình/không khả dụng, 504 timeout, 500 lỗi hệ thống. Không trả stack trace/password/token vào log hoặc response lỗi.

## 7. SignalR contract

Hiện tại: `/interviewHub`, method SendStudentAnswer(studentId, answerText), event ReceiveAIFollowUp(string); service trả chuỗi mock. C18 chuyển contract sang SendStudentAnswer(attemptId, questionId, answerText, clientMessageId). Frontend phải cập nhật cùng mốc, không giữ studentId tự khai là căn cứ quyền.

Mục tiêu: xác thực JWT; check owner/state/time giống HTTP use case; cả HTTP và Hub dùng cùng logic lưu answer. Response event ReceiveAIFollowUp({attemptId,turnId,questionId,contentText,sequence}); lỗi nghiệp vụ dùng InterviewError({code,message,clientMessageId}); đóng bài dùng AttemptStatusChanged({attemptId,status}). Gửi vào group bài thi do server quản lý sau authorize; không cho client tùy ý join bài người khác. Retry clientMessageId không tạo hai lượt/AI response trùng. Reconnect tải lịch sử đã lưu; không dựa hoàn toàn vào event realtime.

## 8. Kế hoạch migration và checklist bàn giao

1. C07/C13 trước khi đụng DB có sẵn: đọc read-only INFORMATION_SCHEMA và sys.foreign_keys/indexes, __EFMigrationsHistory nếu tồn tại; ghi kết quả kiểu khóa/tên cột và số bản ghi, không dump dữ liệu người dùng.
2. So sánh với model/snapshot. DB mới: tạo/apply migrations đủ schema; thay EnsureCreated bằng migration flow rõ ở mốc persistence. DB ảnh: chọn mapping/chuyển đổi có bảo toàn dữ liệu, review SQL migration trước khi apply.
3. Không sửa InitialCreate đã chia sẻ để giả vờ có Users; thêm migration C07 và cập nhật snapshot. Kiểm tra trên DB dev mới và bản sao DB hiện hữu khi có dữ liệu.
4. C13 snapshot bộ đề/rubric, thêm FK/index và trạng thái; C20 bổ sung assessment/decimal. Một người tạo migration mỗi lần, sau khi pull model chung.

C03 hoàn thành ở mức tài liệu: mapping khác biệt, baseline schema mới, vòng đời, role, công thức điểm và request/response đã ghi. **Chưa xác minh schema SQL live và chưa apply migration**; đó là điều kiện trước triển khai persistence lên DB có sẵn. Người nhận C04/C08 có thể dùng contract này; người nhận C07 phải thực hiện bước xác minh DB phía trên. Ví dụ công thức đã đối chiếu: 10*(3/4*0.4+3/6*0.6)=6.
