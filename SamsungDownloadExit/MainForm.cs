using System.Diagnostics;
using System.Drawing.Drawing2D;

namespace SamsungDownloadExit;

internal sealed class MainForm : Form
{
    private readonly OdinProtocol protocol = new();
    private readonly Label stateLabel = new();
    private readonly Label detailLabel = new();
    private readonly Button rebootButton = new();
    private readonly Button refreshButton = new();
    private readonly LinkLabel driverHelp = new();
    private readonly Panel statusDot = new();

    public MainForm()
    {
        Text = "Samsung Download Exit";
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(246, 248, 251);
        ClientSize = new Size(690, 450);
        MinimumSize = new Size(650, 460);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;

        var header = new Panel { Dock = DockStyle.Top, Height = 76, BackColor = Color.White };
        var title = new Label
        {
            Text = "Samsung Download Exit",
            Font = new Font("Segoe UI Semibold", 17F),
            ForeColor = Color.FromArgb(24, 35, 52),
            AutoSize = true,
            Location = new Point(360, 14)
        };
        var subtitle = new Label
        {
            Text = "خروج آمن من Download Mode بضغطة واحدة",
            ForeColor = Color.FromArgb(101, 111, 126),
            AutoSize = true,
            Location = new Point(365, 48)
        };
        header.Controls.Add(title);
        header.Controls.Add(subtitle);

        var card = new Panel
        {
            Location = new Point(40, 104),
            Size = new Size(610, 290),
            BackColor = Color.White
        };
        card.Paint += (_, e) =>
        {
            using var pen = new Pen(Color.FromArgb(225, 230, 238));
            e.Graphics.DrawRectangle(pen, 0, 0, card.Width - 1, card.Height - 1);
        };

        statusDot.Size = new Size(16, 16);
        statusDot.Location = new Point(555, 30);
        using (var dotShape = new GraphicsPath())
        {
            dotShape.AddEllipse(0, 0, 15, 15);
            statusDot.Region = new Region(dotShape);
        }
        statusDot.Paint += (_, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(statusDot.BackColor);
            e.Graphics.FillEllipse(brush, 0, 0, 15, 15);
        };

        stateLabel.Font = new Font("Segoe UI Semibold", 14F);
        stateLabel.ForeColor = Color.FromArgb(24, 35, 52);
        stateLabel.AutoSize = false;
        stateLabel.Location = new Point(40, 22);
        stateLabel.Size = new Size(500, 34);
        stateLabel.TextAlign = ContentAlignment.MiddleRight;

        detailLabel.ForeColor = Color.FromArgb(101, 111, 126);
        detailLabel.AutoSize = false;
        detailLabel.Location = new Point(40, 63);
        detailLabel.Size = new Size(530, 50);
        detailLabel.TextAlign = ContentAlignment.TopRight;

        rebootButton.Text = "إعادة تشغيل الجهاز والخروج من Download Mode";
        rebootButton.Location = new Point(40, 128);
        rebootButton.Size = new Size(530, 58);
        rebootButton.FlatStyle = FlatStyle.Flat;
        rebootButton.FlatAppearance.BorderSize = 0;
        rebootButton.BackColor = Color.FromArgb(0, 184, 107);
        rebootButton.ForeColor = Color.White;
        rebootButton.Font = new Font("Segoe UI Semibold", 11F);
        rebootButton.Cursor = Cursors.Hand;
        rebootButton.Click += RebootClicked;

        refreshButton.Text = "إعادة فحص الاتصال";
        refreshButton.Location = new Point(325, 207);
        refreshButton.Size = new Size(245, 40);
        refreshButton.FlatStyle = FlatStyle.Flat;
        refreshButton.FlatAppearance.BorderColor = Color.FromArgb(205, 212, 222);
        refreshButton.BackColor = Color.White;
        refreshButton.Cursor = Cursors.Hand;
        refreshButton.Click += async (_, _) => await RefreshStatusAsync();

        driverHelp.Text = "مساعدة تعريف USB (WinUSB)";
        driverHelp.LinkColor = Color.FromArgb(0, 128, 96);
        driverHelp.AutoSize = false;
        driverHelp.TextAlign = ContentAlignment.MiddleCenter;
        driverHelp.Location = new Point(40, 207);
        driverHelp.Size = new Size(245, 40);
        driverHelp.LinkClicked += (_, _) => ShowDriverHelp();

        var safety = new Label
        {
            Text = "الأداة لا تفلش أي ملفات ولا تمسح بياناتك؛ ترسل أمر reboot فقط.",
            ForeColor = Color.FromArgb(100, 110, 124),
            AutoSize = false,
            Location = new Point(40, 260),
            Size = new Size(530, 22),
            TextAlign = ContentAlignment.MiddleCenter
        };

        card.Controls.AddRange([statusDot, stateLabel, detailLabel, rebootButton, refreshButton, driverHelp, safety]);
        Controls.Add(card);
        Controls.Add(header);
        Shown += async (_, _) => await RefreshStatusAsync();
    }

