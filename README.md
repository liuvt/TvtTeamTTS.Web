# TVT Team · TvtTeamTTS.Web

Ứng dụng chuyển văn bản thành giọng nói tiếng Việt, chạy offline trên Windows bằng Blazor Server và .NET 9.

![Logo TVT Team](wwwroot/images/tvt-logo.png)

## Chức năng v2.2

- **Sửa lỗi dán văn bản dài:** JavaScript giữ toàn bộ text ở trình duyệt, chỉ gửi số ký tự/từ cho Blazor Server khi nhập; khi bấm render mới gửi 1 lần văn bản hợp lệ. Tăng giới hạn SignalR lên 512 KiB để nhận Unicode tiếng Việt <= 50.000 ký tự; nội dung dài hơn vẫn dán được nhưng hiển thị cảnh báo và ẩn nút render.
- Nhập tối đa **50.000 ký tự** (bao gồm khoảng trắng và xuống dòng). Hiển thị bộ đếm `0 / 50.000 ký tự`; khi vượt giới hạn sẽ hiện cảnh báo và **ẩn nút render**.
- Ba nhóm giọng: **Nữ / Nam / Khác**. Tự phân loại theo thư mục `Voices/Nữ`, `Voices/Nam`, `Voices/Khác`, hỗ trợ model `.onnx` và `.onnx.json` hoặc `config.json`.
- Mặc định chọn **Nữ**, ưu tiên **Ngọc Huyền** nếu model hiện diện.
- **Tốc độ phát video**: chỉ cho phép `0,25x`, `0,5x`, `0,75x`, `1x`, `1,25x`, `1,75x`, `2x`. Đây là tốc độ đọc được áp dụng khi tổng hợp MP3; ứng dụng không xuất file video.
- Ghép các đoạn thành MP3 192 kbps; tự tải về **một lần khi render thành công**; có nút tải thủ công dự phòng nếu trình duyệt chặn tải.
- Mỗi tab có một mã phiên và thư mục `Output` riêng. Gửi tín hiệu dọn file khi thoát/đóng tab; tự dọn phiên cũ sau 2 giờ nếu tín hiệu đóng tab thất bại.
- Giao diện GitHub-style (dark mode), logo TVT Team, responsive.
- Có tiến độ, dừng render và thông báo lỗi.

## Cài đặt nhanh

1. Cài **.NET 9 SDK** trên Windows 10/11 (x64).
2. Chép engine từ bản chạy cũ vào `Runtime/piper/` (phải có `piper.exe` và các tệp đi kèm) và FFmpeg vào `Runtime/ffmpeg/ffmpeg.exe`.
3. Đặt cặp model vào thư mục giọng tương ứng, ví dụ:

```text
Voices/
  Nữ/
    NgocHuyen/
      ngochuyen.onnx
      ngochuyen.onnx.json
  Nam/
    NgocNgan/
      ngocngan.onnx
      ngocngan.onnx.json
  Khác/
    VoiceTuyChinh/
      voice.onnx
      voice.onnx.json
```

4. Chạy tại thư mục chứa `.csproj`:

```bat
dotnet restore
dotnet build -c Release
dotnet run --project TvtTeamTTS.Web.csproj --urls http://localhost:4110
```

5. Mở `http://localhost:4110`, dán văn bản, chọn giọng và bấm **Tạo & tải MP3**.

**Lưu ý:** ZIP này là **source code**, không kèm runtime engine, FFmpeg hoặc model ONNX (để tránh tệp nặng và ảnh hưởng bản quyền). Mặc định sẽ chưa có giọng đọc nếu bạn chưa thêm model. Có thể dùng lại engine/model từ bản cài hiện tại.

Chi tiết và cách xử lý lỗi: [HUONG_DAN_CAI_DAT.md](HUONG_DAN_CAI_DAT.md).

## Khắc phục lỗi giọng Nữ/Khác (`-1073740791`)

