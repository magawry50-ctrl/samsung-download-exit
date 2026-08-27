# Samsung Download Exit

أداة Windows صغيرة لإعادة تشغيل أجهزة Samsung والخروج من **Download Mode** بدون زر Power.

## الاستخدام

1. أدخل الجهاز إلى Download Mode عن طريق توصيل الكابل أثناء الضغط على زري رفع وخفض الصوت.
2. شغّل `Samsung Download Exit.exe`.
3. عندما تظهر الحالة **الجهاز جاهز** اضغط زر إعادة التشغيل الأخضر.

الأداة ترسل فقط تسلسل Samsung/Odin الآمن لبدء جلسة ثم إنهائها مع طلب reboot. لا تفلش firmware، ولا تفتح bootloader، ولا تمسح بيانات.

## تعريف USB على Windows

الاتصال منخفض المستوى يحتاج أن يكون جهاز Download Mode معرفًا بـ **WinUSB** (المفضل) أو libusbK. لو ظهر تنبيه تعريف USB:

1. افتح [Zadig](https://zadig.akeo.ie/) كمسؤول.
2. اختر **Options → List All Devices**.
3. اختر جهاز Samsung الذي يحمل VID `04E8` (غالبًا PID `685D` واسم `Gadget Serial`).
4. اختر **WinUSB** ثم **Install Driver** أو **Replace Driver**.

اختر جهاز Samsung الصحيح فقط. تغيير التعريف قد يجعل Odin الرسمي لا يراه إلى أن تعيد تثبيت Samsung USB Driver.

## البناء

يتطلب .NET 10 SDK:

```powershell
dotnet publish .\SamsungDownloadExit\SamsungDownloadExit.csproj -c Release -r win-x64 --self-contained true
```

الناتج في `SamsungDownloadExit/bin/Release/net10.0-windows/win-x64/publish/`.

## المصدر والترخيص

تنفيذ حزم Odin مبني على التوثيق العملي في مشروع [Heimdall](https://github.com/Benjamin-Dobell/Heimdall) (MIT). الاتصال بـ USB يتم عبر [LibUsbDotNet](https://github.com/LibUsbDotNet/LibUsbDotNet) (LGPL-3.0-or-later).
