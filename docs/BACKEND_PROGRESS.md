# Tiến độ backend và phối hợp coding

## Kết quả mới nhất trên nhánh quang — 03/10/2026

Đã tích hợp `khoi` tại `eaab86e` vào `quang` (baseline trước merge `08ab287`). Giữ CRUD Question/Rubric/User, persistence và Swagger của khoi; giải quyết 3 conflict ở IUnitOfWork, Program.cs và WebApi.csproj. Swagger Development mở tại `/`, JSON tại `/swagger/v1/swagger.json`; launchUrl đổi về trang gốc. Cấu hình appsettings.json local được bảo toàn ngoài commit.

Build kiểm chứng dùng output tạm vì API trong Visual Studio đang khóa DLL: PASS, 0 warning / 0 error. C01 đã đạt tiêu chí khôi phục build qua phần sửa `e2672b3` của khoi; chưa xác minh CRUD với DB. C02 tiếp theo: đồng bộ hướng dẫn/SDK; C03 tiếp theo: chốt mapping DB và API. Không đánh dấu các mốc CRUD/Auth hoàn thành chỉ vì đã merge source; password hashing và migration Users còn thiếu.

### Lịch sử kiểm tra trước merge (không còn là trạng thái hiện tại)

Kiểm tra truy cập localhost: HTTPS 7035 `/` trả 404 trước khi sửa; `/openapi/v1.json` và `/weatherforecast` trả 200; HTTP 5110 chuyển sang HTTPS 7035. Chứng chỉ dev đã trusted. Thêm redirect trang gốc Development đến tài liệu OpenAPI và launchUrl cho Visual Studio. Cần restart API để áp dụng. Tài liệu hiện là JSON, chưa có Swagger UI.

Build bản sửa redirect thành công với thư mục output riêng vì Visual Studio đang giữ DLL của server chạy; 0 lỗi source, có cảnh báo NU1900 do không đọc được dữ liệu audit NuGet. Đã xóa output kiểm chứng riêng sau build; không dừng phiên Visual Studio của người dùng.

Baseline hiện tại: `quang`, commit `51135a6`. Các phần review bên dưới ghi nhận nhánh `main` tại `c27221a`, không mô tả source hiện tại của `quang`.

- Máy đã có SDK .NET 10.0.401. Build ban đầu trên `quang` thành công, 0 lỗi, 1 cảnh báo NU1903 từ Microsoft.OpenApi 2.0.0.
- Nâng dependency Microsoft.OpenApi lên 2.7.5, bản vá cùng major theo advisory GHSA-v5pm-xwqc-g5wc; giữ Microsoft.AspNetCore.OpenApi hiện tại.
- Nhánh này mới có service/controller mock, DbContext và UnitOfWork khung; chưa có entity/repository/CRUD User và Question đầy đủ. Lỗi accessor IUnitOfWork ở main không tồn tại tại đây. Không đánh dấu C01 của main là DONE chỉ vì quang build được.
- Giữ nguyên cấu hình appsettings.json đang được người dùng chỉnh. Chưa kiểm chứng SQL Server hoặc chức năng nghiệp vụ.
- Sau cập nhật: `dotnet build SWD392_Group3.slnx` PASS, 0 warning / 0 error. API khởi động Development thành công; GET /openapi/v1.json trả tài liệu OpenAPI; POST /interviewHub/negotiate trả HTTP 200. Đã dừng server sau kiểm tra. Commit đề xuất: `fix(api): update vulnerable OpenAPI dependency` (chưa commit).


Cập nhật: 03/10/2026 (Asia/Saigon). Baseline: `main`, commit `c27221a`.
Đây là nguồn tiến độ chung: đọc trước khi code, nhận việc trước khi sửa và cập nhật sau mỗi phiên. Trạng thái dưới đây dựa trên code local và lịch sử Git; không xác nhận công việc chưa push của thành viên hoặc nhánh khác.

## Kết luận kiểm tra Onion Architecture

**Đúng hướng phụ thuộc ở cấp project, nhưng chưa thể xem là backend hoàn chỉnh hoặc build được.**

