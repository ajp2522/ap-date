using System.Runtime.InteropServices;

namespace ap_date;

public sealed class GpuGaugeControl : Control
{
    private const int RefreshIntervalMilliseconds = 10_000;
    private const uint PdhMoreData = 0x800007D2;
    private const uint PdhNoData = 0x800007D5;
    private const uint PdhFormatDouble = 0x00000200;
    private const uint PdhValidData = 0;
    private const uint PdhNewData = 1;

    private readonly System.Windows.Forms.Timer refreshTimer;
    private IntPtr queryHandle;
    private IntPtr counterHandle;
    private double? usagePercentage;

    public GpuGaugeControl()
    {
        Size = new Size(95, 95);
        BackColor = Color.FromArgb(24, 27, 29);
        ForeColor = Color.White;
        DoubleBuffered = true;

        refreshTimer = new System.Windows.Forms.Timer
        {
            Interval = RefreshIntervalMilliseconds
        };
        try
        {
            OpenCounter();
            CollectSample();
        }
        catch
        {
            refreshTimer.Dispose();
            CloseCounter();
            throw;
        }

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
        e.Graphics.DrawString("GPU", labelFont, labelBrush,
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
            CloseCounter();
        }

        base.Dispose(disposing);
    }

    private void RefreshTimer_Tick(object? sender, EventArgs e)
    {
        CollectSample();
        usagePercentage = ReadUsagePercentage();
        Invalidate();
    }

    private void OpenCounter()
    {
        CheckPdhStatus(PdhOpenQuery(null, IntPtr.Zero, out queryHandle), "open a GPU performance query");

        var status = PdhAddEnglishCounter(
            queryHandle,
            @"\GPU Engine(*)\Utilization Percentage",
            IntPtr.Zero,
            out counterHandle);
        if (status != 0)
        {
            CloseCounter();
            CheckPdhStatus(status, "add the GPU utilization counter");
        }
    }

    private void CollectSample() =>
        CheckPdhStatus(PdhCollectQueryData(queryHandle), "collect GPU utilization data");

    private double? ReadUsagePercentage()
    {
        uint bufferSize = 0;
        uint itemCount = 0;
        var status = PdhGetFormattedCounterArray(
            counterHandle,
            PdhFormatDouble,
            ref bufferSize,
            ref itemCount,
            IntPtr.Zero);

        if (status == PdhNoData)
        {
            return null;
        }

        if (status != PdhMoreData)
        {
            CheckPdhStatus(status, "read GPU utilization data");
        }

        if (itemCount == 0 || bufferSize == 0)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(checked((int)bufferSize));
        try
        {
            status = PdhGetFormattedCounterArray(
                counterHandle,
                PdhFormatDouble,
                ref bufferSize,
                ref itemCount,
                buffer);
            if (status == PdhNoData)
            {
                return null;
            }

            CheckPdhStatus(status, "read GPU utilization data");

            var itemSize = Marshal.SizeOf<FormattedCounterItem>();
            double? busiestEngine = null;
            for (var index = 0; index < itemCount; index++)
            {
                var itemPointer = IntPtr.Add(buffer, checked((int)index * itemSize));
                var item = Marshal.PtrToStructure<FormattedCounterItem>(itemPointer);
                if (item.Value.Status is not (PdhValidData or PdhNewData)
                    || !double.IsFinite(item.Value.Value))
                {
                    continue;
                }

                var utilization = Math.Clamp(item.Value.Value, 0, 100);
                busiestEngine = busiestEngine is double current
                    ? Math.Max(current, utilization)
                    : utilization;
            }

            return busiestEngine;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private void CloseCounter()
    {
        if (queryHandle != IntPtr.Zero)
        {
            PdhCloseQuery(queryHandle);
            queryHandle = IntPtr.Zero;
            counterHandle = IntPtr.Zero;
        }
    }

    private static void CheckPdhStatus(uint status, string operation)
    {
        if (status != 0)
        {
            throw new InvalidOperationException(
                $"Unable to {operation}. PDH error: 0x{status:X8}.");
        }
    }

    [DllImport("pdh.dll", EntryPoint = "PdhOpenQueryW", CharSet = CharSet.Unicode)]
    private static extern uint PdhOpenQuery(string? dataSource, IntPtr userData, out IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhAddEnglishCounterW", CharSet = CharSet.Unicode)]
    private static extern uint PdhAddEnglishCounter(
        IntPtr query,
        string counterPath,
        IntPtr userData,
        out IntPtr counter);

    [DllImport("pdh.dll", EntryPoint = "PdhCollectQueryData")]
    private static extern uint PdhCollectQueryData(IntPtr query);

    [DllImport("pdh.dll", EntryPoint = "PdhGetFormattedCounterArrayW", CharSet = CharSet.Unicode)]
    private static extern uint PdhGetFormattedCounterArray(
        IntPtr counter,
        uint format,
        ref uint bufferSize,
        ref uint itemCount,
        IntPtr itemBuffer);

    [DllImport("pdh.dll", EntryPoint = "PdhCloseQuery")]
    private static extern uint PdhCloseQuery(IntPtr query);

    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterItem
    {
        public IntPtr Name;
        public FormattedCounterValue Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FormattedCounterValue
    {
        public uint Status;
        public double Value;
    }
}
