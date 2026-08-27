using System.Buffers.Binary;
using System.Text;
using LibUsbDotNet;
using LibUsbDotNet.LibUsb;
using LibUsbDotNet.Main;

namespace SamsungDownloadExit;

internal enum DeviceState
{
    Ready,
    NotFound,
    DriverUnavailable,
    Busy,
    Error
}

internal sealed record DeviceStatus(DeviceState State, string Message, int? ProductId = null);

internal sealed class OdinProtocol
{
    private const int SamsungVendorId = 0x04E8;
    private static readonly HashSet<int> DownloadModeProductIds = [0x6601, 0x685D, 0x68C3];
    private const int TimeoutMs = 3000;

    public DeviceStatus Detect()
    {
        try
        {
            using var context = new UsbContext();
            using var devices = context.List();
            var samsung = devices.Where(d => d.VendorId == SamsungVendorId).ToList();
            var download = samsung.FirstOrDefault(d => DownloadModeProductIds.Contains(d.ProductId));

            if (download is null)
            {
                return samsung.Count > 0
                    ? new(DeviceState.NotFound, "تم العثور على جهاز Samsung، لكنه ليس في Download Mode.")
                    : new(DeviceState.NotFound, "لم يتم العثور على جهاز Samsung في Download Mode.");
            }

            try
            {
                download.Open();
                download.Close();
                return new(DeviceState.Ready, $"الجهاز متصل وجاهز (04E8:{download.ProductId:X4}).", download.ProductId);
            }
            catch (Exception ex) when (IsDriverError(ex))
            {
                return new(DeviceState.DriverUnavailable,
                    $"الجهاز ظاهر (04E8:{download.ProductId:X4}) لكن تعريف WinUSB/libusb غير متاح.", download.ProductId);
            }
            catch (Exception ex)
            {
                return new(DeviceState.Busy, $"الجهاز ظاهر لكن تعذّر فتحه: {FriendlyError(ex)}", download.ProductId);
            }
        }
        catch (DllNotFoundException)
        {
            return new(DeviceState.DriverUnavailable, "مكوّن libusb غير موجود بجوار البرنامج.");
        }
        catch (Exception ex)
        {
            return new(DeviceState.Error, $"تعذّر فحص USB: {FriendlyError(ex)}");
        }
    }

    public void Reboot(Action<string>? progress = null)
    {
        progress ??= _ => { };
        using var context = new UsbContext();
        using var devices = context.List();
        var device = devices.FirstOrDefault(d =>
            d.VendorId == SamsungVendorId && DownloadModeProductIds.Contains(d.ProductId));

        if (device is null)
            throw new InvalidOperationException("لم يتم العثور على جهاز Samsung في Download Mode.");

        progress("جارٍ فتح اتصال USB…");
        device.Open();
        try
        {
            var interfaceNumber = FindDataInterface(device);
            device.ClaimInterface(interfaceNumber);
            try
            {
                var (writerId, readerId) = FindBulkEndpoints(device, interfaceNumber);
                var writer = device.OpenEndpointWriter(writerId);
                var reader = device.OpenEndpointReader(readerId);

                progress("جارٍ بدء بروتوكول Samsung/Odin…");
                WriteExact(writer, Encoding.ASCII.GetBytes("ODIN"));
                var hello = ReadExact(reader, 7, 4);
                if (!hello.AsSpan().SequenceEqual("LOKE"u8))
                    throw new IOException("استجابة Download Mode غير متوقعة.");

                progress("جارٍ بدء الجلسة…");
                WriteExact(writer, CreateControlPacket(0x64, 0));
                var beginResult = ValidateResponse(ReadExact(reader, 8, 8), 0x64, "بدء الجلسة");
                if (beginResult != 0)
                {
                    progress("جارٍ ضبط حجم اتصال الجهاز…");
                    WriteExact(writer, CreateSessionPacket(5, 1_048_576));
                    var sizeResult = ValidateResponse(ReadExact(reader, 8, 8), 0x64, "ضبط الاتصال");
                    if (sizeResult != 0)
                        throw new IOException("رفض الجهاز ضبط حجم اتصال USB.");
                }

                progress("جارٍ إنهاء الجلسة بأمان…");
                WriteExact(writer, CreateControlPacket(0x67, 0));
                ValidateResponse(ReadExact(reader, 8, 8), 0x67, "إنهاء الجلسة");

                progress("جارٍ إرسال أمر إعادة التشغيل…");
                WriteExact(writer, CreateControlPacket(0x67, 1));

                // Some bootloaders disconnect immediately instead of returning the final ACK.
                try
                {
                    var response = ReadExact(reader, 8, 8);
                    ValidateResponse(response, 0x67, "إعادة التشغيل");
                }
                catch (Exception) when (!IsStillPresent())
                {
                    // USB disconnect is success after a reboot request.
                }
            }
            finally
            {
                try { device.ReleaseInterface(interfaceNumber); } catch { }
            }
        }
        finally
        {
            try { device.Close(); } catch { }
        }
    }

