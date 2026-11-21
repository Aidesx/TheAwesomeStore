# Kế hoạch chuyển dự án sang .NET 11

## Mục tiêu

Chuyển ứng dụng ASP.NET Core MVC từ `net8.0` sang `.NET 11`, giữ hành vi hiện tại, nâng đồng bộ thư viện nền tảng, xác minh database/deployment và không đưa secrets, `.skill` hoặc `.codex` vào Git.

## Trạng thái

- [x] Chốt .NET 11 làm đích nâng cấp theo yêu cầu.
- [x] Cập nhật `global.json` sang SDK `11.0.100`.
- [x] Cập nhật target framework và package Microsoft/EF Core sang 11.0.0.
- [x] Cập nhật Docker SDK/runtime image sang 11.0.
- [x] Cài đặt/kiểm tra SDK 11 trong môi trường đang thao tác (11.0.100).
- [x] Restore sạch và kiểm tra tương thích toàn bộ NuGet dependencies.
- [x] Xử lý warning từ build (EF Core 11 `FromSqlInterpolated` đã chuyển sang `FromSql`).
- [x] Xác minh ứng dụng EF Core 11 truy cập LocalDB và schema hiện tại.
- [x] Quét advisory NuGet: hiện không package nào bị báo vulnerable; cập nhật các package patch/tooling tương thích.
- [ ] Kiểm tra credential thật trên môi trường triển khai (repository chỉ có placeholder; chưa có quyền truy cập cấu hình deploy).
- [ ] Build/publish Docker image và chạy thử. Docker Desktop/WSL chưa cài; cài WSL qua winget bị Windows từ chối vì yêu cầu quyền administrator (`0x80073d28`).
- [x] Smoke test HTTP trang chủ, chi tiết sản phẩm và tìm kiếm (đều trả 200).
- [x] Smoke test trang Identity, cart, checkout và quyền admin cơ bản.
- [ ] Smoke test tạo đơn/payment callback với tài khoản và credential thanh toán thật.
- [x] Commit riêng nâng cấp framework sau khi hoàn tất các kiểm tra cục bộ.

## Trình tự thực hiện và tiêu chí đạt

### 1. Môi trường SDK

- Cài đúng .NET 11 SDK và xác nhận `dotnet --version` trong thư mục dự án trả `11.0.100`.
- Xác nhận runtime/container platform triển khai hỗ trợ .NET 11.
- Không đổi `global.json` để lách SDK resolver.

### 2. Dependency restore và tương thích

- Xóa output build cũ (`bin`, `obj`) rồi chạy restore sạch bằng SDK 11.
- Cập nhật các package Microsoft ASP.NET Core và EF Core đồng bộ cùng major.
- Kiểm tra package bên thứ ba: `Microsoft.Net.Compilers.Toolset`, Azure Containers Tools, Newtonsoft.Json, RestSharp, SkiaSharp.
- Ghi nhận và xử lý package deprecated, advisory hoặc không tương thích; không nâng package không liên quan khi chưa cần.
- RestSharp 106.15.0 hiện chưa được nâng major vì chỉ dùng trong MoMo; major mới cần cập nhật API và kiểm chứng riêng.

### 3. Compile và breaking changes

- Build Debug và Release, sửa compile error/analyzer warnings có ý nghĩa.
- Đối chiếu ASP.NET Core/.NET/EF Core 11 breaking changes với code đang dùng.
- Giữ lịch sử migration cũ nguyên vẹn; chỉ tạo migration mới nếu model/schema có thay đổi thực sự.

### 4. Database

- Dùng bản sao database để áp dụng migration và kiểm tra rollback/khả năng phục hồi.
- Đã tạo COPY_ONLY backup, restore thành `Shop_Net11_Rehearsal`, khởi chạy ứng dụng .NET 11 trên clone và xác minh 11 migrations, ProductCount=2, truy vấn hoạt động. Đã xóa DB clone và backup sau rehearsal; database `Shop` gốc không đổi.
- Kiểm tra query translation, transaction/locking, checkout tồn kho, callback thanh toán và Identity.
- Không chạy migration trên production như một phần tự động của lần nâng cấp đầu tiên.

### 5. Security/configuration

- Quét package advisory và rà soát secrets/configuration không được commit.
- Xác minh HTTPS, cookie/authentication, Google OAuth, MoMo/email configuration và quyền admin.
- Giữ `.skill`, `.agents/skills`, `.agent/skills`, `.codex` ngoài commit.

### 6. Deployment và smoke test

- Build/publish Docker image .NET 11, kiểm tra startup, health/logs và graceful shutdown.
- Docker CLI chưa có trong môi trường. Cài WSL yêu cầu quyền administrator; Windows từ chối (`0x80073d28`), nên chưa thể cài Docker Desktop/build container.
- Smoke test trang chủ, tìm kiếm, đăng nhập/đăng ký, giỏ hàng, giới hạn tồn kho, COD, payment callback, admin CRUD.
- Chạy staging trước production; có image/database backup và phương án rollback về artifact/runtime trước nâng cấp.

## Ghi chú vòng đời

.NET 11 là STS, dự kiến được hỗ trợ đến tháng 11/2028. Đây là lựa chọn theo yêu cầu nâng cấp; cần lên lịch đánh giá bản major tiếp theo trước khi hết hỗ trợ. Việc nâng framework không tự thay thế việc cập nhật package, secrets, cấu hình và vận hành bảo mật.

## Nhật ký thực hiện

- SDK 11.0.100 được cài và chọn đúng qua `global.json`.
- Restore .NET 11 hoàn tất.
- Build Release hoàn tất với 0 warning, 0 error.
- NuGet vulnerability scan không báo package vulnerable theo nguồn hiện tại.
- HTTP smoke test: `/`, `/Product/Details/1`, `/Product/Search?search=iphone` đều trả 200.
- HTTP smoke test bổ sung: Login/Create/Cart/Checkout trả 200; Admin chuyển 302 về Login; POST thêm sản phẩm hết hàng trả 409 như mong đợi.
- Rehearsal clone: backup/restore thành công; ứng dụng .NET 11 lên clone, đủ 11 migrations và 2 sản phẩm; clone/backup đã dọn sau test.
- Smoke test clone: Home/Details/Search/Login/Register/Cart/Checkout đều trả 200.
- Còn chờ Docker/container smoke test và luồng tạo đơn/payment bằng credential thật. Docker bị chặn do cài WSL cần quyền administrator; kiểm tra credential deploy cần quyền truy cập nền tảng deploy.
