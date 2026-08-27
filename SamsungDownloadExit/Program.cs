namespace SamsungDownloadExit;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Contains("--self-test", StringComparer.OrdinalIgnoreCase))
        {
            Environment.ExitCode = OdinProtocol.SelfTest() ? 0 : 1;
            return;
        }

        if (args.Contains("--usb-test", StringComparer.OrdinalIgnoreCase))
        {
            var status = new OdinProtocol().Detect();
            Environment.ExitCode = status.Message.Contains("libusb", StringComparison.OrdinalIgnoreCase)
                ? 2
                : status.State == DeviceState.Error ? 1 : 0;
            return;
        }

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
