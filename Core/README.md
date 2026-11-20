# RiseOn.Firebase

Khởi động Firebase cho cả game: áp cài đặt của project (App Check, log level),
kiểm tra Google Play services, tạo app mặc định. Pack tự chạy trước khi scene đầu
load, game không phải gọi gì. Package nào dùng Firebase thì
`await FirebaseInit.WaitUntilCompleteAsync()`.

Package `com.riseon.firebase`, namespace `RiseOn.Firebase`. Chỉ build cho Android
và iOS: SDK desktop của Firebase là bản beta dành cho lúc phát triển, Google ghi
rõ không dùng để phát hành. Build cho nền tảng khác bị dừng.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Luồng](#luồng)
- [Dùng trong game](#dùng-trong-game)
- [Khi khởi động lỗi](#khi-khởi-động-lỗi)
- [Cài đặt của project](#cài-đặt-của-project)
- [App Check](#app-check)
- [Truy cập console](#truy-cập-console)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của trang cài đặt |
| `com.google.firebase.app` 13.17.0 | Tự cài theo `package.json`, từ registry RiseOn | Firebase SDK |
| `com.riseon.utils` 1.0.18 | Tự cài theo `package.json` | Các mục của trang cài đặt |
| `com.cysharp.unitask` 2.5.11 | Tự cài theo `package.json`, từ OpenUPM | Các hàm `async` |
| `com.google.firebase.app-check` | Registry RiseOn, cài khi dùng App Check (trang cài đặt có nút cài) | [App Check](#app-check) |
| `google-services.json`, `GoogleService-Info.plist` | Firebase console, Project settings > Your apps; đặt ở bất kỳ đâu trong `Assets` | Firebase biết project nào |
| [Firebase CLI](https://firebase.google.com/docs/cli) | `npm install -g firebase-tools` | Chỉ khi [truy cập console](#truy-cập-console) qua tài khoản Google |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ `RiseOn.Firebase.Requirements`.

## Cài đặt

**Registry RiseOn** (khuyên dùng): thêm registry RiseOn với scope `com.riseon`,
`com.google.firebase`, và OpenUPM với scope `com.cysharp`, `com.google`, vào
`Packages/manifest.json`, rồi thêm package. Mục nào project đã có thì bỏ qua;
registry nào khai scope cụ thể hơn được ưu tiên, nên các gói `com.google.firebase`
lấy từ registry RiseOn dù OpenUPM khai `com.google`.

```json
{
  "scopedRegistries": [
    {
      "name": "RiseOn",
      "url": "https://upm.riseongamestudio.com",
      "scopes": ["com.riseon", "com.google.firebase"]
    },
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": ["com.cysharp", "com.google"]
    }
  ],
  "dependencies": {
    "com.riseon.firebase": "1.0.2"
  }
}
```

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần đủ các
registry như ví dụ trên, vì SDK Firebase chỉ có trên registry RiseOn. Rồi
*Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Firebase.git?path=/Core
```

**Thư mục local**: `"com.riseon.firebase": "file:D:/path/to/Firebase/Core"`.

## Luồng

1. Trước khi scene đầu load, pack đọc cài đặt của project, đặt log level và
   App Check. App Check phải đặt trước mọi lệnh Firebase khác, nên chỉ pack
   làm việc này.
2. `FirebaseApp.CheckAndFixDependenciesAsync`: trên Android, kiểm tra Google
   Play services và hỏi người chơi cập nhật hoặc bật nếu module nào cần.
3. Tạo app mặc định từ file cấu hình, rồi hoàn thành task mà các bên đang chờ.

Bước nào lỗi thì luồng dừng lại chờ game cho thử lại, xem
[Khi khởi động lỗi](#khi-khởi-động-lỗi). Thử lại thì chạy lại từ bước 2, hoặc
từ bước 1 nếu chính bước 1 lỗi. Bản build không gặp lỗi thiếu file cấu hình hay
file làm cho app khác: build dừng trước khi ra bản. Play mode trong Editor không có
bước chặn đó; trang cài đặt báo đỏ lỗi này ở đầu trang.

## Dùng trong game

```csharp
using RiseOn.Firebase;

// Bên nào dùng Firebase thì chờ; xong rồi thì trả về ngay.
var app = await FirebaseInit.WaitUntilCompleteAsync();
```

| API | Ý nghĩa |
|---|---|
| `WaitUntilCompleteAsync()` | Hoàn thành trên thread của Unity, trả app mặc định. Không bao giờ lỗi |

## Khi khởi động lỗi

Game đã dùng Firebase thì không chạy tiếp khi thiếu nó: lỗi thì mọi bên đang chờ
`WaitUntilCompleteAsync()` đứng yên tới khi một lần thử lại thành công. Đúng một chỗ
trong game (thường là màn boot) lấy kết quả ra và cho thử lại:

```csharp
var result = await FirebaseInit.PopResultAsync();
while (result is FirebaseInitFailed failed) {
    // Báo người chơi cập nhật hoặc bật Google Play services, rồi kiểm tra lại.
    await updatePopup.ShowAsync(failed.Error.Status);
    result = await failed.RetryAsync();
}
```

| API | Ý nghĩa |
|---|---|
| `PopResultAsync()` | Kết quả của lần chạy hiện tại: `FirebaseInitSucceeded` hoặc `FirebaseInitFailed`. Mỗi lần chạy chỉ lấy ra được một lần; gọi lần thứ hai thì ném `InvalidOperationException`, để một lỗi không tới hai chỗ xử lý |
| `FirebaseInitFailed.Error` | `FirebaseInitException`; `Status` là kết quả kiểm tra Google Play services, `UnavailableOther` khi lỗi xảy ra trước khi có kết quả đó; `InnerException` là lỗi gốc nếu có |
| `FirebaseInitFailed.RetryAsync()` | Kiểm tra lại (lại hỏi người chơi sửa Google Play services), trả kết quả mới cho chính bên gọi |

Mỗi `FirebaseInitFailed` chỉ thử lại được một lần. Game không lấy kết quả ra
thì mọi thứ dùng Firebase chờ mãi, lỗi chỉ hiện trong log.

Google Play services chỉ làm hỏng bước này khi game có module Firebase cần nó: với app,
Remote Config và App Check, một máy Android không có Google Play services (emulator AOSP
Android 15) vẫn ra `Available` và fetch được Remote Config. Bảng module nào cần Google
Play services nằm ở trang [Dependencies of Firebase Android SDKs on Google Play
services](https://firebase.google.com/docs/android/android-play-services). Lỗi lặp lại ở
mọi lần thử mà máy có Google Play services thì là lỗi của game (file cấu hình, module
Firebase): xem log `FirebaseInit` và `FirebaseInitFailed.Error`. Máy không có Google
Play services mà game dùng module cần nó thì game dừng ở màn boot trên máy đó; game phải
chạy được trên những máy như vậy thì chọn module theo bảng ở trang Firebase nói trên.

## Cài đặt của project

Mở bằng menu **RiseOn > Firebase > Settings**, hoặc Edit > Project Settings >
RiseOn > Firebase. Lần mở đầu tạo
`Assets/RiseOn/Resources/RiseOnFirebaseSettings.asset`; pack đọc asset đó lúc
chạy, project chưa có asset thì dùng giá trị mặc định. Inspector của asset chỉ có
nút mở trang này. Asset nằm ngoài thư mục `Resources` thì game không đọc được và
chạy với giá trị mặc định: trang báo lỗi kèm nút **Move it** chuyển asset về
`Assets/RiseOn/Resources/`, và build dừng.

Đầu trang là hai file cấu hình: project Firebase mỗi file trỏ tới, và báo lỗi khi
thiếu file, có hơn một file cùng tên trong `Assets` (Firebase SDK tự chọn một
file theo luật riêng), package name hoặc bundle id trong file khác Player Settings,
hoặc hai file trỏ hai project khác nhau. Lỗi của nền tảng nào thì build cho nền
tảng đó dừng.

| Mục | Ý nghĩa |
|---|---|
| Log Level | Mức log của Firebase SDK, mặc định `Info` như chính SDK |
| App Check, iOS Provider, Debug in Dev Builds, Editor Debug Token | [App Check](#app-check) |
| Console access | [Truy cập console](#truy-cập-console) |

## App Check

App Check chứng thực rằng request tới Firebase đến từ bản game thật. Bật mục
App Check thì pack đặt provider trước mọi lệnh Firebase:

| Nơi chạy | Provider |
|---|---|
| Editor | Debug |
| Development Build, khi bật Debug in Dev Builds | Debug |
| Android | Play Integrity |
| iOS | App Attest hoặc DeviceCheck, theo iOS Provider |

Bật App Check mà chưa cài `com.google.firebase.app-check` thì build dừng; trang
cài đặt có nút cài. Play mode vẫn chạy, chỉ log lỗi và không có App Check.

Debug token đăng ký trong Firebase console, mục App Check > Apps > Manage debug
tokens:

- Android, iOS: lần chạy đầu của mỗi máy, SDK tự tạo token và in ra log (logcat
  tag `DebugAppCheckProvider`, console Xcode dòng `App Check debug token`).
- Editor: SDK desktop không tự tạo token. Tạo token trong console rồi dán vào ô
  Editor Debug Token của trang cài đặt; ô này lưu trong `UserSettings/` của từng
  người, không vào git và không vào bản build.

Chọn App Attest thì bản build iOS có sẵn entitlement
`com.apple.developer.devicecheck.appattest-environment` (`production`), và máy
dưới iOS 14 (chưa có App Attest) dùng DeviceCheck, như cách Firebase hướng dẫn.
Pack ghi entitlement này sau mọi plugin khác; plugin nào đã đặt giá trị khác thì
build dừng và báo tên file.

Chỉ bật Enforce trong Firebase console khi phần lớn người chơi đã lên bản có App
Check: bản cũ không gửi token sẽ bị từ chối. Play Integrity cần Google Play
services, nên khi đã Enforce thì máy Android không có Google Play services cũng bị
từ chối.

## Truy cập console

Công cụ Editor cần vào project trên Firebase console (như Pull và Push của
[Remote Config](../RemoteConfig/README.md#kéo-và-đẩy-với-firebase-console)) dùng cách
chọn ở ô **Access**, mục **Console access** cuối trang Firebase. Project là project
trong file cấu hình. Lựa chọn lưu trong `UserSettings/` của từng người nên
không vào git:

| Cách | Cần gì | Ghi chú |
|---|---|---|
| Firebase CLI | Firebase CLI đã cài và đăng nhập (nút **Sign in** mở terminal chạy `firebase login --reauth`; đã đăng nhập thì nút thành **Switch account**) | Dùng tài khoản Google của người làm |
| Service Account | File key JSON: Firebase console > Project settings > Service accounts > Generate new private key | Gọi thẳng REST API. Để file ngoài project: file này có quyền admin, trang cảnh báo khi file nằm trong project |

File key của service account thuộc project khác project trong file cấu hình thì
trang cảnh báo.

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).