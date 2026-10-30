#nullable enable

namespace PcAnalyzer.App;

partial class MainForm
{
    private System.ComponentModel.IContainer? components = null;

    private Panel headerPanel = null!;
    private Label lblTitle = null!;
    private Label lblHardware = null!;
    private TableLayoutPanel gridCards = null!;
    private StatusStrip statusStrip = null!;
    private ToolStripStatusLabel lblStatus = null!;
    private ToolStripStatusLabel lblSpacer = null!;
    private ToolStripStatusLabel lblUptime = null!;
    private ToolStripButton btnElevate = null!;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            components?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        headerPanel = new Panel();
        lblTitle = new Label();
        lblHardware = new Label();
        gridCards = new TableLayoutPanel();
        statusStrip = new StatusStrip();
        lblStatus = new ToolStripStatusLabel();
        lblSpacer = new ToolStripStatusLabel();
        lblUptime = new ToolStripStatusLabel();
        btnElevate = new ToolStripButton();

        SuspendLayout();

        // headerPanel
        headerPanel.Dock = DockStyle.Top;
        headerPanel.Height = 74;
        headerPanel.BackColor = Color.FromArgb(20, 22, 26);
        headerPanel.Padding = new Padding(20, 0, 20, 0);
        headerPanel.Controls.Add(lblHardware);
        headerPanel.Controls.Add(lblTitle);

        // lblTitle
        lblTitle.AutoSize = true;
        lblTitle.Font = new Font("Segoe UI Semibold", 15f);
        lblTitle.ForeColor = Color.FromArgb(236, 239, 243);
        lblTitle.Location = new Point(20, 14);
        lblTitle.Text = "Analizador de rendimiento";

        // lblHardware
        lblHardware.AutoSize = true;
        lblHardware.Font = new Font("Segoe UI", 9f);
        lblHardware.ForeColor = Color.FromArgb(128, 136, 150);
        lblHardware.Location = new Point(22, 46);
        lblHardware.Text = "Detectando hardware...";

        // gridCards
        gridCards.Dock = DockStyle.Fill;
        gridCards.BackColor = Color.FromArgb(20, 22, 26);
        gridCards.Padding = new Padding(10, 10, 10, 0);
        gridCards.ColumnCount = 2;
        gridCards.RowCount = 3;
        gridCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        gridCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        gridCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.34f));
        gridCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
        gridCards.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

        // statusStrip
        statusStrip.BackColor = Color.FromArgb(20, 22, 26);
        statusStrip.ForeColor = Color.FromArgb(128, 136, 150);
        statusStrip.SizingGrip = false;
        statusStrip.Items.AddRange(new ToolStripItem[]
        {
            lblStatus,
            lblSpacer,
            lblUptime,
            btnElevate
        });

        // lblStatus
        lblStatus.ForeColor = Color.FromArgb(128, 136, 150);
        lblStatus.Text = "Iniciando...";

        // lblSpacer
        lblSpacer.Spring = true;
        lblSpacer.Text = string.Empty;

        // lblUptime
        lblUptime.ForeColor = Color.FromArgb(128, 136, 150);
        lblUptime.Text = string.Empty;

        // btnElevate
        btnElevate.DisplayStyle = ToolStripItemDisplayStyle.Text;
        btnElevate.ForeColor = Color.FromArgb(150, 160, 175);
        btnElevate.Text = "Ejecutar como administrador";
        btnElevate.ToolTipText = "Necesario para leer temperaturas y ventiladores.";

        // MainForm
        AutoScaleDimensions = new SizeF(96f, 96f);
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(20, 22, 26);
        ClientSize = new Size(1200, 760);
        MinimumSize = new Size(900, 600);
        Controls.Add(gridCards);
        Controls.Add(statusStrip);
        Controls.Add(headerPanel);
        DoubleBuffered = true;
        Font = new Font("Segoe UI", 9f);
        ForeColor = Color.FromArgb(230, 233, 238);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Analizador de rendimiento";

        ResumeLayout(false);
        PerformLayout();
    }
}