| Layer | Phụ thuộc thực tế | Đánh giá |
| --- | --- | --- |
| Domain | Không có ProjectReference/PackageReference | Đúng: entity và enum không phụ thuộc EF, HTTP hoặc Infrastructure |
| Application | Domain; AutoMapper, FluentValidation | Đúng: use case dùng interface repository/external service |
| Infrastructure | Application, Domain; EF Core | Đúng: persistence và implementation nằm ngoài core |
| WebApi | Application, Infrastructure | Chấp nhận cho composition root ở Program.cs; controller/hub dùng service interface |

Không thấy Application/Domain import EF Core, ASP.NET hoặc Infrastructure. Việc WebApi đăng ký concrete implementation ở Program.cs không tự nó vi phạm Onion. Có thể gom đăng ký vào AddApplication/AddInfrastructure để dễ bảo trì, không phải ưu tiên chữa lỗi hiện tại.

Các điểm nên cải thiện: GetCoursesAsync trả Domain entity trực tiếp ra API (nên dùng CourseDto); validation câu hỏi hiện được gọi tại controller, cần bảo đảm use case vẫn được bảo vệ khi gọi từ nơi khác; Domain mới chủ yếu chứa dữ liệu, quy tắc nghiệp vụ cần bổ sung khi triển khai ca thi/chấm điểm.

