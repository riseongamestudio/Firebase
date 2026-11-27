# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.firebase` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [1.0.4] - 2026-11-27

### Đổi

- Trang settings, mục **Config files**: mỗi dòng mang tên file
  (`google-services.json`, `GoogleService-Info.plist`) và icon nền tảng của Unity trước
  ô, thay cho chữ Android, iOS.
- Phụ thuộc `com.riseon.utils` 2.1.0.

## [1.0.3] - 2026-11-26

### Đổi

- Trang settings: mỗi file cấu hình là một ô như ô object của Unity, kèm project Firebase
  file trỏ tới; bấm thì Project window chỉ tới file, bấm đúp thì mở file. Hộp báo thiếu
  file có nút **Open Firebase** mở console (trang Project settings của project khi file
  nền tảng kia đã cho biết project); hộp báo file cho app id khác có nút **Player
  Settings**.
- Hộp báo App Check thiếu package lên đầu trang cùng các lỗi khác, thay vì nằm trong mục
  App Check đang gập.
- Mục App Check gập mở và sửa được cả khi chưa bật, để điền thiết lập trước khi bật.
- Dòng Google account: trong cửa sổ hẹp, tên tài khoản cắt bằng "…" trên một dòng (rê
  chuột để xem đủ) thay vì tràn khỏi dòng.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.utils` 2.0.0.

## [1.0.2] - 2026-11-20

### Đổi

- Cần Unity 6000.3, bản đang dùng để phát triển, thay cho 6000.0 chưa từng được thử:
  `package.json` ghi `unity`, nên Package Manager không cài pack vào project Unity cũ
  hơn.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.utils` 1.0.18.

## [1.0.1] - 2026-11-20

### Sửa

- Project chưa có Odin Inspector: mọi assembly của pack dùng Odin được bỏ qua, và chỉ còn một lỗi
  nói pack cần Odin; trước đó project báo thêm hàng loạt lỗi compile khó hiểu.

## [1.0.0] - 2026-11-20

Bản đầu tiên.