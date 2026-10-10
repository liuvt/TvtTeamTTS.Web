# HƯỚNG DẪN PUBLISH LOCAL / MẠNG LAN

**Ứng dụng:** TVT Team TTS Web  
**Source tham chiếu:** `TvtTeamTTS.Web_v2.3_FixCounter_NewUI.zip`  
**Nền tảng:** ASP.NET Core / Blazor Server, .NET 9, Windows 64-bit  
**Mục tiêu:** Một máy Windows làm máy chủ TTS; các máy khác cùng mạng truy cập bằng Chrome/Edge để tạo và tải MP3.

> **Quan trọng:** Các máy khách không cần cài .NET, engine hay model giọng. **Máy chủ** phải có engine TTS, FFmpeg và các model ONNX. Toàn bộ quá trình tạo MP3 sử dụng CPU/RAM của máy chủ. Ứng dụng hiện **không có đăng nhập**, chỉ triển khai trên LAN tin cậy, không mở cổng trực tiếp ra Internet.

---

## 1. Sơ đồ hoạt động

```text
                 LAN / Wi-Fi nội bộ
                       |
           +-----------+------------+
           |           |            |
        Máy A        Máy B         Máy C
      Chrome/Edge  Chrome/Edge  Chrome/Edge
           |           |            |
           +-----------+------------+
                       |
          http://192.168.1.100:4110
                       |
         +--------------------------+
         |  MÁY CHỦ WINDOWS         |
         |  TVT Team TTS Web        |
         |  Port TCP 4110           |
         |  Runtime/piper           |
         |  Runtime/ffmpeg          |
         |  Voices/*.onnx + JSON    |
         |  Output/ (MP3 tạm)       |
         +--------------------------+
```

`192.168.1.100` là **IP ví dụ**. Hãy thay bằng IPv4 thực tế của máy chủ tại mục 5.

## 2. Chuẩn bị máy chủ

1. Dùng Windows 10/11 hoặc Windows Server **x64** có quyền chạy ứng dụng và mở Windows Firewall.
2. Cài **.NET 9 SDK trên máy build**. Kiểm tra bằng PowerShell hoặc CMD:

   ```powershell
   dotnet --list-sdks
   ```

3. Giải nén source để có thư mục chứa `TvtTeamTTS.Web.csproj`, ví dụ `D:\Dev\TvtTeamTTS.Web`.
4. Chuẩn bị engine và các model. **ZIP source v2.3 không chứa engine, FFmpeg hoặc model dung lượng lớn.** Chép từ bản đang chạy hoặc từ bộ cài tương thích:

   ```text
   D:\Dev\TvtTeamTTS.Web\
   ├── TvtTeamTTS.Web.csproj
   ├── Program.cs
   ├── Components\
   ├── Services\
   ├── wwwroot\
   ├── Runtime\
   │   ├── piper\
   │   │   ├── piper.exe
   │   │   └── (các DLL/thư viện đi kèm)
   │   └── ffmpeg\
   │       └── ffmpeg.exe
   ├── Voices\
   │   ├── Nữ\NgocHuyen\ngochuyen.onnx
   │   ├── Nữ\NgocHuyen\ngochuyen.onnx.json
   │   ├── Nam\...
   │   └── Khác\...
   ├── Temp\
   └── Output\
   ```

   **Lưu ý:** Tên thư mục hiển thị của nhóm có thể là `Nữ/Nam/Khác` hoặc `Nu/Nam/Khac`. File cấu hình nên cùng tên với model: `voice.onnx` + `voice.onnx.json`; source cũng hỗ trợ `config.json` đặt cùng thư mục model. Nếu engine có DLL kèm theo, **chép toàn bộ thư mục**, không chỉ `piper.exe`.

## 3. Publish bản Windows x64 độc lập (khuyến nghị)

Trên **máy build có .NET 9 SDK**, mở **PowerShell** và chạy các lệnh dưới đây. Điều chỉnh `$ProjectDir` nếu giải nén source tại thư mục khác.