    private async Task RefreshStatusAsync()
    {
        SetBusy("جارٍ فحص الجهاز…");
        var status = await Task.Run(protocol.Detect);
        RenderStatus(status);
    }

    private async void RebootClicked(object? sender, EventArgs e)
    {
        SetBusy("جارٍ الاتصال بالجهاز…");
        try
        {
            await Task.Run(() => protocol.Reboot(message =>
                BeginInvoke(() => detailLabel.Text = message)));
            statusDot.BackColor = Color.FromArgb(0, 184, 107);
            stateLabel.Text = "تم إرسال أمر إعادة التشغيل";
            detailLabel.Text = "افصل الكابل بعد ظهور شعار Samsung. قد يستغرق الإقلاع عدة ثوانٍ.";
            rebootButton.Enabled = false;
            refreshButton.Enabled = true;
        }
        catch (Exception ex)
        {
            RenderStatus(new DeviceStatus(
                ex.Message.Contains("driver", StringComparison.OrdinalIgnoreCase)
                    ? DeviceState.DriverUnavailable : DeviceState.Error,
                ex.Message));
        }
    }

    private void SetBusy(string message)
    {
        statusDot.BackColor = Color.FromArgb(245, 166, 35);
        stateLabel.Text = "انتظر لحظة";
        detailLabel.Text = message;
        rebootButton.Enabled = false;
        refreshButton.Enabled = false;
    }

    private void RenderStatus(DeviceStatus status)
    {
        refreshButton.Enabled = true;
        detailLabel.Text = status.Message;
        switch (status.State)
        {
            case DeviceState.Ready:
                statusDot.BackColor = Color.FromArgb(0, 184, 107);
                stateLabel.Text = "الجهاز جاهز";
                rebootButton.Enabled = true;
                break;
            case DeviceState.DriverUnavailable:
                statusDot.BackColor = Color.FromArgb(245, 166, 35);
                stateLabel.Text = "تعريف USB مطلوب";
                rebootButton.Enabled = false;
                break;
            default:
                statusDot.BackColor = Color.FromArgb(220, 70, 70);
                stateLabel.Text = status.State == DeviceState.NotFound ? "الجهاز غير متصل" : "تعذّر الاتصال";
                rebootButton.Enabled = false;
                break;
        }
        statusDot.Invalidate();
    }

    private static void ShowDriverHelp()
    {
        MessageBox.Show(
            "لو الجهاز ظاهر لكن البرنامج لا يقدر يفتحه:\n\n" +
            "1) افتح Zadig كمسؤول.\n" +
            "2) من Options اختر List All Devices.\n" +
            "3) اختر Samsung USB Composite Device أو Gadget Serial (04E8:685D).\n" +
            "4) اختر WinUSB ثم Install/Replace Driver.\n\n" +
            "مهم: اختر جهاز Samsung فقط. ويمكن إعادة تعريف Samsung الرسمي لاحقًا إذا احتجت Odin.",
            "تعريف USB المطلوب", MessageBoxButtons.OK, MessageBoxIcon.Information,
            MessageBoxDefaultButton.Button1, MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign);
    }
}
