# HƯỚNG DẪN CÀI ĐẶT — TVT Team TTS Web

## 1. Yêu cầu

- Windows 10/11 x64, **.NET SDK 9** (`dotnet --list-sdks`).
- Engine tổng hợp TTS cho Windows x64: toàn bộ file vào `Runtime\piper\`, gồm `piper.exe`.
- FFmpeg build hỗ trợ `libmp3lame`: `Runtime\ffmpeg\ffmpeg.exe`.
- Các model `*.onnx` và cấu hình hợp lệ tương ứng.

Các thư mục Runtime, Voices chứa file model lớn không được đóng gói vào ZIP source.

## 2. Cấu trúc thư mục

```text
TvtTeamTTS.Web/
  TvtTeamTTS.Web.csproj
  TvtTeamTTS.Web.sln
  Program.cs
  Components/
  Services/
  Models/
  wwwroot/
    app.css
    images/tvt-logo.png
    js/studio.js
  Runtime/
    piper/piper.exe
    ffmpeg/ffmpeg.exe
  Voices/
    Nữ/NgocHuyen/ngochuyen.onnx
    Nữ/NgocHuyen/ngochuyen.onnx.json
    Nam/NgocNgan/ngocngan.onnx
    Nam/NgocNgan/ngocngan.onnx.json
    Khác/CustomVoice/model.onnx
    Khác/CustomVoice/model.onnx.json
  Temp/
  Output/
```

### Lưu ý về giọng đọc

- Nhóm giọng được xác định bằng **thư mục cấp 1** dưới `Voices`. Model trong `Voices\Nữ\...` luôn nằm trong tab Nữ, không phân biệt tên model; tương tự Nam, Khác. Bạn có thể dùng tên không dấu: `Nu`, `Nam`, `Khac`.
- Vẫn nhận diện tên một số voice cũ nếu chúng đặt trực tiếp dưới `Voices` hoặc `Voices\TenVoice`, nhưng các voice không rõ giới tính sẽ được đưa vào **Khác**.
- JSON nên đặt cạnh ONNX với tên `tenmodel.onnx.json`. Có thể dùng `config.json` chung trong thư mục model.
- Giọng Ngọc Huyền chỉ được chọn mặc định nếu có model tương ứng trong nhóm Nữ, không phải giọng dựng sẵn đi kèm source.
- Bấm **Quét lại giọng đọc** sau khi thêm file, không cần khởi động lại chương trình.

## 3. Build và chạy trên Windows CMD

```bat
cd /d D:\Dev\TvtTeamTTS.Web
dotnet restore
dotnet build TvtTeamTTS.Web.sln -c Release
dotnet run --project TvtTeamTTS.Web.csproj --urls http://localhost:4110
```

Vào `http://localhost:4110` trên trình duyệt Chrome/Edge.

## 4. Publish

```bat
dotnet publish TvtTeamTTS.Web.csproj -c Release -r win-x64 --self-contained false -o publish
xcopy /E /I /Y Runtime publish\Runtime
xcopy /E /I /Y Voices publish\Voices
```

Chạy `publish\TvtTeamTTS.Web.exe --urls http://localhost:4110`.

## 5. Cách sử dụng

1. Dán tối đa **50.000 ký tự** kể cả dấu và khoảng trắng. Nếu vượt, cảnh báo xuất hiện và nút render tự ẩn.
2. Chọn nhóm giọng Nữ, Nam hoặc Khác; mặc định là Nữ với Ngọc Huyền được ưu tiên.
3. Chọn **Tốc độ phát video** từ bảy mức cố định; thực chất đây là tốc độ đọc trong audio MP3.
4. Bấm **Tạo & tải MP3**. Chương trình tự chia thành các phần nhỏ, tổng hợp audio rồi ghép MP3.
5. Khi xong, trình duyệt tự tải MP3 **một lần**. Nếu Chrome chặn tải tự động, bấm nút tải MP3 thủ công.
6. Bắt đầu lần render kế tiếp bình thường; tên file tự sinh duy nhất không ghi đè lần trước.
7. File output trên server được xóa khi đóng tab. Nếu trình duyệt bị tắt cưỡng bức và không gửi tín hiệu đóng, hệ thống có tác vụ dọn phiên cũ sau khoảng 2 giờ (kiểm tra 5 phút/lần).

