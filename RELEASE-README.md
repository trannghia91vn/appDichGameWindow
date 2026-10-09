# GameTranslator 1.1.2

GameTranslator chạy hoàn toàn local trên Windows 10/11 x64. Gói portable đã bao gồm .NET và OCR; không cần cài .NET riêng.

## Yêu cầu

- Ollama đã cài và đang chạy tại `http://localhost:11434`.
- Có ít nhất một model dịch local. Khuyến nghị:

```powershell
ollama pull translategemma:4b
```

GameTranslator không cài Ollama, không tự tải model và không gửi ảnh chụp, văn bản OCR hoặc bản dịch lên dịch vụ cloud.

## Cách dùng

1. Khởi động Ollama.
2. Chạy `GameTranslator.exe`.
3. Chọn một model Ollama.
4. Nhấn `Chọn vùng` và kéo quanh vùng hội thoại tiếng Anh trong game.
5. Nhấn `Hiện Overlay`, sau đó đặt overlay ở vị trí mong muốn.
6. Dịch thủ công bằng `DỊCH` hoặc phím tắt; mặc định là `F8`.
7. Hoặc bật switch `Realtime` trên overlay để tự dịch khi chữ trong vùng chọn thay đổi. Tắt switch để trở lại dịch thủ công; ẩn overlay cũng dừng Realtime.

Realtime được giới hạn ở hai luồng OCR và tự giãn nhịp kiểm tra từ 1,5 đến 3 giây khi chữ không đổi để giảm ảnh hưởng đến game và OBS.

Cấu hình được lưu tại `%LOCALAPPDATA%\GameTranslator\settings.json`.

Chữ dịch trên overlay có màu vàng và được nối thành đoạn liền mạch, tự xuống dòng theo chiều rộng cửa sổ.

## Xử lý sự cố

- **Ollama không kết nối:** mở Ollama, kiểm tra URL rồi nhấn `Làm mới`.
- **Không có model:** chạy `ollama pull translategemma:4b`, sau đó nhấn `Làm mới`. App không tự tải model.
- **OCR không đọc được:** chọn vùng sát phần chữ, tránh hiệu ứng chuyển cảnh và thử lại với chữ tiếng Anh rõ hơn.
- **Phím tắt bị ứng dụng khác sử dụng:** nhấn `Đổi phím` và chọn tổ hợp khác.
- **Overlay không hiển thị:** nhấn `Hiện Overlay`; nếu overlay nằm ngoài màn hình, khởi động lại app để vị trí được kiểm tra lại.
