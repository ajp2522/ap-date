using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ap_date;

public sealed class CpuGaugeControl : Control
{
    private const int RefreshIntervalMilliseconds = 10_000;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private ulong previousIdleTime;
    private ulong previousKernelTime;
    private ulong previousUserTime;
    private double? usagePercentage;

    public CpuGaugeControl()
    {
        Size = new Size(95, 95);
        BackColor = Color.FromArgb(24, 27, 29);
        ForeColor = Color.White;
        DoubleBuffered = true;

        ReadSystemTimes(out previousIdleTime, out previousKernelTime, out previousUserTime);

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
        e.Graphics.DrawString("CPU", labelFont, labelBrush,
            new RectangleF(0, center.Y - 23, Width, 28), centeredText);
        e.Graphics.DrawString(
            usagePercentage is double current ? $"{current:0}%" : "--%",
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
        ReadSystemTimes(out var idleTime, out var kernelTime, out var userTime);

        var idleDelta = idleTime - previousIdleTime;
        var totalDelta = (kernelTime - previousKernelTime) + (userTime - previousUserTime);
        usagePercentage = totalDelta == 0
            ? 0
            : Math.Clamp((double)(totalDelta - idleDelta) / totalDelta * 100, 0, 100);

        previousIdleTime = idleTime;
        previousKernelTime = kernelTime;
        previousUserTime = userTime;
        Invalidate();
    }

    private static void ReadSystemTimes(out ulong idleTime, out ulong kernelTime, out ulong userTime)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read system CPU usage.");
        }

        idleTime = ToUInt64(idle);
        kernelTime = ToUInt64(kernel);
        userTime = ToUInt64(user);
    }

    private static ulong ToUInt64(SystemFileTime time) =>
        ((ulong)time.HighDateTime << 32) | time.LowDateTime;

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(
        out SystemFileTime idleTime,
        out SystemFileTime kernelTime,
        out SystemFileTime userTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemFileTime
    {
        public uint LowDateTime;
        public uint HighDateTime;
    }
}