```powershell
$ProjectDir = 'D:\Dev\TvtTeamTTS.Web'
$PublishDir = 'C:\TVTTeamTTS\Publish'

Set-Location $ProjectDir

dotnet restore .\TvtTeamTTS.Web.csproj
if ($LASTEXITCODE -ne 0) { throw 'Restore thất bại' }

dotnet publish .\TvtTeamTTS.Web.csproj -c Release -r win-x64 --self-contained true -o $PublishDir
if ($LASTEXITCODE -ne 0) { throw 'Publish thất bại' }

# Chép engine, FFmpeg và các model từ source sang thư mục chạy.
New-Item -ItemType Directory -Force "$PublishDir\Runtime", "$PublishDir\Voices", "$PublishDir\Temp", "$PublishDir\Output" | Out-Null

if (Test-Path '.\Runtime') {
    Copy-Item '.\Runtime\*' "$PublishDir\Runtime" -Recurse -Force
} else {
    Write-Warning 'Chưa có Runtime trong source - phải chép engine/FFmpeg từ máy đang sử dụng.'
}

if (Test-Path '.\Voices') {
    Copy-Item '.\Voices\*' "$PublishDir\Voices" -Recurse -Force
} else {
    Write-Warning 'Chưa có Voices - phải chép các model ONNX/JSON.'
}

Write-Host "Publish xong: $PublishDir"
```

**`--self-contained true`** đóng gói .NET runtime cùng ứng dụng, vì vậy **máy chạy bản publish** không cần cài riêng .NET runtime. Chế độ này **không** đóng gói các file ngoài dự án như `Runtime\piper`, `Runtime\ffmpeg` và model ONNX nếu bạn chưa chép vào.

Nếu muốn bản nhẹ hơn và **máy chủ đã cài ASP.NET Core Runtime 9 x64**, bạn có thể thay `--self-contained true` bằng `--self-contained false`.

Sau khi publish, kiểm tra tối thiểu:

```powershell
$PublishDir = 'C:\TVTTeamTTS\Publish'
@(
  "$PublishDir\TvtTeamTTS.Web.exe",
  "$PublishDir\Runtime\piper\piper.exe",
  "$PublishDir\Runtime\ffmpeg\ffmpeg.exe"
) | ForEach-Object { '{0} : {1}' -f (Test-Path $_), $_ }

Get-ChildItem "$PublishDir\Voices" -Recurse -Filter '*.onnx' |
  Select-Object FullName
```

Ba dòng kiểm tra file EXE phải trả về `True`, và danh sách ONNX phải có ít nhất một model. Chưa cần có file trong `Output` vì đây là thư mục sinh file MP3 tạm.

## 4. Chạy thử TVT Team TTS trên máy chủ

Trong PowerShell:

```powershell
Set-Location 'C:\TVTTeamTTS\Publish'
.\TvtTeamTTS.Web.exe --urls 'http://0.0.0.0:4110'
```

Giữ cửa sổ PowerShell này mở. Trên **chính máy chủ**, mở:

**http://localhost:4110**

Kiểm tra: trang giao diện hiển thị, chọn được giọng Nữ/Nam/Khác và render được một đoạn ngắn thành MP3. Khi render thất bại, xem lỗi trong cửa sổ PowerShell.

**Phân biệt địa chỉ:**

| Địa chỉ | Dùng cho |
| --- | --- |
| `http://localhost:4110` | Truy cập ngay trên máy chủ |
| `http://0.0.0.0:4110` | Địa chỉ **lắng nghe** của server, không phải địa chỉ dùng để mở từ máy khác |
| `http://192.168.1.100:4110` | Địa chỉ truy cập từ **máy khác**, thay IP bằng IP thật |

## 5. Xác định IPv4 của máy chủ

Trên máy chủ, mở CMD và nhập:

```cmd
ipconfig
```

Tìm mục **IPv4 Address** trong card mạng đang kết nối (Ethernet/Wi-Fi), ví dụ `192.168.1.100`. Không lấy địa chỉ `127.0.0.1`, địa chỉ adapter ảo hoặc địa chỉ IPv4 của mạng khác.

Nên thiết lập **DHCP Reservation** trên router để máy chủ giữ cùng một IP qua các lần khởi động.

## 6. Mở Windows Firewall cho máy khác truy cập

Trên máy chủ, mở **PowerShell → Run as Administrator**, chạy:

```powershell
New-NetFirewallRule `
  -DisplayName 'TVT Team TTS - LAN TCP 4110' `
  -Direction Inbound `
  -Action Allow `
  -Protocol TCP `
  -LocalPort 4110 `
  -Profile Private `
  -RemoteAddress LocalSubnet