Bản v2.1 bổ sung **alias đường dẫn ASCII** khi model/JSON nằm trong thư mục có dấu (`Nữ`, `Khác`) và đảm bảo file WAV tạm cũng nằm trên đường dẫn ASCII. Engine gốc được chạy với thư mục làm việc đúng nơi chứa các DLL/dữ liệu. Cách khắc phục và kiểm tra lỗi riêng: [KHAC_PHUC_LOI_0xC0000409.md](KHAC_PHUC_LOI_0xC0000409.md).

## Kỹ thuật

- .NET 9.0 / Razor Components / Interactive Server.
- Namespace, solution, project: `TvtTeamTTS.Web`.
- API download: `GET /download/{sessionId}/{file}` — tên file sinh tự động và khó đoán, ngăn ghi đè giữa các phiên.
- API cleanup: `POST /api/session/{sessionId}/close` — được trình duyệt gọi bằng `sendBeacon` khi tab đóng.
- API heartbeat: `POST /api/session/{sessionId}/touch` — giữ phiên đang sử dụng tồn tại.
- File WAV tạm trong `Temp/` được dọn sau mỗi lượt render (bao gồm khi bị hủy và lỗi thông thường).
- Các file MP3 được dọn sau khi tab đóng; trình duyệt có thể mất tín hiệu dọn khi bị kill đột ngột, nên có thêm cơ chế dọn định kỳ.

> Không mở port ra Internet nếu chưa thêm xác thực và các biện pháp giới hạn sử dụng. App được thiết kế chạy trên máy nội bộ/localhost.

## Bản v2.2 — Khắc phục không dán được văn bản dài

- **Nguyên nhân v2.1:** `@bind:event="oninput"` gửi lại toàn bộ textarea trong từng sự kiện, có thể vượt ngưỡng ~32 KiB mặc định của SignalR.
- **Giải pháp:** JavaScript quản lý textarea và đếm từ/ký tự ngay tại trình duyệt; tín hiệu gửi về Blazor chỉ gồm 2 số nguyên. Không tăng tải mỗi phím bấm.
- Khi chọn **Tạo & tải MP3**, JS chỉ gửi văn bản tối đa 50.000 ký tự. Server cũng kiểm tra lại giới hạn.
- Nếu dán 25.000 **từ** (thường > 50.000 **ký tự**) thì văn bản vẫn hiện trong khung nhập, bộ đếm báo quá hạn và nút render ẩn theo đúng yêu cầu.
- Nhấn `Ctrl+F5` sau cập nhật để Chrome lấy `studio.js` mới, hoặc xóa cache của trang localhost.

> Lưu ý: Chưa chạy kiểm thử biên dịch/render trên Windows trong môi trường này; cần `dotnet build` và kiểm tra thử với máy cài SDK/engine thực tế.


## Bản v2.3 — Sửa cảnh báo ảo và thiết kế Studio mới

- **Fix cảnh báo giả khi textarea trống:** `.message { display:flex }` có thể đè thuộc tính HTML `hidden`. Khởi tạo phần cảnh báo với `hidden style="display:none"`; JS đồng bộ `hidden` + `style.display` và chỉ hiển thị khi `textarea.value.length > 50000`.
- Gắn version query `app.css?v=2.3.1` và `studio.js?v=2.3.1` để hạn chế Chrome tải file cũ từ cache.
- Thiết kế lại giao diện theo bố cục web utility của clip2down.store: header gọn, hero giới thiệu, card chính tập trung vào nhập văn bản, nhóm giọng, tốc độ, thao tác xuất MP3, phần 3 bước hướng dẫn. Giữ thương hiệu và logo TVT Team.
- Nút Dán và Xóa ngay trên trình soạn thảo; thanh thể hiện tỷ lệ ký tự; không bao giờ tự xóa văn bản khi quá 50.000.
- Giữ nguyên cấu trúc engine/voice, logic download và cơ chế dọn file của bản v2.2.

Cập nhật source, sao chép `Runtime` và `Voices` từ bản cũ, chạy `dotnet restore`, `dotnet build -c Release`, `dotnet run --urls http://localhost:4110`.
