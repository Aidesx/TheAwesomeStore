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
- [ ] Chạy thử image bằng Docker/Podman. Đã build image archive bằng container tooling tích hợp trong SDK 11; máy chưa có engine để chạy container.
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
- SDK 11 đã tạo Docker-compatible archive `bin/Project385-net11.oci.tar` (`project-385:net11-verify`) trên base `mcr.microsoft.com/dotnet/aspnet:11.0`; manifest có 8 layers và entrypoint `dotnet /app/Project 385.dll`. Docker CLI/engine chưa có để import và chạy image.
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
- Xác minh lại ngày 23/11/2026: restore bằng SDK 11.0.100 thành công; build Release 0 warning/0 error; publish Release thành công tại `bin/publish-net11`.
- Khởi động artifact publish trên LocalDB với database dùng riêng `Shop_Net11_Verify_20261123`; ứng dụng tự áp dụng migration và seed, sau đó Home/Details/Search/Login/Register/Cart/Checkout trả 200, Admin trả 302 khi chưa đăng nhập. Database xác minh đã được xóa sau kiểm tra; database dùng thật không bị chạm tới.
- Quét NuGet advisory hiện tại: không tìm thấy package vulnerable.
- Callback MoMo giả với chữ ký sai trên một LocalDB riêng bị từ chối đúng cách (HTTP 302 về `/Cart`); database tạm đã xóa. Đây chỉ xác nhận nhánh từ chối chữ ký, không thay thế callback thành công từ sandbox/credential MoMo thật.
- Docker CLI không có trong môi trường; chưa có test project riêng nên `dotnet test` không cung cấp kết quả test tự động. Còn chờ Docker/container smoke test và luồng tạo đơn/payment với credential thật.
- Container tooling tích hợp SDK 11 đã build image archive thành công trên base ASP.NET 11; đã xác minh tag, entrypoint và manifest. Chưa thể chạy image để kiểm tra health/logs/graceful shutdown vì chưa có container engine.