```

Lệnh này chỉ cho phép kết nối TCP 4110 từ mạng con nội bộ khi mạng Windows được đặt cấu hình **Private**. Đối với Windows Server hoặc mạng do công ty quản lý (Domain/VLAN khác subnet), cần nhờ quản trị viên điều chỉnh quy tắc Firewall phù hợp; **không nên mở công khai cho toàn Internet**.

Có thể xem trạng thái card mạng bằng:

```powershell
Get-NetConnectionProfile | Select-Object InterfaceAlias, NetworkCategory
```

Nếu mạng đáng tin cậy nhưng đang là `Public`, cần đánh giá chính sách bảo mật trước khi đổi profile. Không tự đổi profile đối với mạng công cộng.

## 7. Truy cập từ máy tính khác trong cùng LAN

Tại máy A/B/C, mở Chrome hoặc Edge, nhập địa chỉ:

```text
http://192.168.1.100:4110
```

**Thay `192.168.1.100` bằng IP thật của máy chủ.** Không nhập `localhost` ở máy khách, vì `localhost` luôn trỏ về chính máy đó.

Nếu trình duyệt không kết nối được, mở PowerShell trên máy khách và kiểm tra:

```powershell
Test-NetConnection -ComputerName 192.168.1.100 -Port 4110
```

- `TcpTestSucceeded : True` → Máy khách kết nối được đến cổng máy chủ; nếu web lỗi, xem log ứng dụng/trình duyệt.
- `TcpTestSucceeded : False` → Kiểm tra IP, server có đang chạy hay không, Firewall, cùng mạng/VLAN, Wi-Fi **AP isolation** và phần mềm bảo mật.

> Để kiểm tra nhanh, hai máy nên nối cùng router LAN/Wi-Fi không bật chế độ cô lập thiết bị. Máy chủ không được ngủ (Sleep) khi đang phục vụ người khác.

## 8. Tạo file BAT chạy nhanh (không cần gõ lệnh mỗi lần)

Tạo file `START_TVT_TEAM_TTS_LAN.bat` **trên máy chủ** và dán:

```bat
@echo off
chcp 65001 >nul
title TVT Team TTS - LAN Server
cd /d "C:\TVTTeamTTS\Publish"

if not exist "TvtTeamTTS.Web.exe" (
    echo KHONG TIM THAY TvtTeamTTS.Web.exe
    pause
    exit /b 1
)

echo ============================================
echo TVT TEAM TTS - SERVER NOI BO - PORT 4110
echo ============================================
echo Truy cap tai may chu: http://localhost:4110
echo Truy cap tu may khac: http://IP-MAY-CHU:4110
echo.

"TvtTeamTTS.Web.exe" --urls "http://0.0.0.0:4110"

