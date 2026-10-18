using System.Diagnostics;
using System.Net.NetworkInformation;

namespace ap_date;

public sealed class NetworkGaugeControl : Control
{
    private const int RefreshIntervalMilliseconds = 10_000;
    private const double BytesPerKilobyte = 1024;
    private const double BytesPerMegabyte = BytesPerKilobyte * 1024;
    private const double BytesPerGigabyte = BytesPerMegabyte * 1024;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private Dictionary<string, NetworkSnapshot> previousSnapshots;
    private long previousSampleTimestamp;
    private double? transferRateBytesPerSecond;
    private double? usagePercentage;

    public NetworkGaugeControl()
    {
        Size = new Size(95, 95);
        BackColor = Color.FromArgb(24, 27, 29);
        ForeColor = Color.White;
        DoubleBuffered = true;

        previousSnapshots = ReadNetworkSnapshots();
        previousSampleTimestamp = Stopwatch.GetTimestamp();

        refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        refreshTimer.Tick += RefreshTimer_Tick;
        refreshTimer.Start();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        const float strokeWidth = 6.72F;
        var bounds = new RectangleF(
            strokeWidth / 2 + 1.12F,
            strokeWidth / 2 + 1.12F,
            Width - strokeWidth - 2.24F,
            Height - strokeWidth - 2.24F);

        using var trackPen = new Pen(Color.FromArgb(56, 63, 65), strokeWidth);
        e.Graphics.DrawArc(trackPen, bounds, 0, 360);

        if (usagePercentage is double percentage && percentage > 0)
        {
            using var usagePen = new Pen(Color.LimeGreen, strokeWidth)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawArc(usagePen, bounds, -90, (float)(percentage / 100 * 360));
        }

        using var labelFont = new Font("Segoe UI", 16F, FontStyle.Bold);
        using var valueFont = new Font("Segoe UI", 11F);
        using var labelBrush = new SolidBrush(ForeColor);
        using var valueBrush = new SolidBrush(Color.LimeGreen);
        using var centeredText = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center
        };

        var center = new PointF(Width / 2F, Height / 2F);
        e.Graphics.DrawString("Net", labelFont, labelBrush,
            new RectangleF(0, center.Y - 23, Width, 28), centeredText);
        e.Graphics.DrawString(
            transferRateBytesPerSecond is double rate ? FormatTransferRate(rate) : "--",
            valueFont,
            valueBrush,
            new RectangleF(0, center.Y + 5, Width, 22),
            centeredText);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            refreshTimer.Stop();
            refreshTimer.Tick -= RefreshTimer_Tick;
            refreshTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        var currentSnapshots = ReadNetworkSnapshots();
        var currentTimestamp = Stopwatch.GetTimestamp();
        var elapsedSeconds = Stopwatch.GetElapsedTime(previousSampleTimestamp, currentTimestamp).TotalSeconds;
        ulong transferredBytes = 0;
        ulong totalCapacityBitsPerSecond = 0;

        foreach (var (interfaceId, current) in currentSnapshots)
        {
            totalCapacityBitsPerSecond += current.SpeedBitsPerSecond;

            if (previousSnapshots.TryGetValue(interfaceId, out var previous))
            {
                if (current.BytesReceived >= previous.BytesReceived)
                {
                    transferredBytes += current.BytesReceived - previous.BytesReceived;
                }

                if (current.BytesSent >= previous.BytesSent)
                {
                    transferredBytes += current.BytesSent - previous.BytesSent;
                }
            }
        }

        transferRateBytesPerSecond = elapsedSeconds > 0 ? transferredBytes / elapsedSeconds : 0;
        var totalCapacityBytesPerSecond = totalCapacityBitsPerSecond / 4d;
        usagePercentage = totalCapacityBytesPerSecond > 0
            ? Math.Clamp(transferRateBytesPerSecond.Value / totalCapacityBytesPerSecond * 100, 0, 100)
            : null;

        previousSnapshots = currentSnapshots;
        previousSampleTimestamp = currentTimestamp;
        Invalidate();
    }

    private static Dictionary<string, NetworkSnapshot> ReadNetworkSnapshots()
    {
        var snapshots = new Dictionary<string, NetworkSnapshot>();
        foreach (var networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (networkInterface.NetworkInterfaceType == NetworkInterfaceType.Loopback
                || networkInterface.OperationalStatus != OperationalStatus.Up)
            {
                continue;
            }

            var statistics = networkInterface.GetIPStatistics();
            snapshots.Add(
                networkInterface.Id,
                new NetworkSnapshot(
                    (ulong)statistics.BytesReceived,
                    (ulong)statistics.BytesSent,
                    (ulong)Math.Max(networkInterface.Speed, 0)));
        }

        return snapshots;
    }

    private static string FormatTransferRate(double bytesPerSecond) => bytesPerSecond switch
    {
        >= BytesPerGigabyte => $"{bytesPerSecond / BytesPerGigabyte:0.0} GB/s",
        >= BytesPerMegabyte => $"{bytesPerSecond / BytesPerMegabyte:0.0} MB/s",
        >= BytesPerKilobyte => $"{bytesPerSecond / BytesPerKilobyte:0.0} KB/s",
        _ => $"{bytesPerSecond:0} B/s"
    };

    private readonly record struct NetworkSnapshot(
        ulong BytesReceived,
        ulong BytesSent,
        ulong SpeedBitsPerSecond);
}
