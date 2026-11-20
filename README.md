# RiseOn Firebase

Bộ package Firebase của RiseOn cho Unity. Mỗi thư mục là một package UPM riêng,
cài và đánh version độc lập.

| Thư mục | Package | Nội dung |
|---|---|---|
| [Core](Core/README.md) | `com.riseon.firebase` | `FirebaseInit`: tự khởi động Firebase khi mở game, App Check, log level, kiểm tra Google Play services, giao lỗi cho một chỗ trong game. Trang cài đặt kiểm tra file cấu hình và chọn cách Editor truy cập Firebase console |
| [RemoteConfig](RemoteConfig/README.md) | `com.riseon.firebase.remoteconfig` | Khai báo tham số Remote Config trong Project Settings, sinh enum và giá trị có kiểu, fetch khi mở game, tham số live, kéo và đẩy với Firebase console từ Editor |

## Phụ thuộc

| Package | Tự cài theo `package.json` | Tùy chọn |
|---|---|---|
| `com.riseon.firebase` | `com.google.firebase.app`, `com.cysharp.unitask` | `com.google.firebase.app-check` |
| `com.riseon.firebase.remoteconfig` | `com.riseon.firebase`, `com.google.firebase.remote-config`, `com.cysharp.unitask` | |

Scoped registry cần có:

| Registry | URL | Scope |
|---|---|---|
| RiseOn | `https://upm.riseongamestudio.com` | `com.riseon`, `com.google.firebase` |
| OpenUPM | `https://package.openupm.com` | `com.cysharp`, `com.google` |

## Phát hành

Tag có tiền tố tên package: `com.riseon.firebase/1.0.0`,
`com.riseon.firebase.remoteconfig/1.0.0`.

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).