Tham chiếu nguyên tắc phụ thuộc vào core: [Microsoft — Common web application architectures](https://learn.microsoft.com/en-us/dotnet/architecture/modern-web-apps-azure/common-web-application-architectures).

## Giai đoạn hiện tại

**Nền tảng 4 layer + CRUD đang phát triển; chưa đến giai đoạn thi AI chạy xuyên suốt.** Không đặt phần trăm hoàn thành vì chưa có backlog và tiêu chí nghiệm thu đầy đủ.

| Hạng mục | Trạng thái | Bằng chứng / phần còn thiếu |
| --- | --- | --- |
| Solution, DI, Swagger, CORS | Có khung | 4 project; Program.cs đăng ký service, repository, SignalR |
| Question Bank + Rubric | Có code, chưa xác minh chạy | CRUD, lọc, phân trang, mapping, validator, soft delete; cần kiểm tra DB/API và giới hạn phân trang/CourseId |
| Course | Một phần | Entity, repository generic, seed, endpoint danh sách; chưa có CRUD quản lý riêng |
| User | Chưa tích hợp xong | Có entity/config/DTO/repository/service/controller; lỗi IUnitOfWork, thiếu DbSet và implementation Users; chưa có migration Users |
| Import câu hỏi | Mock | Controller gửi dummyPath, service luôn trả true |
| SignalR interview | Có khung | Hub gọi service và gửi ReceiveAIFollowUp; chưa có ca thi/transcript hoặc quản lý quyền |
| OpenAI | Mock | OpenAIService trả chuỗi cố định; InterviewService chưa dùng IOpenAIService |
| Chấm điểm | Mock | GradingService luôn trả 8.5; chưa lưu Assessment hoặc luồng giảng viên chốt điểm |
| Speech STT/TTS | Chưa thấy triển khai | Chưa có interface/implementation Speech trong cây source |
| Auth / phân quyền | Chưa thấy triển khai | Không có cấu hình authentication, endpoint login/token hoặc Authorize bảo vệ API/hub |
| ExamSession / Transcript / Assessment | Mới ở thiết kế | Có trong Conceptual_ERD.md, chưa có entity/migration/use case tương ứng |
| Kiểm thử / CI | Chưa thấy trong file được Git theo dõi | Chưa có test project hoặc workflow |

Lịch sử gần nhất: `iamnhtf` thêm User từ entity đến controller (57ab063 → c27221a); trước đó `Nguyen Bui Đang Khoi` chỉnh nền tảng/Swagger/cấu hình local. Đây là tác giả commit, không tự suy ra người nhận các việc tiếp theo. Thông điệp commit nói đã nối User nhưng source thực tế vẫn thiếu.

## Lỗi và ưu tiên xử lý

| ID | Mức | Phát hiện | Điều kiện đóng |
| --- | --- | --- | --- |
| BE-01 | P0 | Application/Interfaces/Repositories/IUnitOfWork.cs bị chèn Users vào accessor của Questions/Courses/Rubrics, sai cú pháp | Khôi phục các property hợp lệ; khai báo Users đúng một lần; build pass |
| BE-02 | P0 | UserRepository dùng _context.Users nhưng AivesDbContext chưa có Users; UnitOfWork chưa implement Users | Nối DbSet và repository qua UoW; kiểm tra API User với DB |
| BE-03 | P0 | Cả 4 project target net10.0; đã cài SDK 10.0.401 (trước đó chỉ có 9.0.318); README vẫn ghi .NET 8 | Thống nhất SDK/target/packages/setup; build trên máy nhóm thành công |
| BE-04 | P1 | UserService gán dto.Password thẳng vào PasswordHash | Hash qua abstraction phù hợp; kiểm tra dữ liệu lưu không chứa password nguyên văn |
| BE-05 | P1 | Migration/snapshot hiện chưa có Users | Tạo migration sau khi chốt model; áp dụng lên DB phát triển và xác minh CRUD |
| BE-06 | P1 | UsersController bắt mọi Exception thành 400/404; chưa validate DTO/role và chưa auth | Phân loại lỗi nghiệp vụ/hệ thống, validation và quyền truy cập; kiểm tra 400/404/409/401/403 phù hợp |
| BE-07 | P1 | Query câu hỏi chưa giới hạn PageIndex/PageSize; create chỉ kiểm tra CourseId khác rỗng | Kiểm tra input và môn tồn tại; xử lý lỗi ổn định |
| BE-08 | P2 | README mô tả feature/file chưa triển khai, SDK và URL không khớp code | Tài liệu phân biệt planned/implemented và hướng dẫn chạy đúng |

Kiểm tra lần đầu: `dotnet build SWD392_Group3.slnx --no-restore` → FAIL, NETSDK1045. Theo yêu cầu người dùng, đã cài SDK 10.0.401 thành công. Build lại bằng `dotnet build SWD392_Group3.slnx`: restore cả 4 project thành công, Domain build thành công, Application FAIL với 6 lỗi CS1014 tại IUnitOfWork.cs dòng 7/9/11. BE-01 đã được compiler xác nhận; BE-02 vẫn là phát hiện qua đọc source, cần kiểm tra lại sau khi sửa BE-01. Chưa xác minh runtime, migration hay kết nối SQL Server. Không thay target framework hoặc sửa code nghiệp vụ trong phiên review này.

## Bảng nhận việc chung

Trạng thái: TODO → IN_PROGRESS → REVIEW → DONE; BLOCKED phải ghi lý do. Owner hiện chưa phân công. Mỗi việc chỉ một người chịu trách nhiệm chính.

| Task | Phạm vi file chính | Phụ thuộc | Owner | Trạng thái | Tiêu chí nghiệm thu |
| --- | --- | --- | --- | --- | --- |
| T01 Sửa tích hợp User | IUnitOfWork, UnitOfWork, AivesDbContext | Chốt SDK để build | Chưa nhận | TODO | BE-01/02 đóng; build pass |
| T02 Đồng bộ môi trường | *.csproj, README, global.json nếu cần | Nhóm chốt phiên bản | Chưa nhận | TODO | BE-03 đóng; hướng dẫn setup tái lập được |
| T03 Hoàn thiện User/Auth | User DTO/service/controller/validator; abstraction hash/auth và implementation | T01, T02 | Chưa nhận | TODO | BE-04/06 đóng; CRUD và phân quyền có kiểm chứng |
| T04 Migration User | Infrastructure/Migrations, UserConfiguration | T01; chốt model với T03 | Chưa nhận | TODO | BE-05 đóng; migration DB dev thành công |
| T05 Hoàn thiện Question Bank | Question DTO/service/validator/repository/controller | T02 | Chưa nhận | TODO | CRUD/rubric/soft delete/phân trang được kiểm chứng; BE-07 đóng |
| T06 Import thật | Import contract/service/controller và parser Infrastructure | T05; chốt định dạng file | Chưa nhận | TODO | Upload/import có báo lỗi từng dòng, không còn dummyPath/return true |
| T07 Ca thi + Transcript | Entity/DTO/interface/use case mới; persistence tương ứng | T03, T05; chốt ERD | Chưa nhận | TODO | Tạo/bắt đầu/kết thúc ca thi, lưu hội thoại và kiểm soát quyền |
| T08 OpenAI + SignalR | IOpenAIService, OpenAIService, InterviewService, InterviewHub | T07 | Chưa nhận | TODO | Gọi AI thật; lỗi/timeout được xử lý; hội thoại gắn đúng ca thi |
| T09 Speech | Interface Application, implementation Infrastructure | Chốt provider/audio contract; T08 để tích hợp | Chưa nhận | TODO | STT/TTS được kiểm chứng và kết nối luồng thi |
| T10 Grading + lecturer decision | Assessment entity/use case/repository/API; GradingService | T07, T08 | Chưa nhận | TODO | Điểm theo rubric, lưu đề xuất, giảng viên duyệt/chốt có quyền |
| T11 Kiểm thử + tài liệu | Test project/CI, README, tài liệu API | Theo từng module hoàn thiện | Chưa nhận | TODO | Build và kiểm tra luồng chính pass; docs phản ánh code |

## Lộ trình commit nhỏ cho hai người

Mỗi mốc có checkbox và tên người làm, không chia cố định A/B. Trước khi bắt đầu, ghi tên thật và trạng thái IN_PROGRESS ở mốc nhận; khi đạt tiêu chí, đổi [ ] thành [x], ghi SHA/PR và kiểm chứng vào nhật ký. Người còn lại review chéo. Không tick chỉ vì AI đã sinh code. C00/C01 đã hoàn thành; các mốc còn lại chưa nhận.

Mỗi hàng là một commit với một mục tiêu có thể review. Các file interface/entity và implementation liên quan cần đi cùng nhau để commit build được. C01 là mốc khôi phục build; từ C02 trở đi mỗi commit phải build pass. Nếu phát sinh lỗi baseline khác, đóng chúng trong commit sửa build và ghi rõ, không trộn thêm feature.

### M0 — Khôi phục nền tảng



- [x] **C00** — Người làm: Quang (cùng Codex), commit 51135a6

  Commit: `docs: add backend roadmap and collaboration notes`

  Phạm vi / nghiệm thu: Share file note này và link README; ghi baseline build đang fail

  Cần trước: Không

- [x] **C01** — Người làm: Khoi, sửa e2672b3; Quang tích hợp qua f488a34

  Commit: `fix(user): repair unit of work and user persistence wiring`

  Phạm vi / nghiệm thu: Sửa accessor IUnitOfWork, thêm DbSet Users và implementation Users; build toàn solution pass

  Cần trước: C00

- [ ] **C02** — Người làm: Chưa nhận

  Commit: `chore: align dotnet sdk and local setup documentation`

  Phạm vi / nghiệm thu: Chốt .NET 10, cấu hình SDK theo chính sách nhóm, README đúng SDK/URL; peer build được theo hướng dẫn

  Cần trước: C01

- [ ] **C03** — Người làm: Chưa nhận

  Commit: `docs: define database and api contracts for aives`

  Phạm vi / nghiệm thu: Đối chiếu DB thực tế/ảnh với entity và migration; ghi mapping, trạng thái ca thi, role, cách tính điểm và API request/response

  Cần trước: C02


C03 là điểm chốt trước khi thêm schema. Ảnh DB có ExamSessions/Transcripts/Assessments nhưng migration trong baseline chưa có; không suy ra DB đang mở được tạo bởi migration hiện tại. Trong ảnh, Transcript giống bài thi của một sinh viên; Assessment lưu câu trả lời từng câu. Quyết định rõ nơi lưu lượt hội thoại (speaker, nội dung, timestamp, câu hỏi) để hỗ trợ vấn đáp. Xác minh kiểu PK/FK, quan hệ và DB dev cần giữ dữ liệu; không tạo initial migration mới đè DB hoặc tự đổi tên bảng khi chưa có mapping.

### M1 — CRUD và tài khoản dùng được

Sau C03, hai người có thể nhận các mốc không phụ thuộc nhau. Ghi tên người giữ quyền tích hợp file chung và migration trong nhật ký trước khi code.



- [ ] **C04** — Người làm: Chưa nhận

  Commit: `feat(user): hash passwords through application abstraction`

  Phạm vi / nghiệm thu: Contract hash ở Application, implementation ở Infrastructure, UserService sử dụng; kiểm tra hash/verify và không lưu password nguyên văn

  Cần trước: C03

- [ ] **C05** — Người làm: Chưa nhận

  Commit: `fix(api): standardize business error responses`

  Phạm vi / nghiệm thu: Lỗi nghiệp vụ có loại rõ, xử lý tập trung; lỗi hệ thống không bị báo nhầm 404 và không lộ chi tiết nội bộ

  Cần trước: C04

- [ ] **C06** — Người làm: Chưa nhận

  Commit: `feat(user): validate user input and role changes`

  Phạm vi / nghiệm thu: Validator, kiểm tra trùng username và role hợp lệ; request lỗi có phản hồi ổn định

  Cần trước: C05

- [ ] **C07** — Người làm: Chưa nhận

  Commit: `feat(db): add user schema migration`

  Phạm vi / nghiệm thu: Migration và snapshot đồng bộ model; kiểm tra cập nhật DB dev và CRUD User

  Cần trước: C06

- [ ] **C08** — Người làm: Chưa nhận

  Commit: `fix(question): validate paging and course references`

  Phạm vi / nghiệm thu: PageIndex/PageSize có giới hạn; CourseId tồn tại; kiểm tra input sai và lọc/phân trang

  Cần trước: C03

- [ ] **C09** — Người làm: Chưa nhận

  Commit: `fix(question): verify rubric replacement and soft deletion`

  Phạm vi / nghiệm thu: Cập nhật rubric không để dữ liệu cũ sai; tổng trọng số hợp lệ; câu hỏi/rubric đã xóa không xuất hiện

  Cần trước: C08

- [ ] **C10** — Người làm: Chưa nhận

  Commit: `feat(course): expose course management through dtos`

  Phạm vi / nghiệm thu: Course DTO/use case/controller, CRUD theo contract; không trả entity trực tiếp; kiểm tra môn không tồn tại

  Cần trước: C09

- [ ] **C11** — Người làm: Chưa nhận

  Commit: `feat(auth): add login and token verification`

  Phạm vi / nghiệm thu: Login xác minh hash, token có hạn dùng/cấu hình ngoài source; kiểm tra login sai và token hết hạn

  Cần trước: C07

- [ ] **C12** — Người làm: Chưa nhận

  Commit: `feat(auth): protect user and question management by role`

  Phạm vi / nghiệm thu: Áp dụng quyền theo C03 cho API; người dùng không tự nâng role; kiểm tra 401/403 và lecturer/student

  Cần trước: C10, C11


Gate M1: hai người chạy trên DB dev của mình; tạo user, login, quản lý course/question/rubric và soft delete thành công. Cập nhật SHA các commit và ví dụ gọi API trong note. Không tuyên bố chức năng thi AI đã xong.

### M2 — Thi bằng văn bản chạy xuyên suốt



- [ ] **C13** — Người làm: Chưa nhận

  Commit: `feat(exam): add session and student attempt models`

  Phạm vi / nghiệm thu: ExamSession, Transcript/bài thi, lượt hội thoại theo C03; EF configuration + migration cùng commit; kiểm tra quan hệ FK

  Cần trước: C12

- [ ] **C14** — Người làm: Chưa nhận

  Commit: `feat(exam): manage session lifecycle`

  Phạm vi / nghiệm thu: Tạo/mở/đóng ca thi; validate course, thời gian, trạng thái và quyền lecturer; không cho chuyển trạng thái trái quy tắc

  Cần trước: C13

- [ ] **C15** — Người làm: Chưa nhận

  Commit: `feat(exam): start attempts and persist answer turns`

  Phạm vi / nghiệm thu: Student vào phiên hợp lệ; lưu câu trả lời theo đúng user/session/question; không truy cập bài thi người khác

  Cần trước: C14

- [ ] **C16** — Người làm: Chưa nhận

  Commit: `feat(ai): implement structured ai provider integration`

  Phạm vi / nghiệm thu: Adapter gọi AI thật, cấu hình secret bên ngoài, timeout/cancellation và lỗi provider; kiểm tra response hợp lệ/lỗi bằng fake provider, smoke test thật khi có key

  Cần trước: C03; merge sau C15

- [ ] **C17** — Người làm: Chưa nhận

  Commit: `feat(interview): generate follow-up questions from exam context`

  Phạm vi / nghiệm thu: InterviewService dùng abstraction AI, ngữ cảnh môn/câu hỏi/hội thoại; lưu lượt AI, không dùng chuỗi mock

  Cần trước: C15, C16

- [ ] **C18** — Người làm: Chưa nhận

  Commit: `feat(signalr): authorize interview messages by attempt`

  Phạm vi / nghiệm thu: Hub xác thực, kiểm tra ownership/state phía server; gửi đúng người/phiên; không tin studentId do client tự gửi

  Cần trước: C17

- [ ] **C19** — Người làm: Chưa nhận

  Commit: `test(exam): cover the text viva workflow`

  Phạm vi / nghiệm thu: Kiểm tra login → vào ca thi → trả lời → nhận câu hỏi tiếp → lưu hội thoại → kết thúc; cả đường lỗi quyền/state/provider

  Cần trước: C18


Gate M2: demo một ca thi văn bản có dữ liệu lưu thật và AI phản hồi thật khi cấu hình provider. Fake provider phục vụ test phải được phân biệt rõ với môi trường demo.

### M3 — Chấm điểm và giảng viên duyệt



- [ ] **C20** — Người làm: Chưa nhận

  Commit: `feat(grading): add assessment contracts and score rules`

  Phạm vi / nghiệm thu: Entity Assessment, DTO và quy tắc điểm theo rubric/C03; Người còn lại review schema; tích hợp configuration/migration trong cùng commit

  Cần trước: C19

- [ ] **C21** — Người làm: Chưa nhận

  Commit: `feat(grading): persist ai scores and feedback per answer`

  Phạm vi / nghiệm thu: GradingService bỏ 8.5 cố định; lưu điểm đề xuất/feedback/confidence; kiểm tra cấu trúc và khoảng điểm AI trả về

  Cần trước: C20

- [ ] **C22** — Người làm: Chưa nhận

  Commit: `feat(assessment): let lecturers review and finalize scores`

  Phạm vi / nghiệm thu: API giảng viên xem/chốt điểm và comment; audit ai chốt/khi nào; student không thể chốt; không âm thầm ghi đè điểm đã chốt

  Cần trước: C21

- [ ] **C23** — Người làm: Chưa nhận

  Commit: `feat(result): calculate totals and expose authorized results`

  Phạm vi / nghiệm thu: Tổng điểm theo quy tắc đã chốt, phân biệt đề xuất/chính thức; quyền xem kết quả và xử lý bài chưa chấm đủ

  Cần trước: C22

- [ ] **C24** — Người làm: Chưa nhận

  Commit: `test(grading): verify scoring and lecturer decisions`

  Phạm vi / nghiệm thu: Kiểm tra tính điểm, output AI sai, retry không nhân đôi assessment và quyền chốt; demo kết quả lưu DB

  Cần trước: C23


Gate M3: thi văn bản → AI đề xuất điểm → giảng viên duyệt → sinh viên xem kết quả chính thức. Đây là MVP backend trước khi thêm voice.

### M4 — Voice, import và ổn định bản bàn giao



- [ ] **C25** — Người làm: Chưa nhận

  Commit: `feat(import): parse and validate question uploads`

  Phạm vi / nghiệm thu: Chốt định dạng; upload thật, giới hạn file, lỗi theo dòng; bỏ dummyPath; ghi rõ chính sách import một phần/toàn bộ

  Cần trước: C12; có thể làm sớm nếu M2 chưa cần

- [ ] **C26** — Người làm: Chưa nhận

  Commit: `feat(speech): add speech-to-text provider adapter`

  Phạm vi / nghiệm thu: Contract ở Application, adapter Infrastructure; kiểm tra audio hợp lệ/lỗi/timeout và tiếng Việt

  Cần trước: C24; chốt provider

- [ ] **C27** — Người làm: Chưa nhận

  Commit: `feat(speech): add text-to-speech provider adapter`

  Phạm vi / nghiệm thu: Nhận text và tạo audio; kiểm tra cấu hình, lỗi provider và định dạng frontend nhận được

  Cần trước: C26

- [ ] **C28** — Người làm: Chưa nhận

  Commit: `feat(interview): connect voice input and spoken responses`

  Phạm vi / nghiệm thu: Nối STT → use case trả lời → AI → TTS; giữ luồng text dự phòng; kiểm tra quyền, lưu lượt, kết thúc phiên

  Cần trước: C27

- [ ] **C29** — Người làm: Chưa nhận

  Commit: `chore(ci): automate backend build and verification`

  Phạm vi / nghiệm thu: CI dùng .NET 10, build/test không cần secret thật; cấu hình môi trường demo và health check phù hợp

  Cần trước: C24, C25, C28

- [ ] **C30** — Người làm: Chưa nhận

  Commit: `docs: publish backend setup and demo acceptance checklist`

  Phạm vi / nghiệm thu: README đúng code, hướng dẫn migration/config, API/SignalR contract, kết quả demo và giới hạn còn lại

  Cần trước: C29


Gate M4: hai máy dựng backend từ tài liệu được, các kiểm tra pass, demo voice + chấm/duyệt điểm hoàn chỉnh. Không cần đợi cuối dự án mới viết test: thêm kiểm tra nghiệp vụ/rủi ro cùng commit chức năng; C19/C24 bổ sung kiểm tra xuyên suốt.

### Cách share commit và giao việc cho AI

- Share một branch/PR chứa một vài commit liên quan theo gate. Peer kéo commit nền tảng đã merge trước khi làm task phụ thuộc; không cherry-pick riêng migration thiếu entity/configuration hoặc service thiếu interface.
- Mỗi thông báo bàn giao ghi: mã Cxx, SHA thật, branch/PR, contract thay đổi, cách kiểm tra, blocker và mốc peer có thể bắt đầu. Cập nhật bảng Txx tương ứng: T01=C01, T02=C02, T03=C04–06/C11–12, T04=C07, T05=C08–10, T06=C25, T07=C13–15, T08=C16–19, T09=C26–28, T10=C20–24, T11=kiểm tra xuyên suốt/C29–30.
- Trong một checkout chỉ một người/agent sửa tại một thời điểm. Mỗi người dùng branch và checkout riêng; code song song không đồng nghĩa sửa chung file. Với file chung, bên không giữ quyền tích hợp bàn giao yêu cầu cụ thể thay vì tự sửa.
- Khi AI hoàn thành, đọc diff và chạy kiểm chứng trước khi commit. Nếu mốc cần chia thêm, dùng Cxx.a/Cxx.b và ghi điều kiện build của từng commit; không gộp các mốc chưa liên quan.

Prompt mẫu cho mỗi phiên:

```text
Đọc docs/BACKEND_PROGRESS.md. Thực hiện duy nhất mốc Cxx trên branch hiện tại.
Người làm: <tên thật>. Baseline commit: <SHA thật>. Phụ thuộc đã merge: <SHA thật>.
Phạm vi file được sửa: <ghi cụ thể>. File người khác đang giữ: <ghi cụ thể>.
Giữ Onion Architecture và contract đã chốt tại C03.
Triển khai đầy đủ tiêu chí mốc, kiểm tra phù hợp và cập nhật nhật ký bàn giao.
Nếu cần đổi contract/schema hoặc chạm file người khác giữ, nêu yêu cầu phối hợp.
Không ghi secret; không đánh dấu DONE nếu chưa kiểm chứng.
Báo diff, kết quả kiểm tra, blocker và commit message đề xuất.
```

## Quy tắc nhiều người cùng vibe code

1. Trước phiên: đọc file này, kiểm tra branch/status, ghi Owner, IN_PROGRESS, branch và phạm vi sửa vào nhật ký. Nếu task đang có owner, phối hợp trước khi chạm cùng file.
2. Mỗi task dùng branch riêng, ví dụ `codex/t01-user-integration`. Các checkout khác nhau dùng branch/worktree riêng; tránh hai agent sửa đồng thời trên một working directory.
3. Giữ hướng phụ thuộc: Domain không biết layer ngoài; Application dùng abstraction; EF/API bên ngoài ở Infrastructure; controller/hub chỉ điều phối HTTP/SignalR và gọi use case.
4. Chốt contract DTO/interface/entity trước khi làm hai module phụ thuộc nhau. Ghi thay đổi contract và người bị ảnh hưởng trong nhật ký.
5. File chung (Program.cs, *.csproj, AivesDbContext, IUnitOfWork, UnitOfWork, MappingProfile, migrations, README): chỉ một owner sửa tại một thời điểm. Người khác ghi yêu cầu tích hợp vào task/PR.
6. Migration do một người tạo sau khi đồng bộ model; không để nhiều người tạo migration song song từ snapshot khác nhau.
7. Kết thúc phiên: ghi file đã sửa, kết quả build/test, commit/PR, blocker và bước tiếp theo. Có code chưa chạy được thì ghi REVIEW/BLOCKED, không ghi DONE.
8. DONE khi đáp ứng tiêu chí task, đã build/kiểm tra phù hợp, bỏ mock trong phạm vi và được review. Không coi DI/endpoint tồn tại là feature đã hoàn thành.
9. Trước merge: cập nhật từ branch chung, xử lý conflict, chạy lại kiểm tra bị ảnh hưởng và cập nhật bảng/nhật ký. Không ghi secret hoặc chuỗi kết nối riêng vào tài liệu.

## Nhật ký bàn giao

Thêm một dòng sau mỗi phiên; giữ các dòng cũ. Khi hai branch cùng cập nhật tài liệu, giữ cả hai bản ghi khi giải quyết conflict.

| Ngày | Người / task / branch | Đã làm / file ảnh hưởng | Kiểm chứng | Blocker / bước tiếp theo | Commit / PR |
| --- | --- | --- | --- | --- | --- |
| 03/10/2026 | Codex / review / main | Kiểm tra 4 layer, source, commit; tạo tài liệu tiến độ | Đã cài SDK 10.0.401; restore pass; build FAIL 6 lỗi CS1014 ở IUnitOfWork | Ưu tiên T01; T02 còn đồng bộ README/setup; chưa kiểm tra runtime DB | Baseline c27221a; tài liệu chưa commit |
| 03/10/2026 | Quang cùng Codex / C01 tích hợp / quang | Merge backend khoi; chuyển roadmap thành checklist có tên người làm | Build output tạm PASS, 0 warning / 0 error; kiểm tra đủ 31 checkbox | Tiếp tục C02/C03; CRUD DB chưa kiểm chứng; cấu hình local vẫn nằm trong stash đã lưu | Sửa gốc e2672b3; merge f488a34 |

Mẫu bàn giao cho phiên tiếp theo:

```text
Task / Owner / Branch:
Trạng thái:
Đã hoàn thành:
File đã sửa và contract đã đổi:
Build/test đã chạy và kết quả:
Blocker:
Bước tiếp theo:
Commit/PR:
```

