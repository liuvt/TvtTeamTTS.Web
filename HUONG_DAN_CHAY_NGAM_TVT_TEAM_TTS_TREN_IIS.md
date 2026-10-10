# HƯỚNG DẪN CHẠY NGẦM TVT TEAM TTS v2.3 TRÊN IIS (MẠNG LAN)

> Áp dụng cho source `TvtTeamTTS.Web_v2.3_FixCounter_NewUI.zip`, ứng dụng Blazor Server / ASP.NET Core .NET 9, chạy trên Windows x64. Mục tiêu: một máy chạy IIS, các máy khác dùng trình duyệt qua LAN. Không cần giữ PowerShell/CMD mở.

## 1. Sơ đồ triển khai

- Tên IIS website: `TVTTeamTTS`
- Application Pool: `TVTTeamTTS-Pool`
- Physical Path: `C:\TVTTeamTTS\Publish`
- Binding: HTTP, `All Unassigned`, Port `4110`, Host name để trống
- URL máy chủ: `http://localhost:4110`
- URL máy khác: `http://<IP-MAY-CHU>:4110` (ví dụ `http://192.168.1.100:4110`)

**Không chạy đồng thời** `TvtTeamTTS.Web.exe --urls http://0.0.0.0:4110` khi IIS chiếm port 4110. Dừng cửa sổ console cũ (Ctrl+C) trước khi khởi chạy site IIS.

## 2. Bật tính năng Windows cần thiết

Trên Windows 10/11 Pro/Enterprise, chạy `optionalfeatures.exe`:

1. Mở **Internet Information Services**.
2. Đánh dấu **Web Management Tools > IIS Management Console**.
3. Trong **World Wide Web Services > Application Development Features**, bật **WebSocket Protocol**.
4. Bật **Application Initialization** (nếu có) để hỗ trợ khởi động nóng.
5. Chọn OK và đợi Windows hoàn tất.

Trên Windows Server, vào **Server Manager > Add Roles and Features > Web Server (IIS)** và chọn **WebSocket Protocol**, **Application Initialization**, **IIS Management Console**.

Kiểm tra `inetmgr` mở được IIS Manager.

## 3. Cài ASP.NET Core Hosting Bundle cho .NET 9

Dù publish `--self-contained true`, **IIS vẫn cần ASP.NET Core Module (ANCM)** do Hosting Bundle cài đặt.

- Trang Microsoft: https://dotnet.microsoft.com/en-us/download/dotnet/9.0
- Tại **ASP.NET Core Runtime / Windows**, tải mục **Hosting Bundle**.
- Cài trên máy chủ sau khi đã bật IIS. Nếu cài Hosting Bundle từ trước khi bật IIS, chạy lại bộ cài ở chế độ Repair.
- Sau khi cài có thể khởi động lại Windows; nếu không, chạy CMD bằng quyền Administrator:

```cmd
net stop was /y
net start w3svc
```

**Lưu ý về vòng đời:** .NET 9 hết hỗ trợ ngày **10/11/2026**. Ứng dụng hiện tại vẫn dùng .NET 9; nên lên kế hoạch chuyển source sang .NET 10 LTS.

## 4. Publish ứng dụng

Mở PowerShell tại thư mục chứa `TvtTeamTTS.Web.csproj`:

```powershell
dotnet restore
dotnet publish .\TvtTeamTTS.Web.csproj -c Release -r win-x64 --self-contained true -o "C:\TVTTeamTTS\Publish"
```

Lưu ý:

- Publish tạo tự động file `web.config` trong thư mục đích. **Không xóa file này.**
- Nếu đã có site IIS đang chạy, dừng site/app pool trước khi publish để tránh file bị khóa.
- Kiểm tra tồn tại `TvtTeamTTS.Web.exe`, `TvtTeamTTS.Web.dll`, `web.config`, `wwwroot`, `appsettings.json`.
- `Runtime` và `Voices` là thành phần model/engine do bạn cung cấp; không có sẵn trong ZIP source. Chép đầy đủ từ ứng dụng cũ.

Cấu trúc gợi ý:

```text
C:\TVTTeamTTS\Publish\
├── TvtTeamTTS.Web.exe
├── TvtTeamTTS.Web.dll
├── web.config
├── appsettings.json
├── wwwroot\
├── Runtime\
│   ├── piper\
│   │   ├── piper.exe
│   │   └── (toàn bộ DLL của engine)
│   └── ffmpeg\
│       └── ffmpeg.exe
├── Voices\
│   ├── Nữ\
│   ├── Nam\
│   └── Khác\
├── Temp\
└── Output\
```

