using System.Drawing;
using System.Windows.Forms;

namespace ImageAI;

public class SettingsForm : Form
{
    public string ServerUrl  { get; private set; }
    public string ModelName  { get; private set; }

    private readonly TextBox _urlBox;
    private readonly TextBox _modelBox;

    public SettingsForm(string url, string model)
    {
        ServerUrl = url;
        ModelName = model;

        Text          = "Настройки сервера";
        Size          = new System.Drawing.Size(480, 270);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox   = false;
        MinimizeBox   = false;
        StartPosition = FormStartPosition.CenterParent;
        BackColor     = Color.FromArgb(37, 37, 38);
        ForeColor     = Color.White;
        Font          = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 3,
            Padding     = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        _urlBox   = Field(url);
        _modelBox = Field(model);

        layout.Controls.Add(Label("URL сервера:"),  0, 0);
        layout.Controls.Add(_urlBox,                1, 0);
        layout.Controls.Add(Label("Модель:"),       0, 1);
        layout.Controls.Add(_modelBox,              1, 1);

        var hint = new Label
        {
            Text      = "Для удалённого доступа используй ngrok:\n  ngrok http 1234  →  вставь полученный https-адрес",
            ForeColor = Color.FromArgb(140, 140, 140),
            Font      = new Font("Segoe UI", 8.5f),
            Dock      = DockStyle.Fill,
            AutoSize  = false,
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 2);

        var btnOk = new Button
        {
            Text        = "Сохранить",
            DialogResult = DialogResult.OK,
            Width       = 100,
            Height      = 30,
            BackColor   = Color.FromArgb(0, 120, 212),
            ForeColor   = Color.White,
            FlatStyle   = FlatStyle.Flat,
            Anchor      = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            ServerUrl = _urlBox.Text.Trim().TrimEnd('/');
            ModelName = _modelBox.Text.Trim();
        };

        var btnCancel = new Button
        {
            Text         = "Отмена",
            DialogResult = DialogResult.Cancel,
            Width        = 80,
            Height       = 30,
            BackColor    = Color.FromArgb(60, 60, 60),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat,
            Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnCancel.FlatAppearance.BorderSize = 0;

        var btnPanel = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height        = 44,
            Padding       = new Padding(8, 6, 8, 6),
            BackColor     = Color.FromArgb(30, 30, 30),
        };
        btnPanel.Controls.Add(btnOk);
        btnPanel.Controls.Add(btnCancel);

        Controls.Add(layout);
        Controls.Add(btnPanel);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private static TextBox Field(string value) => new()
    {
        Text      = value,
        Dock      = DockStyle.Fill,
        BackColor = Color.FromArgb(50, 50, 50),
        ForeColor = Color.White,
        BorderStyle = BorderStyle.FixedSingle,
        Font      = new Font("Consolas", 9.5f),
        Margin    = new Padding(0, 0, 0, 8),
    };

    private static Label Label(string text) => new()
    {
        Text      = text,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock      = DockStyle.Fill,
        ForeColor = Color.FromArgb(180, 180, 180),
    };
}