    private static int FindDataInterface(IUsbDevice device)
    {
        var config = device.Configs.FirstOrDefault()
            ?? throw new IOException("لا يوجد USB configuration صالح.");

        var data = config.Interfaces.FirstOrDefault(i =>
            (byte)i.Class == 0x0A && i.Endpoints.Count == 2);
        return (data ?? config.Interfaces.FirstOrDefault(i => i.Endpoints.Count == 2)
            ?? throw new IOException("لم يتم العثور على واجهة USB الخاصة بـ Download Mode.")).Number;
    }

    private static (WriteEndpointID Writer, ReadEndpointID Reader) FindBulkEndpoints(IUsbDevice device, int interfaceNumber)
    {
        var usbInterface = device.Configs.SelectMany(c => c.Interfaces)
            .First(i => i.Number == interfaceNumber);
        var outAddress = usbInterface.Endpoints.FirstOrDefault(e =>
            (e.EndpointAddress & 0x80) == 0 && (e.Attributes & 0x03) == 2)?.EndpointAddress;
        var inAddress = usbInterface.Endpoints.FirstOrDefault(e =>
            (e.EndpointAddress & 0x80) != 0 && (e.Attributes & 0x03) == 2)?.EndpointAddress;

        if (outAddress is null || inAddress is null)
            throw new IOException("لم يتم العثور على Bulk USB endpoints.");

        return ((WriteEndpointID)outAddress.Value, (ReadEndpointID)inAddress.Value);
    }

    private static void WriteExact(UsbEndpointWriter writer, byte[] data)
    {
        var error = writer.Write(data, TimeoutMs, out var transferred);
        if (error != Error.Success || transferred != data.Length)
            throw new IOException($"فشل إرسال USB ({error}، {transferred}/{data.Length} بايت).");
    }

    private static byte[] ReadExact(UsbEndpointReader reader, int bufferSize, int expected)
    {
        var buffer = new byte[bufferSize];
        var error = reader.Read(buffer, TimeoutMs, out var transferred);
        if (error != Error.Success || transferred != expected)
            throw new IOException($"فشل استقبال USB ({error}، {transferred}/{expected} بايت).");
        return buffer[..transferred];
    }

    internal static byte[] CreateControlPacket(uint type, uint request)
    {
        var packet = new byte[1024];
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(0, 4), type);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(4, 4), request);
        return packet;
    }

    internal static byte[] CreateSessionPacket(uint request, uint value)
    {
        var packet = CreateControlPacket(0x64, request);
        BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(8, 4), value);
        return packet;
    }

    private static uint ValidateResponse(byte[] packet, uint type, string operation)
    {
        if (packet.Length < 8 || BinaryPrimitives.ReadUInt32LittleEndian(packet) != type)
            throw new IOException($"استجابة غير صحيحة أثناء {operation}.");
        return BinaryPrimitives.ReadUInt32LittleEndian(packet.AsSpan(4, 4));
    }

    private static bool IsDriverError(Exception ex) =>
        ex is DllNotFoundException || ex.Message.Contains("driver", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("not supported", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("access", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("entity not found", StringComparison.OrdinalIgnoreCase);

    private static string FriendlyError(Exception ex) =>
        ex.Message.Replace("libusb", "USB", StringComparison.OrdinalIgnoreCase);

    private static bool IsStillPresent()
    {
        try
        {
            using var context = new UsbContext();
            using var devices = context.List();
            return devices.Any(d => d.VendorId == SamsungVendorId && DownloadModeProductIds.Contains(d.ProductId));
        }
        catch { return false; }
    }

    internal static bool SelfTest()
    {
        var begin = CreateControlPacket(0x64, 0);
        var reboot = CreateControlPacket(0x67, 1);
        var packetSize = CreateSessionPacket(5, 1_048_576);
        return begin.Length == 1024 && begin[0] == 0x64 && begin.Skip(1).All(b => b == 0)
            && reboot.Length == 1024 && reboot[0] == 0x67 && reboot[4] == 1
            && reboot.Take(4).SequenceEqual(new byte[] { 0x67, 0, 0, 0 })
            && packetSize[0] == 0x64 && packetSize[4] == 5 && packetSize[10] == 0x10;
    }
}