Đường dẫn máy chủ nên dùng ký tự ASCII như `C:\TVTTeamTTS\Publish` để tránh lỗi của engine TTS native.

## 5. Tạo Application Pool và Website trong IIS

Mở `inetmgr` (**Run as administrator**).

### 5.1. Application Pool

1. Chọn **Application Pools > Add Application Pool...**.
2. Name: `TVTTeamTTS-Pool`.
3. **.NET CLR version: No Managed Code**.
4. Managed pipeline mode: **Integrated**.
5. Chọn OK.
6. Chuột phải `TVTTeamTTS-Pool` > **Advanced Settings...**:
   - **Start Mode** = `AlwaysRunning`.
   - **Idle Time-out (minutes)** = `0`.
   - **Enable 32-Bit Applications** = `False` (ứng dụng x64).
   - **Maximum Worker Processes** = `1` (giữ một tiến trình duy nhất; bộ nhớ session được lưu trong ứng dụng).
   - Identity mặc định: `ApplicationPoolIdentity`.

### 5.2. Website

1. Chuột phải **Sites > Add Website...**.
2. Site name: `TVTTeamTTS`.
3. Application pool: `TVTTeamTTS-Pool`.
4. Physical path: `C:\TVTTeamTTS\Publish`.
5. Type: `http`; IP: `All Unassigned`; Port: `4110`; Host name: để trống.
6. Chọn OK.
7. Chọn site `TVTTeamTTS` > **Advanced Settings...** > **Preload Enabled** = `True` (cần Application Initialization).
8. Chọn **Start** nếu site chưa chạy.

> Khi bật `AlwaysRunning`, tắt Idle Timeout và Preload, IIS có thể khởi chạy ứng dụng sau khi Windows/IIS khởi động, không cần cửa sổ CMD. Một số môi trường cần request đầu tiên để kích hoạt nếu Application Initialization chưa sẵn sàng.

## 6. Cấp quyền cho engine và thư mục ghi file

Mở PowerShell **Run as Administrator**:

```powershell
$poolUser = 'IIS AppPool\TVTTeamTTS-Pool'
$root = 'C:\TVTTeamTTS\Publish'
New-Item -ItemType Directory -Force -Path "$root\Temp", "$root\Output" | Out-Null
icacls $root /grant "${poolUser}:(OI)(CI)RX" /T
icacls "$root\Temp" /grant "${poolUser}:(OI)(CI)M" /T
icacls "$root\Output" /grant "${poolUser}:(OI)(CI)M" /T
```

- Root, `wwwroot`, `Voices`, `Runtime`: đọc và thực thi.
- `Temp`, `Output`: quyền Modify để tạo/xóa file WAV/MP3 và alias ONNX.
- Không cấp quyền ghi Full Control cho toàn bộ `C:\` hoặc `Everyone`.
- Nếu đổi `TvtTeamTTS:NativeWorkDirectory` trong `appsettings.json` sang một thư mục khác (ví dụ `C:\TVT-TTS-Cache`), phải tạo thư mục đó và cấp quyền Modify cho cùng Application Pool.

Ví dụ `appsettings.json` (tùy chọn):

```json
{
  "TvtTeamTTS": {
    "NativeWorkDirectory": "C:\\TVT-TTS-Cache"
  }
}
```

## 7. Mở Firewall cho máy khác trong LAN

Mở PowerShell **Run as Administrator**:

```powershell
New-NetFirewallRule -DisplayName 'TVT Team TTS IIS LAN' -Direction Inbound -Protocol TCP -LocalPort 4110 -Action Allow -Profile Private -RemoteAddress LocalSubnet
```

Xem IP thực tế của máy chủ:

```cmd
ipconfig
```

Mở `http://localhost:4110` trên server trước. Sau đó truy cập `http://<IP-MAY-CHU>:4110` từ máy trong cùng LAN. Cần đúng profile Private và đường đi mạng không bị VLAN/router chặn.

## 8. Kiểm tra Blazor Server / SignalR

Ứng dụng dùng **Blazor Interactive Server** nên cần kết nối mạng ổn định và **WebSocket Protocol** trong IIS. Kiểm tra:

- Trang mở bình thường, bộ đếm ký tự tăng khi dán text.
- Danh sách Voices hiện đủ Nữ/Nam/Khác.
- Tạo MP3 thử với 1–2 câu trước, sau đó thử nội dung dài.
- Chrome DevTools > Network có kết nối `/_blazor` (thường WebSocket; nếu không hỗ trợ có thể fallback).

Khi nhiều máy cùng render, CPU/RAM máy chủ sẽ chịu toàn bộ tải. Phiên bản v2.3 **chưa triển khai hàng đợi render tập trung**, nên tránh cho quá nhiều người render văn bản dài cùng lúc.

## 9. Xử lý sự cố

| Dấu hiệu | Nguyên nhân thường gặp / cách kiểm tra |
|---|---|
| 500.19 | Kiểm tra `web.config`, Hosting Bundle/ASP.NET Core Module, cấu hình IIS |
| 500.30 | Ứng dụng không khởi động: thiếu `Runtime`, quyền thư mục hoặc lỗi lúc startup; kiểm tra Event Viewer |
| 502.5 | Tiến trình ASP.NET Core không chạy được (thường out-of-process); kiểm tra log IIS và Event Viewer |
| 403 / Access Denied | Thiếu quyền `IIS AppPool\TVTTeamTTS-Pool` đối với thư mục |
| Giao diện lên nhưng render lỗi | Kiểm tra `Runtime\piper\piper.exe`, DLL, `Runtime\ffmpeg\ffmpeg.exe`, ONNX/JSON và quyền chạy/ghi |
| Máy chủ vào được, máy khác không vào | Xem IP, Binding port 4110, Firewall, VLAN, Wi-Fi client isolation |
| Dán văn bản khiến mất kết nối | Kiểm tra WebSocket, SignalR, cache JavaScript phiên bản mới |
| Vừa khởi động đã yêu cầu port 4110 đang dùng | Có console EXE khác đang chiếm port; đóng console hoặc đổi Binding |

Xem **Event Viewer > Windows Logs > Application** (ASP.NET Core Module / .NET Runtime); xem **IIS log** tại `%SystemDrive%\inetpub\logs\LogFiles`.

Nếu cần bật tạm log khởi động: trong `web.config` (file publish) đổi thuộc tính của `<aspNetCore>` thành `stdoutLogEnabled="true"` và `stdoutLogFile=".\logs\stdout"`, tạo thư mục `logs` và cấp Modify cho pool. **Tắt lại `stdoutLogEnabled="false"` sau khi debug** để tránh đầy ổ đĩa.

## 10. Cập nhật phiên bản về sau

1. Sao lưu `Voices`, `Runtime`, `appsettings.json` và những cấu hình thực tế.
2. Trong IIS chọn **Stop** website / Application Pool.
3. Publish bản mới vào thư mục `C:\TVTTeamTTS\Publish` (hoặc publish ra Staging và chép vào sau khi Stop).
4. Đảm bảo `Voices` và `Runtime` vẫn còn, ACL trên `Temp`/`Output` phù hợp.
5. Chọn **Start** website, vào thử `http://localhost:4110` và render MP3 ngắn.

## 11. Lưu ý vận hành và bảo mật

- Nên chỉ dùng cho **LAN tin cậy**. v2.3 chưa có tài khoản/xác thực và HTTP chưa mã hóa. Không chuyển tiếp port 4110 trực tiếp ra Internet.
- Khi cần nhiều người dùng, nên bổ sung hàng đợi render, giới hạn số job đồng thời, theo dõi CPU/RAM/ổ cứng, giới hạn người dùng và HTTPS.
- IIS Application Pool recycle/restart có thể ngắt phiên Blazor và các job đang chạy. Cân nhắc lên lịch recycle vào giờ ít sử dụng; đừng vô hiệu hóa mọi cơ chế bảo trì khi chưa có giám sát.
- Tránh để Antivirus chặn các chương trình engine; kiểm tra chữ ký/nguồn gốc thư viện trước khi cho phép loại trừ.

## Tài liệu Microsoft tham khảo

- ASP.NET Core lên IIS: https://learn.microsoft.com/en-us/aspnet/core/tutorials/publish-to-iis
- Hướng dẫn IIS hosting (ASP.NET Core 9): https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/iis/?view=aspnetcore-9.0
- WebSockets với IIS: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets
- Vòng đời .NET: https://dotnet.microsoft.com/en-us/platform/support/policy