## 6. Xử lý sự cố

- **Giọng Nữ/Khác gặp -1073740791 / 0xC0000409:** đọc [KHAC_PHUC_LOI_0xC0000409.md](KHAC_PHUC_LOI_0xC0000409.md). Bản v2.1 tự tạo alias không dấu cho model nằm trong thư mục Unicode, cùng file WAV tạm đường dẫn ASCII; nếu vẫn lỗi, kiểm tra model/JSON và engine/DLL.

- **Không có voice:** kiểm tra cặp `.onnx` và `.onnx.json`, nhóm đúng và bấm quét lại.
- **Thiếu engine:** phải có `Runtime\piper\piper.exe` cùng các DLL của bản engine tương ứng.
- **Thiếu FFmpeg:** bổ sung `Runtime\ffmpeg\ffmpeg.exe`; kiểm tra bằng `Runtime\ffmpeg\ffmpeg.exe -version`.
- **Trình duyệt yêu cầu tải lại:** một lần render sinh đúng một yêu cầu tải tự động. Hãy kiểm tra tùy chọn Chrome tự tải xuống nhiều file và chỉ bấm một lần. File được giữ lại đến khi đóng tab, nên có thể bấm tải thủ công.
- **Dừng render mà vẫn tạo file:** bản này gửi hủy tiến trình engine/FFmpeg và dọn WAV tạm.
- **Không thể render hơn 50.000 ký tự:** cần chia nội dung thành nhiều lượt, đây là giới hạn thiết kế.
- **Port đã dùng:** đổi sang `http://localhost:5110` hoặc chạy `netstat -ano | findstr :4110`.

## 7. Bảo mật khi chia sẻ trong mạng LAN

Ứng dụng mặc định không có tài khoản đăng nhập, do đó nên dùng localhost hoặc LAN tin cậy. Nếu cần công khai ra Internet, bổ sung xác thực người dùng, giới hạn request và bảo vệ endpoint download/cleanup trước khi triển khai.

---

**Lưu ý kỹ thuật:** Runtime engine hiện tại vẫn dùng đường dẫn `Runtime\piper\piper.exe` để tương thích bộ chạy offline đang sử dụng. Tên hiển thị, project và giao diện là **TVT Team**.

## 7. Dán văn bản dài (đã sửa từ v2.2)

Mặc định SignalR giới hạn message 32 KB. Các bản cũ `@bind:event="oninput"` có thể không nhận nội dung dán dài, đặc biệt văn bản tiếng Việt. Bản v2.2 giữ nội dung tại trình duyệt và chỉ gửi lên máy chủ khi render; message max 512 KiB dành cho tối đa 50.000 ký tự. Dán > 50.000 ký tự vẫn hiển thị số đếm/cảnh báo nhưng không gửi hay render.

Để cập nhật: thay source v2.1 bằng source mới, GIỮ LẠI thư mục `Runtime`/`Voices` chứa model; chạy lại `dotnet build`; làm mới trình duyệt bằng `Ctrl+F5`.

## 8. Bản v2.3 (sửa cảnh báo ký tự ban đầu và đổi giao diện)

Nếu ô văn bản trống nhưng hiện “Đã vượt giới hạn 50.000 ký tự – Vượt 0 ký tự”, đó là lỗi hiển thị `hidden` bị CSS `display:flex` ghi đè hoặc Chrome dùng cache cũ. Đã sửa bằng inline style, JS thay đổi trực tiếp cả `hidden` và `display`, và cache bust CSS/JS v2.3.1. Cảnh báo chỉ xuất hiện khi ký tự > 50.000. Nhấn Ctrl+F5 khi cập nhật giao diện.
