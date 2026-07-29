# LINE Bot Local

LINE Bot แบบ echo ที่รันจากเครื่อง local ได้เลย

## ขั้นตอนการใช้งาน

### 1. ติดตั้ง .NET 8 SDK
ดาวน์โหลดจาก https://dotnet.microsoft.com/download/dotnet/8.0

### 2. ตั้งค่า LINE credentials
แก้ไขไฟล์ `appsettings.json`:
```json
{
  "LineBot": {
    "ChannelAccessToken": "ใส่ token จาก LINE Developers Console",
    "ChannelSecret": "ใส่ secret จาก LINE Developers Console"
  }
}
```

### 3. รันโปรแกรม
```bash
cd LineBotLocal
dotnet run
```
โปรแกรมจะเปิดที่ http://localhost:5000

### 4. เปิด tunnel ด้วย ngrok
```bash
ngrok http 5000
```
จะได้ URL เช่น `https://xxxx.ngrok-free.app`

### 5. ตั้ง Webhook URL ใน LINE Developers Console
ไปที่ Messaging API settings > Webhook URL ใส่:
```
https://xxxx.ngrok-free.app/webhook
```
แล้วกด Verify

## ฟีเจอร์
- Echo ข้อความกลับ
- Echo sticker กลับ
- Verify X-Line-Signature
- Log request ออก console
