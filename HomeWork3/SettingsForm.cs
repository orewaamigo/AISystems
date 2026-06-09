using System.Drawing;
using System.Windows.Forms;

namespace ImageAI;

public class SettingsForm : Form
{
    public string ServerUrl  { get; private set; }
    public string ModelName  { get; private set; }

    private readonly ComboBox _urlBox;
    private readonly ComboBox _modelBox;

    private static readonly string[] ServerPresets =
    [
        "http://127.0.0.1:1234",   // LM Studio
        "http://127.0.0.1:11434",  // Ollama
        "http://127.0.0.1:8080",
        "https://api.openai.com/v1",
    ];

    private static readonly string[] ModelPresets =
    [
        "google/gemma-4-12b-qat",
        "google/gemma-3-12b-it",
        "meta-llama/llama-3.2-11b-vision-instruct",
        "microsoft/phi-4-multimodal-instruct",
        "qwen/qwen2.5-vl-7b-instruct",
        "mistralai/mistral-small-3.1-24b-instruct",
        "llava-hf/llava-1.5-7b-hf",
    ];

    public SettingsForm(string url, string model)
    {
        ServerUrl = url;
        ModelName = model;

        Text            = "Настройки";
        Size            = new System.Drawing.Size(500, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        BackColor       = Color.FromArgb(37, 37, 38);
        ForeColor       = Color.White;
        Font            = new Font("Segoe UI", 10f);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 3,
            Padding     = new Padding(16),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _urlBox   = Combo(ServerPresets, url);
        _modelBox = Combo(ModelPresets, model);

        layout.Controls.Add(Lbl("Сервер:"),   0, 0);
        layout.Controls.Add(_urlBox,          1, 0);
        layout.Controls.Add(Lbl("Модель:"),   0, 1);
        layout.Controls.Add(_modelBox,        1, 1);

        var hint = new Label
        {
            Text      = "Поддерживаются модели, совместимые с OpenAI API. URL должен указывать на корневой адрес сервера без слэша на конце.",
            ForeColor = Color.FromArgb(120, 120, 120),
            Dock      = DockStyle.Fill,
        };
        layout.Controls.Add(hint, 0, 2);
        layout.SetColumnSpan(hint, 2);

        var btnOk = new Button
        {
            Text         = "Сохранить",
            DialogResult = DialogResult.OK,
            Width        = 100,
            Height       = 30,
            BackColor    = Color.FromArgb(0, 120, 212),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat,
            Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        btnOk.FlatAppearance.BorderSize = 0;
        btnOk.Click += (_, _) =>
        {
            ServerUrl = (_urlBox.Text ?? "").Trim().TrimEnd('/');
            ModelName = (_modelBox.Text ?? "").Trim();
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

    private static ComboBox Combo(string[] presets, string current)
    {
        var c = new ComboBox
        {
            Text          = current,
            Dock          = DockStyle.Fill,
            BackColor     = Color.FromArgb(50, 50, 50),
            ForeColor     = Color.White,
            FlatStyle     = FlatStyle.Flat,
            Font          = new Font("Consolas", 9.5f),
            Margin        = new Padding(0, 0, 0, 8),
            DropDownStyle = ComboBoxStyle.DropDown,
        };
        foreach (var s in presets) c.Items.Add(s);
        return c;
    }

    private static Label Lbl(string text) => new()
    {
        Text      = text,
        TextAlign = ContentAlignment.MiddleLeft,
        Dock      = DockStyle.Fill,
        ForeColor = Color.FromArgb(180, 180, 180),
    };
}
