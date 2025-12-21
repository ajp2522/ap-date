namespace ap_date;

public partial class DateForm : Form
{
    Label date;

    public DateForm()
    {
        InitializeComponent();

        // Create and configure the label
        date = new Label();
        date.AutoSize = true;
        date.Font = new System.Drawing.Font("Segoe UI", 24F);
        date.Location = new System.Drawing.Point(0, 0);

        // Initial update
        UpdateDate();

        // Add controls to the form
        Controls.Add(date);
        Text = "AP-Date";
        AutoSize = true;
    }

    private void UpdateDate()
    {
        date.Text = DateTime.Now.ToString("dd MM");
        // Examples of other formats:
        // "dd/MM" -> 17/12
        // "d MMM"  -> 17 Dec
    }
}
