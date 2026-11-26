# Lịch sử thay đổi

Mọi thay đổi đáng kể của `com.riseon.firebase.remoteconfig` được ghi ở đây. Định dạng theo
[Keep a Changelog](https://keepachangelog.com/vi/1.1.0/), đánh số theo
[Semantic Versioning](https://semver.org/lang/vi/).

## [2.0.1] - 2026-11-26

### Đổi

- Mục Parameters: mỗi tham số là một dòng, key ở cột nhãn, kiểu ở cột giá trị; mũi tên đầu dòng mở ra giá trị mặc định, Live, mô tả và nhóm, mỗi
  thứ một dòng thẳng cột giá trị của trang. Dữ liệu trong asset giữ nguyên.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.firebase` 1.0.3,
  `com.riseon.utils` 2.0.0.

## [2.0.0] - 2026-11-20

### Đổi

- `RemoteKeyExt` đổi tên thành `RemoteKeyUtils`, theo luật đặt tên lớp extension của các
  pack RiseOn. Gọi kiểu extension (`key.Get<int>()`) không phải sửa; code gọi
  `RemoteKeyExt.Get<T>(key)` thì đổi tên lớp.
- Cần Unity 6000.3, bản đang dùng để phát triển, thay cho 6000.0 chưa từng được thử:
  `package.json` ghi `unity`, nên Package Manager không cài pack vào project Unity cũ
  hơn.
- Phụ thuộc RiseOn lên bản phát hành cùng đợt: `com.riseon.firebase` 1.0.2,
  `com.riseon.utils` 1.0.18.

## [1.0.1] - 2026-11-20

### Sửa

- Project chưa có Odin Inspector: mọi assembly của pack dùng Odin được bỏ qua, và chỉ còn một lỗi
  nói pack cần Odin; trước đó project báo thêm hàng loạt lỗi compile khó hiểu.

## [1.0.0] - 2026-11-20

Bản đầu tiên.