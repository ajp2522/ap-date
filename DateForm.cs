namespace ap_date;

public partial class DateForm : Form
{
    private readonly Label date;
    private readonly CpuGaugeControl cpuGauge;
    private readonly GpuGaugeControl gpuGauge;
    private readonly MemoryGaugeControl memoryGauge;
    private readonly NetworkGaugeControl networkGauge;

    public DateForm()
    {
        InitializeComponent();
        ClientSize = new Size(222, 250);
        BackColor = Color.FromArgb(24, 27, 29);
        ForeColor = Color.White;

        date = new Label();
        date.AutoSize = true;
        date.BackColor = BackColor;
        date.Font = new Font("Segoe UI", 12F);
        date.ForeColor = ForeColor;
        date.Location = new Point(10, 0);

        UpdateDate();

        cpuGauge = new CpuGaugeControl
        {
            Location = new Point(10, 35),
            Size = new Size(95, 95)
        };

        gpuGauge = new GpuGaugeControl
        {
            Location = new Point(10, 145),
            Size = new Size(95, 95)
        };

        memoryGauge = new MemoryGaugeControl
        {
            Location = new Point(117, 35),
            Size = new Size(95, 95)
        };

        networkGauge = new NetworkGaugeControl
        {
            Location = new Point(117, 145),
            Size = new Size(95, 95)
        };

        Controls.Add(date);
        Controls.Add(cpuGauge);
        Controls.Add(gpuGauge);
        Controls.Add(memoryGauge);
        Controls.Add(networkGauge);
        Text = "AP-Date";
    }

    private void UpdateDate()
    {
        date.Text = DateTime.Now.ToString("dd/MMM HH:mm");
        // Examples of other formats: "d MMM"  -> 17 Dec
    }
}
