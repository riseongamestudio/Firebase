# RiseOn.Firebase.RemoteConfig

Remote Config khai báo trong Project Settings: mỗi tham số có key, kiểu, giá trị
mặc định trong app. Pack sinh một enum và một class có property đúng kiểu cho
từng key, tự fetch khi mở game sau `com.riseon.firebase`, và kéo, đẩy tham số
với Firebase console ngay trong Editor.

Package `com.riseon.firebase.remoteconfig`, namespace `RiseOn.Firebase.RemoteConfig`.

## Mục lục

- [Yêu cầu](#yêu-cầu)
- [Cài đặt](#cài-đặt)
- [Khai báo tham số](#khai-báo-tham-số)
- [Code sinh ra](#code-sinh-ra)
- [Luồng khi mở game](#luồng-khi-mở-game)
- [Dùng trong game](#dùng-trong-game)
- [Khi fetch lỗi](#khi-fetch-lỗi)
- [Tham số live](#tham-số-live)
- [Fetch](#fetch)
- [Kéo và đẩy với Firebase console](#kéo-và-đẩy-với-firebase-console)
- [Lịch sử thay đổi](#lịch-sử-thay-đổi)
- [Giấy phép](#giấy-phép)

## Yêu cầu

| Phụ thuộc | Cách có | Dùng cho |
|---|---|---|
| Unity 6000.3 | | Bản đang dùng để phát triển |
| [Odin Inspector](https://odininspector.com) | Cài tay từ Asset Store | Các mục của trang cài đặt |
| `com.riseon.firebase` 2.0.0 | Tự cài theo `package.json` | Khởi động Firebase, project trong file cấu hình |
| `com.riseon.utils` 2.0.0 | Tự cài theo `package.json` | Các mục của trang cài đặt |
| `com.google.firebase.remote-config` 13.17.0 | Tự cài theo `package.json`, từ registry RiseOn | Firebase Remote Config SDK |
| `com.cysharp.unitask` 2.5.11 | Tự cài theo `package.json`, từ OpenUPM | Các hàm `async` |
| [Firebase CLI](https://firebase.google.com/docs/cli) | `npm install -g firebase-tools` | Chỉ khi [kéo và đẩy](#kéo-và-đẩy-với-firebase-console) qua tài khoản Google |

"Tự cài" là Package Manager tự lấy theo version từ Unity Registry và các registry khai
trong manifest, cả khi cài pack bằng git URL. Odin không có trên UPM nên phải cài vào
project trước. Thiếu Odin thì project báo một lỗi từ
`RiseOn.Firebase.RemoteConfig.Requirements`.

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
    "com.riseon.firebase.remoteconfig": "2.0.1"
  }
}
```

**Git URL**: phụ thuộc vẫn lấy theo version từ registry, nên manifest cần đủ các
registry như ví dụ trên, vì SDK Firebase chỉ có trên registry RiseOn. Rồi
*Package Manager → + → Add package from git URL*:

```
https://github.com/riseongamestudio/Firebase.git?path=/RemoteConfig
```

**Thư mục local**:
`"com.riseon.firebase.remoteconfig": "file:D:/path/to/Firebase/RemoteConfig"`.

## Khai báo tham số

Dev mở bằng menu **RiseOn > Firebase > Remote Config**, hoặc Edit > Project Settings
> RiseOn > Firebase > Remote Config. Lần mở đầu tạo
`Assets/RiseOn/Resources/RiseOnRemoteConfigSettings.asset`. Inspector của asset chỉ
có nút mở trang này.

Mục Parameters liệt kê mỗi tham số một dòng: key ở cột nhãn, kiểu ở cột giá trị. Mũi
tên đầu dòng mở ra dưới dòng các ô còn lại, mỗi ô một dòng.

| Ô | Ý nghĩa |
|---|---|
| Key | Key trên console: chữ cái hoặc `_`, rồi chữ, số, `_` |
| Kiểu | `Bool`, `Int`, `Float`, `String`, `Json`. Đổi sang kiểu mà giá trị mặc định đang có không hợp thì giá trị về `false`, `0`, chuỗi rỗng hay `{}` theo kiểu mới |
| Default | Giá trị mặc định trong app: dùng tới khi luồng tải xong, khi game đi tiếp sau lỗi mà máy chưa từng fetch được key đó, và cho key console không có |
| Live | [Tham số live](#tham-số-live) |
| Description, Group | Mô tả và nhóm tham số trên console; mô tả cũng thành doc comment trong code sinh ra |

Sửa trên trang ghi thẳng vào asset; Ctrl+Z hoàn tác như mọi chỗ khác của Unity. Ghi
file key thì Unity compile lại, nên trang không ghi theo từng lần sửa. Khi key, kiểu
hay mô tả khác file key, `RemoteKeys` chưa theo kịp và trang báo vàng kèm hai nút:

- **Apply**: ghi file key ngay. Key nào đã đổi tên thì `RemoteKeys.ten_cu` và
  `RemoteKey.ten_cu` trong script dưới `Assets` và trong package embedded đổi luôn
  thành tên mới, để code vẫn compile; hộp vàng liệt kê trước các key sẽ đổi. Code gọi
  qua `using static`, hay nằm ở chỗ khác, dev tự sửa.
- **Revert**: đưa danh sách tham số về lần cuối trang thấy chúng khớp file key, kể
  cả giá trị mặc định và Live lúc đó; mọi sửa sau đó mất, Ctrl+Z lấy lại được. Bản đó
  lưu trong `Library/` của máy này, nên nút mờ khi máy chưa giữ bản nào, hoặc khi file
  key đổi từ chỗ khác (pull, merge); rê chuột lên nút để xem lý do.

Rời trang hoặc đóng cửa sổ lúc còn báo vàng thì Unity hỏi **Apply** hay **Discard**
(Discard làm như Revert). Không Revert được thì hộp hỏi **Apply** hay **Later**;
Later để file chờ, và build dừng tới khi ghi. Unity compile vì lý do khác thì không
hỏi, file vẫn chờ.

Lỗi hiện ở đầu trang; còn lỗi thì không ghi file key, không đẩy lên console, và
build dừng:

| Lỗi | Dev làm gì |
|---|---|
| Key sai luật, key trùng, giá trị mặc định không đúng kiểu | Sửa dòng đó |
| Key `value__` hoặc `RemoteKeys`, tên C# không cho dùng ở chỗ code sinh ra đặt nó | Đổi tên key |
| Hơn 2000 tham số, giới hạn của Firebase | Bớt tham số |
| Hai key cùng số (hai nhánh merge trúng cùng số ngẫu nhiên) | Xóa key mà field enum đã lưu không trỏ tới rồi thêm lại để có số mới |
| File key chưa theo asset: trang còn mở, hoặc đã chọn Later (trên trang chỉ báo vàng) | Bấm **Apply** hoặc **Revert** |

## Code sinh ra

Source generator của pack đọc
`Assets/RiseOn/RemoteConfig/Keys.RiseOnRemoteConfigGenerator.additionalfile`, file
trang ghi khi dev bấm Apply (xem trên), rồi sinh code lúc compile, không ra file
`.cs`. Chỉ key, kiểu và mô tả nằm trong file đó, nên đổi giá trị mặc định hay Live
không làm Unity compile lại. Asset được import (pull, merge) thì pack tự ghi lại file,
đổi tên trong code như Apply, và cấp số cho key chưa có số; trang đang mở thì việc đó
chờ tới lúc dev chọn trên trang: khi đó bấm **Apply**, vì Revert hay Discard bỏ luôn
thay đổi vừa lấy về. Pull từ console ghi file ngay.
File key có dòng hỏng (thường do conflict khi merge) thì compile báo `RISEONRC001`:
dev mở trang, bấm **Apply**.

```csharp
namespace RiseOn.Firebase.RemoteConfig {
    public enum RemoteKey {
        /// <summary>Giây giữa hai interstitial</summary>
        interstitial_every_seconds = 1477070679,
        level_list = 1715901910,
    }

    public static partial class RemoteKeys {
        /// <summary>Giây giữa hai interstitial</summary>
        public static RemoteValue<int> interstitial_every_seconds => RemoteValues.Get<int>(1477070679);

        public static RemoteValue<string> level_list => RemoteValues.Get<string>(1715901910);
    }
}
```

Tên trong code là key viết y nguyên: `interstitial_every_seconds` là
`RemoteKeys.interstitial_every_seconds`; key trùng từ khóa C# thì thêm `@`
(`RemoteKeys.@class`). `RemoteKeys` không có member nào khác, nên key nào console nhận
cũng có tên trong code, trừ `value__` và `RemoteKeys` (xem bảng lỗi ở trên). Mỗi key
mang một số ngẫu nhiên cố định, nên field enum đã lưu trong scene, prefab vẫn trỏ đúng
key khi key khác đổi tên hay bị xóa; giá trị của field đó đọc bằng `key.Get<T>()`.

| Kiểu | Kiểu C# | Kiểu trên console |
|---|---|---|
| `Bool` | `bool` | `BOOLEAN` |
| `Int` | `int` | `NUMBER` |
| `Float` | `float` | `NUMBER` |
| `String` | `string` | `STRING` |
| `Json` | `string` | `JSON` |

## Luồng khi mở game

1. Trước khi scene đầu load, pack chờ `FirebaseInit.WaitUntilCompleteAsync()`.
2. Đặt giá trị mặc định trong app và cài đặt fetch (lần đầu của mỗi lần mở game).
3. Theo Loading:
   - `WaitForFetch` (mặc định): fetch rồi activate; màn boot chờ tối đa bằng
     fetch timeout.
   - `NextLaunch`: activate giá trị lần mở trước đã fetch, không chờ mạng.
4. Mọi `RemoteValue` nhận giá trị của lần mở game này; hoàn thành task mà các
   bên đang chờ; nếu có tham số live thì bắt đầu nghe cập nhật.
5. Với `NextLaunch`: fetch chạy nền cho lần mở sau. Fetch nền lỗi thì chỉ ghi
   cảnh báo, lần mở sau dùng lại giá trị cũ và fetch tiếp.

Nếu bước 2 hoặc 3 lỗi, xem [Khi fetch lỗi](#khi-fetch-lỗi).

## Dùng trong game

```csharp
using RiseOn.Firebase.RemoteConfig;

// Key chọn trong Inspector, field của một MonoBehaviour.
[SerializeField] private RemoteKey key;

// Màn boot: chờ giá trị của lần mở game này.
await RemoteConfigInit.WaitUntilCompleteAsync();

int interval = RemoteKeys.interstitial_every_seconds;    // đổi ngầm sang int
var levels   = RemoteKeys.level_list.Value;
var value    = key.Get<int>().Value;

// Giá trị đổi (khi fetch xong, và mỗi lần console đăng giá trị mới cho tham số live).
RemoteKeys.interstitial_every_seconds.Changed += value => interstitial.Interval = value;
```

Trước khi luồng xong, `Value` là giá trị mặc định trong app. `Get<T>` sai kiểu
thì ném `InvalidCastException`; key không còn trong settings (field enum trỏ tới key
đã xóa) thì ném `KeyNotFoundException`. Giá trị từ console không đọc được theo kiểu của
tham số (ví dụ `"abc"` cho `Int`) thì giữ giá trị mặc định và ghi cảnh báo.

`WaitUntilCompleteAsync()` không bao giờ lỗi.

## Khi fetch lỗi

Luồng dừng lại, mọi bên đang chờ `WaitUntilCompleteAsync()` đứng yên cho tới khi game
quyết định, kể cả khi máy đã có giá trị fetch từ lần mở trước.

Đúng một chỗ trong game (thường là màn boot) nhận kết quả và quyết định:

```csharp
using RiseOn.Firebase.RemoteConfig;

var result = await RemoteConfigInit.PopResultAsync();
while (result is RemoteConfigInitFailed failed) {
    // Fetch quá dày thì thử lại cũng bị từ chối: đi tiếp với giá trị đang có.
    if (failed.Throttled || !await noConnectionPopup.AskRetryAsync()) {
        await failed.ContinueAsync();
        break;
    }

    result = await failed.RetryAsync();
}
```

| API | Ý nghĩa |
|---|---|
| `PopResultAsync()` | Kết quả của lần chạy hiện tại: `RemoteConfigInitSucceeded`, `RemoteConfigInitFailed`, hoặc `RemoteConfigInitUnsupported` nơi Firebase không chạy (xem [Nền tảng khác](../Core/README.md#nền-tảng-khác)): mọi giá trị giữ mặc định trong app. Mỗi lần chạy chỉ lấy ra được một lần; gọi lần thứ hai thì ném `InvalidOperationException` |
| `RemoteConfigInitFailed.Throttled` | `true` khi Firebase từ chối fetch vì fetch quá dày: thử lại cũng bị từ chối tới khi hết khoảng chờ. Lỗi khác, kể cả lỗi không phải fetch, là `false` |
| `RemoteConfigInitFailed.Error` | Exception của SDK; fetch lỗi ném `FirebaseException` không có message, lý do SDK ghi cho lần fetch đó nằm trong log lỗi của `RemoteConfigInit` |
| `RemoteConfigInitFailed.RetryAsync()` | Fetch lại, trả kết quả mới cho chính bên gọi |
| `RemoteConfigInitFailed.ContinueAsync()` | Xong luồng với giá trị máy đã fetch được, key nào chưa có thì lấy giá trị mặc định trong app; lần mở sau fetch lại |

Mỗi `RemoteConfigInitFailed` chỉ thử lại hoặc đi tiếp được một lần. Game không lấy kết
quả ra thì luồng chờ mãi, lỗi chỉ hiện trong log.

## Tham số live

Mặc định một tham số giữ giá trị của lúc mở game tới lần mở sau, để giữa ván chơi
không có gì đổi. Tham số bật Live thì nhận giá trị mới ngay khi console đăng, qua
kết nối real-time của Firebase, và gọi `Changed`. Kết nối chỉ mở khi có ít nhất
một tham số live. Activate là việc của cả template, nhưng pack chỉ đổi giá trị
của tham số live; tham số khác nhận giá trị mới ở lần mở game sau.

Cần bật Firebase Remote Config Realtime API cho project trong Google Cloud.

## Fetch

| Mục | Mặc định | Ý nghĩa |
|---|---|---|
| Loading | `WaitForFetch` | `WaitForFetch`: mở game chờ fetch, kiểu Firebase [khuyên mạnh khi có A/B test](https://firebase.google.com/docs/remote-config/loading). `NextLaunch`: mở game dùng ngay giá trị lần trước đã fetch và fetch cho lần sau, kiểu [best practices](https://firebase.google.com/docs/remote-config/optimization-best-practices) của Firebase khuyên cho trường hợp còn lại; thay đổi trên console tới người chơi chậm một lần mở, lần mở đầu tiên dùng giá trị mặc định |
| Timeout (s) | 3 | Quá thời gian này thì fetch lỗi. Mặc định của SDK là 60 giây, theo [Firebase](https://firebase.google.com/docs/remote-config/loading) là quá dài cho màn loading; 3 giây là mức Firebase dùng trong [bài giới thiệu SDK V2](https://firebase.blog/posts/2019/08/remote-config-v2-sdk-update) |
| Interval (min) | 720 | Fetch trong khoảng này sau lần trước thì trả giá trị đã lưu, không gọi server. 12 giờ là mặc định của SDK, [hướng dẫn Unity](https://firebase.google.com/docs/remote-config/unity/get-started) gọi là con số tốt cho bản phát hành; bài SDK V2 nói xuống quá mỗi giờ một lần dễ bị giới hạn tần suất |
| Dev Interval (min) | 0 | Như trên, cho Development Build và Editor. Mẫu của Firebase dùng 0 khi phát triển để sửa trên console thì thấy ngay, và nhắc đặt lại trước khi phát hành |

Tham số cần đổi nhanh hơn 12 giờ thì bật [Live](#tham-số-live): real-time bỏ qua
minimum fetch interval.

## Kéo và đẩy với Firebase console

Pull và Push vào project trong file cấu hình của `com.riseon.firebase`, theo cách truy
cập chọn ở trang Firebase (xem [Truy cập console](../Core/README.md#truy-cập-console)).
Chưa có project (thiếu file cấu hình, hoặc hai file trỏ hai project khác nhau) hoặc chưa
truy cập được thì đầu trang Remote Config có hộp cảnh báo nói lý do; nút **Open
Firebase** trong hộp mở trang Firebase để sửa.

Trên thanh Parameters, nút mũi tên chéo mở Remote Config của project trên Firebase
console. Hai nút đám mây, mũi tên xuống là **Pull**, mũi tên lên là **Push**, mở cửa
sổ so sánh từng key. Các nút mờ khi chưa dùng được; rê chuột lên nút để xem lý do:

| Thay đổi | Pull | Push |
|---|---|---|
| Add | Key chỉ console có, thêm vào asset với số mới | Key chỉ asset có, thêm lên console |
| Update | Ghi kiểu, mặc định, mô tả, nhóm của console vào asset | Ghi của asset lên console |
| Remove | Key chỉ asset có, xóa khỏi asset | Key chỉ console có, xóa khỏi console |

Add và Update được chọn sẵn, Remove thì không. Chỉ so kiểu, giá trị mặc định, mô
tả và nhóm; giá trị theo điều kiện, điều kiện, A/B test là của console và giữ
nguyên khi đẩy; Live là của asset. Tham số trên console dùng "in-app default"
được coi như khớp mọi giá trị mặc định.

Push lấy lại template mới nhất ngay lúc bấm, chỉ ghi những key đã chọn vào đó, hỏi
lại một lần rồi mới đăng; với service account, server kiểm template trước khi hỏi.
Khác nhau giữa hai cách truy cập:

| | Service Account | Firebase CLI |
|---|---|---|
| Nút **Validate** (server kiểm tra, không đăng) | Có | Không; lỗi hiện khi đăng |
| Ai đó đăng xen giữa lúc lấy và lúc đăng | Đăng thất bại, báo bấm **Refresh** | CLI đăng đè bản mới nhất |

Mỗi lần đăng là một version mới trên console, xem và rollback ở mục Remote Config
> Change history.

## Lịch sử thay đổi

Xem [CHANGELOG.md](CHANGELOG.md).

## Giấy phép

MIT, xem [LICENSE.md](LICENSE.md).