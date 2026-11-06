# NgHuy API Setup

Công cụ thiết lập **NgHuy API** cho Claude Code, Claude Desktop, Codex CLI và app ChatGPT (Codex mode) trên Windows.

## Cách sử dụng

1. [Tải NgHuyAPI.exe](https://github.com/nghuy257/NgHuy-API-SetUp/releases/latest/download/NgHuyAPI.exe).
2. Mở file vừa tải và dán **API key** của `api.nghuy.tech` vào ô **API key**.
3. Bấm **Test key**: ứng dụng kiểm tra key, tải danh sách model, báo app nào chưa cài (Codex CLI, app ChatGPT, Claude Code, Claude Desktop) và cho bạn chọn cài cái nào.
4. Tick **Codex / Claude** tùy key, chọn model tương ứng, bấm **Set up selected** và chờ thông báo hoàn tất.
5. Mở app ChatGPT (Codex mode), gõ `codex` hoặc `claude` trong terminal mới, hoặc mở Claude Desktop để dùng.

Ứng dụng kiểm tra key, cho chọn model có sẵn theo key (GPT, Gemini hoặc Claude), cài thành phần còn thiếu và hiển thị gói / số dư / hạn dùng nếu gateway hỗ trợ. **Chỉ nhận API key trực tiếp, không dùng mã CDK.**

- **Codex CLI** được tải thẳng từ GitHub (bản standalone, không cần Node.js, không cần quyền admin) vào `%LocalAppData%\Programs\Codex`. Máy đã có npm thì cài qua npm.
- **App ChatGPT** cài qua Microsoft Store (winget). Nếu winget lỗi, ứng dụng tự mở trang Store đúng app, bạn chỉ cần bấm Install.
- **Claude Code** (`~/.claude/settings.json`) và **Claude Desktop** (cấu hình gateway) trỏ về `https://api.nghuy.tech`; key được mã hóa bằng Windows DPAPI và đọc qua credential helper, không ghi thẳng vào cấu hình client. Model được chọn theo danh sách gateway trả về cho key.
- Nút **Reset to default** gỡ cấu hình do NgHuy API quản lý khỏi các client; app vẫn được giữ lại. **Restore latest** khôi phục bản sao lưu của NgHuy API.

> Chỉ cần tải và mở file `.exe`, không cần tải mã nguồn hay thư mục logo. Tool cần Windows có .NET Framework 4.5 trở lên và kết nối Internet khi kiểm tra key / cài client. File chưa được ký số nên Windows có thể hiện cảnh báo SmartScreen; chỉ chạy nếu bạn tin cậy nguồn tải.

## Mở bằng link

Sau lần mở giao diện đầu tiên, ứng dụng đăng ký giao thức `nghuyapi://` cho người dùng hiện tại, không cần quyền admin. Link chỉ nhận API key bắt đầu bằng `sk-` và tự chạy thiết lập.

**Không chia sẻ link chứa API key** vì link có thể bị lưu trong lịch sử trình duyệt hoặc ứng dụng. Nên nhập key trực tiếp trong giao diện.

## API đang sử dụng

- API tương thích OpenAI / Codex: `https://api.nghuy.tech/v1`.
- URL gốc cho Claude: `https://api.nghuy.tech`.
- Xem thông tin key: `/api/public/check-key` (chỉ hoạt động nếu server triển khai endpoint này).

## Dòng lệnh

```powershell
.\NgHuyAPI.exe --status                 # Xem trạng thái client và cấu hình
.\NgHuyAPI.exe --self-test              # Chạy kiểm tra nội bộ, không gọi gateway
.\NgHuyAPI.exe --install-missing        # Cài client còn thiếu
.\NgHuyAPI.exe --install-codex-portable # Cài Codex CLI bản standalone
.\NgHuyAPI.exe --reset-default          # Gỡ cấu hình do NgHuy API quản lý
.\NgHuyAPI.exe --restore-latest         # Khôi phục bản sao lưu mới nhất

# Nhập API key qua stdin, không truyền key trong tham số dòng lệnh:
Read-Host 'API key' | .\NgHuyAPI.exe --apply-stdin
```

Mặc định lệnh thiết lập cấu hình cả Claude và Codex. Thêm `--claude-only` hoặc `--codex-only` để giới hạn mục tiêu.

## Build từ mã nguồn

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

File sau khi build: `dist\NgHuyAPI.exe`. Script dùng compiler .NET Framework có sẵn trên Windows và tạo logo / icon từ `assets/nghuy-source.jpg`.

## Dữ liệu và bảo mật

- Provider ID: `nghuyapi`.
- Key mã hóa và backup: `%LocalAppData%\NgHuy API`.
- Model catalog: `%USERPROFILE%\.codex\nghuyapi-model-catalog.json`.
- Không tự chuyển key hoặc backup từ bản thương hiệu trước; hãy nhập lại API key và chạy **Set up selected**.
- Không đưa API key, `credential.dat`, `.env` hay thư mục backup lên GitHub.
