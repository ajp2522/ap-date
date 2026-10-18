using System.ComponentModel;
using System.Runtime.InteropServices;

namespace ap_date;

public sealed class MemoryGaugeControl : Control
{
    private const int RefreshIntervalMilliseconds = 10_000;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private double usagePercentage;

    public MemoryGaugeControl()
    {
        Size = new Size(95, 95);
        BackColor = Color.FromArgb(24, 27, 29);
        ForeColor = Color.White;
        DoubleBuffered = true;

        UpdateUsage();

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

        if (usagePercentage > 0)
        {
            using var usagePen = new Pen(Color.LimeGreen, strokeWidth)
            {
                StartCap = System.Drawing.Drawing2D.LineCap.Round,
                EndCap = System.Drawing.Drawing2D.LineCap.Round
            };
            e.Graphics.DrawArc(usagePen, bounds, -90, (float)(usagePercentage / 100 * 360));
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
        e.Graphics.DrawString("Mem", labelFont, labelBrush,
            new RectangleF(0, center.Y - 23, Width, 28), centeredText);
        e.Graphics.DrawString(
            $"{usagePercentage:0}%",
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

    private void RefreshTimer_Tick(object? sender, EventArgs e) => UpdateUsage();

    private void UpdateUsage()
    {
        var memoryStatus = new MemoryStatusEx();
        if (!GlobalMemoryStatusEx(ref memoryStatus))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read system memory usage.");
        }

        usagePercentage = memoryStatus.TotalPhysical == 0
            ? 0
            : Math.Clamp(
                (double)(memoryStatus.TotalPhysical > memoryStatus.AvailablePhysical
                    ? memoryStatus.TotalPhysical - memoryStatus.AvailablePhysical
                    : 0)
                    / memoryStatus.TotalPhysical * 100,
                0,
                100);
        Invalidate();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx buffer);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhysical;
        public ulong AvailablePhysical;
        public ulong TotalPageFile;
        public ulong AvailablePageFile;
        public ulong TotalVirtual;
        public ulong AvailableVirtual;
        public ulong AvailableExtendedVirtual;

        public MemoryStatusEx()
        {
            Length = (uint)Marshal.SizeOf<MemoryStatusEx>();
            MemoryLoad = 0;
            TotalPhysical = 0;
            AvailablePhysical = 0;
            TotalPageFile = 0;
            AvailablePageFile = 0;
            TotalVirtual = 0;
            AvailableVirtual = 0;
            AvailableExtendedVirtual = 0;
        }
    }
}
