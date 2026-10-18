namespace ap_date;

public partial class DateForm : Form
{
    private readonly Label date;
    private readonly CpuGaugeControl cpuGauge;
    private readonly MemoryGaugeControl memoryGauge;

    public DateForm()
    {
        InitializeComponent();
        ClientSize = new Size(375, 270);
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
            Location = new Point(10, 85),
            Size = new Size(170, 170)
        };

        memoryGauge = new MemoryGaugeControl
        {
            Location = new Point(195, 85),
            Size = new Size(170, 170)
        };

        Controls.Add(date);
        Controls.Add(cpuGauge);
        Controls.Add(memoryGauge);
        Text = "AP-Date";
    }

    private void UpdateDate()
    {
        date.Text = DateTime.Now.ToString("dd/MMM HH:mm");
        // Examples of other formats: "d MMM"  -> 17 Dec
    }
}