pause
```

Nhấp đúp file BAT để chạy. **Đóng cửa sổ BAT/PowerShell sẽ dừng server.** Đây là cách chạy thủ công để kiểm tra hoặc sử dụng trong giờ làm việc, **chưa phải Windows Service**.

Để vận hành 24/7 không cần người đăng nhập Windows, cần cấu hình dịch vụ Windows/Task Scheduler đúng chuẩn và kiểm tra quyền đọc model, quyền ghi `Temp/Output`; bản source v2.3 chưa tự đăng ký Windows Service.

## 9. Thử với nhiều máy và kiểm tra tải MP3

1. Trên máy chủ, render một đoạn ngắn để xác nhận engine và model hoạt động.
2. Trên máy khách thứ nhất, truy cập URL LAN, chọn giọng, bấm tạo MP3 và kiểm tra file tải về.
3. Trên máy khách thứ hai, làm tương tự; kiểm tra các tab không tải nhầm file của nhau.
4. Thử văn bản dài dưới **50.000 ký tự**; bộ đếm phải đúng, chỉ hiển thị cảnh báo khi trên 50.000 ký tự.
5. Kiểm tra CPU/RAM máy chủ nếu nhiều máy render đồng thời. **Bản v2.3 chưa có hàng đợi render tập trung**, vì vậy không nên cho nhiều tác vụ lớn chạy cùng lúc trên máy CPU yếu.

File MP3 tạm nằm ở `C:\TVTTeamTTS\Publish\Output`, được quản lý theo phiên/tab. Đóng tab sẽ gửi yêu cầu dọn file; nếu tín hiệu đóng tab bị mất, hệ thống có tác vụ dọn phiên cũ sau khoảng 2 giờ. Việc dọn file có thể chậm nếu file đang được tải.

## 10. Các lỗi thường gặp

| Hiện tượng | Cách kiểm tra / khắc phục |
| --- | --- |
| `dotnet` không được nhận diện | Cài .NET **SDK 9** trên máy build; mở terminal mới, chạy `dotnet --list-sdks`. |
| Không có `TvtTeamTTS.Web.exe` | Kiểm tra `dotnet publish` có thành công, đúng đường dẫn output, đúng `win-x64`. |
| `ERR_CONNECTION_REFUSED` trên máy khác | Kiểm tra server đang mở, đúng IP LAN, cổng 4110, Firewall và các thiết bị cùng mạng. |
| Máy chủ truy cập được `localhost` nhưng máy khác không được | Chạy `--urls http://0.0.0.0:4110`; kiểm tra Firewall, profile mạng và AP isolation/VLAN. |
| Cổng 4110 đã được sử dụng | Chạy `netstat -ano | findstr :4110`; dừng tiến trình đang giữ cổng hoặc đổi sang cổng khác và cập nhật Firewall/URL. |
| Không tìm thấy giọng | Kiểm tra file `.onnx` + `.onnx.json` trong `Voices`, bấm **Quét lại giọng đọc**. |
| Thiếu engine | Phải có `Runtime\piper\piper.exe` **cùng các DLL đi kèm** trong thư mục Publish. |
| Thiếu FFmpeg | Phải có `Runtime\ffmpeg\ffmpeg.exe`; kiểm tra bằng `& 'C:\TVTTeamTTS\Publish\Runtime\ffmpeg\ffmpeg.exe' -version`. |
| Engine lỗi `-1073740791` (`0xC0000409`) | Kiểm tra sự tương thích ONNX/JSON, bộ engine/DLL x64 và log cửa sổ server; thay model lỗi để đối chứng. |
| Ô trống báo vượt 50.000 ký tự | Xác nhận đúng bản v2.3; nhấn **Ctrl+F5** để xóa cache giao diện cũ. |
| Người dùng bị ngắt kết nối khi server chạy lâu | Kiểm tra máy chủ bị Sleep, đóng CMD/BAT, khởi động lại Windows, proxy/WebSocket nội bộ và log ứng dụng. |

## 11. Kiểm tra bảo mật và khuyến nghị vận hành

- Chỉ sử dụng trên **mạng LAN tin cậy**; ứng dụng không có xác thực đăng nhập hoặc phân quyền người dùng.
- Truy cập qua `http://` **không mã hóa nội dung trên đường truyền**. Không dùng với tài liệu nhạy cảm trên mạng không tin cậy; khi có nhu cầu đó, cần thiết lập HTTPS và xác thực.
- Không port-forward cổng 4110 trên router, không dùng IP Public/đưa server ra Internet khi chưa có bảo vệ.
- Dùng ổ lưu trữ còn dung lượng cho WAV/MP3 tạm; bảo đảm quyền ghi `Temp` và `Output`.
- Cập nhật model/engine tập trung tại máy chủ; máy khách chỉ cần tải lại trang.
- Máy chủ có thể cần cài các **Windows native runtime/DLL** tương ứng với phiên bản engine đang sử dụng, dù .NET đã publish self-contained.

---

## 12. Tóm tắt lệnh quan trọng

**Publish (PowerShell tại thư mục source):**

```powershell
dotnet publish .\TvtTeamTTS.Web.csproj -c Release -r win-x64 --self-contained true -o 'C:\TVTTeamTTS\Publish'
```

**Chạy máy chủ LAN (PowerShell tại thư mục Publish):**

```powershell
.\TvtTeamTTS.Web.exe --urls 'http://0.0.0.0:4110'
```

**Máy khách truy cập:**

```text
http://IP-MAY-CHU:4110
```

**Ghi chú về phạm vi kiểm thử:** Hướng dẫn được đối chiếu với `Program.cs`, các service TTS/FFmpeg/voice và file project trong source TVT Team TTS v2.3. File hướng dẫn không thay đổi source. Chưa xác nhận `dotnet publish` hoặc render thực tế trên một máy Windows vì môi trường biên soạn không có .NET 9 SDK/engine giọng.
