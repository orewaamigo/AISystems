using System.Drawing;
using System.Windows.Forms;
using ImageAI.Models;
using ImageAI.Services;
using OpenCvSharp;

namespace ImageAI;

public class MainForm : Form
{
    private string _serverUrl   = "http://127.0.0.1:1234";
    private string _modelName   = "google/gemma-4-12b-qat";

    private LlmService            _llm       = null!;
    private readonly ImageProcessor _processor = new();
    private readonly Stack<Mat>     _history   = new();
    private Mat?                    _current;

    private PictureBox  _picture   = null!;
    private RichTextBox _chat      = null!;
    private TextBox     _input     = null!;
    private Button      _send      = null!;
    private Label       _sizeInfo  = null!;
    private Label       _modelLabel = null!;

    public MainForm()
    {
        _llm = new LlmService(_serverUrl, _modelName);
        BuildUi();
        AppendAi("Привет! Нажми «Открыть» и выбери изображение, потом просто пиши что с ним сделать.");
    }

    // ── UI ─────────────────────────────────────────────────────────────────────

    private void BuildUi()
    {
        Text          = "AI Image Chat";
        Size          = new System.Drawing.Size(1280, 760);
        MinimumSize   = new System.Drawing.Size(900, 600);
        BackColor     = Clr(28, 28, 28);
        ForeColor     = Color.White;
        Font          = new Font("Segoe UI", 10f);
        StartPosition = FormStartPosition.CenterScreen;

        var split = new SplitContainer
        {
            Dock          = DockStyle.Fill,
            SplitterWidth = 4,
            BackColor     = Clr(55, 55, 55),
        };
        // Shown — форма уже отображена, размеры финальны
        Shown += (_, _) => { split.SplitterDistance = split.Width * 45 / 100; };

        split.Panel1.Controls.Add(BuildLeft());
        split.Panel2.Controls.Add(BuildRight());
        Controls.Add(split);
    }

    private Panel BuildLeft()
    {
        _picture = new PictureBox
        {
            Dock      = DockStyle.Fill,
            SizeMode  = PictureBoxSizeMode.Zoom,
            BackColor = Clr(18, 18, 18),
        };

        _sizeInfo = new Label
        {
            Dock      = DockStyle.Bottom,
            Height    = 22,
            BackColor = Clr(37, 37, 38),
            ForeColor = Clr(140, 140, 140),
            Font      = new Font("Consolas", 8.5f),
            Text      = "  нет изображения",
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0),
        };

        // Кнопки тулбара
        var flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(4, 7, 4, 7),
            BackColor     = Clr(37, 37, 38),
        };
        flow.Controls.Add(Btn("📂 Открыть",   OnOpen));
        flow.Controls.Add(Btn("💾 Сохранить", OnSave));
        flow.Controls.Add(Btn("↩ Отменить",   OnUndo));
        flow.Controls.Add(Btn("🔍 Во весь",   OnShowFull));
        flow.Controls.Add(BuildAllCommandsBtn());

        var toolbar = new Panel { Dock = DockStyle.Bottom, Height = 44, BackColor = Clr(37, 37, 38) };
        toolbar.Controls.Add(flow);

        var panel = new Panel { Dock = DockStyle.Fill };
        // Порядок: Bottom-элементы добавляются ДО Fill
        panel.Controls.Add(toolbar);
        panel.Controls.Add(_sizeInfo);
        panel.Controls.Add(_picture);
        return panel;
    }

    private Panel BuildRight()
    {
        var settingsBtn = new Button
        {
            Text      = "⚙",
            Width     = 38,
            Dock      = DockStyle.Right,
            BackColor = Clr(0, 100, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 12f),
            Cursor    = Cursors.Hand,
        };
        settingsBtn.FlatAppearance.BorderSize = 0;
        settingsBtn.Click += OnSettings;

        var header = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 38,
            BackColor = Clr(0, 120, 212),
        };
        var headerLabel = new Label
        {
            Dock      = DockStyle.Fill,
            Text      = "   AI Image Chat",
            Font      = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleLeft,
        };
        header.Controls.Add(headerLabel);
        header.Controls.Add(settingsBtn);

        _modelLabel = new Label
        {
            Dock      = DockStyle.Top,
            Height    = 20,
            BackColor = Clr(20, 20, 20),
            ForeColor = Clr(110, 110, 110),
            Font      = new Font("Consolas", 8f),
            Text      = $"  {_modelName}  ·  {_serverUrl}",
            TextAlign = ContentAlignment.MiddleLeft,
        };
        var modelLbl = _modelLabel;

        _chat = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BackColor   = Clr(30, 30, 30),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.None,
            Font        = new Font("Consolas", 10.5f),
            WordWrap    = true,
            ScrollBars  = RichTextBoxScrollBars.Vertical,
        };

        _input = new TextBox
        {
            Dock            = DockStyle.Fill,
            BackColor       = Clr(50, 50, 50),
            ForeColor       = Color.White,
            BorderStyle     = BorderStyle.None,
            Font            = new Font("Segoe UI", 11f),
            PlaceholderText = "Напишите команду...",
        };
        _input.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter) { e.SuppressKeyPress = true; _ = SendAsync(); }
        };

        _send = new Button
        {
            Text      = "▶",
            Width     = 48,
            Dock      = DockStyle.Right,
            BackColor = Clr(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 13f, FontStyle.Bold),
            Cursor    = Cursors.Hand,
        };
        _send.FlatAppearance.BorderSize = 0;
        _send.Click += (_, _) => _ = SendAsync();

        var inputRow = new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 48,
            BackColor = Clr(40, 40, 40),
            Padding   = new Padding(10, 8, 0, 8),
        };
        inputRow.Controls.Add(_input);
        inputRow.Controls.Add(_send);

        // Панель быстрых команд (5 основных действий для задания)
        var cmdBar = BuildCommandBar();

        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Clr(30, 30, 30) };
        panel.Controls.Add(_chat);
        panel.Controls.Add(inputRow);
        panel.Controls.Add(cmdBar);
        panel.Controls.Add(modelLbl);
        panel.Controls.Add(header);
        return panel;
    }

    private Panel BuildCommandBar()
    {
        var flow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents  = false,
            Padding       = new Padding(6, 5, 6, 5),
            BackColor     = Clr(37, 37, 38),
        };

        // Перевернуть — выпадающее меню с выбором направления
        var flipBtn  = MakeCmdBtn("↔ Перевернуть ▾");
        var flipMenu = new ContextMenuStrip();
        StyleMenu(flipMenu);
        flipMenu.Items.Add("Горизонтально",   null, (_, _) => QuickSend("отразить горизонтально"));
        flipMenu.Items.Add("Вертикально",      null, (_, _) => QuickSend("отразить вертикально"));
        flipMenu.Items.Add("Оба направления",  null, (_, _) => QuickSend("отразить в обоих направлениях"));
        flipBtn.Click += (_, _) => flipMenu.Show(flipBtn, new System.Drawing.Point(0, flipBtn.Height));
        flow.Controls.Add(flipBtn);

        // Изменить размер — диалог ввода ширины
        var resizeBtn = MakeCmdBtn("📐 Изменить размер");
        resizeBtn.Click += (_, _) =>
        {
            string? w = AskInput("Введите новую ширину в пикселях:", "800");
            if (w != null) QuickSend($"изменить ширину до {w} пикселей");
        };
        flow.Controls.Add(resizeBtn);

        // Найти объекты — выпадающее меню по цветам
        var objBtn  = MakeCmdBtn("🎯 Объекты ▾");
        var objMenu = new ContextMenuStrip();
        StyleMenu(objMenu);
        objMenu.Items.Add("🔴  Красные объекты",  null, (_, _) => QuickSend("найти красные объекты"));
        objMenu.Items.Add("🟢  Зелёные объекты",  null, (_, _) => QuickSend("найти зелёные объекты"));
        objMenu.Items.Add("🔵  Синие объекты",    null, (_, _) => QuickSend("найти синие объекты"));
        objMenu.Items.Add("🟡  Контуры",           null, (_, _) => QuickSend("выделить контуры"));
        objMenu.Items.Add("🩷  Кожа",              null, (_, _) => QuickSend("найти области кожи"));
        objBtn.Click += (_, _) => objMenu.Show(objBtn, new System.Drawing.Point(0, objBtn.Height));
        flow.Controls.Add(objBtn);
        flow.Controls.Add(CmdBtn("⬛ Сделать ч/б", "сделать чёрно-белым"));
        flow.Controls.Add(CmdBtn("✏ Края",          "обнаружить края"));

        var bar = new Panel { Dock = DockStyle.Bottom, Height = 40, BackColor = Clr(37, 37, 38) };
        bar.Controls.Add(flow);
        return bar;
    }

    // Кнопка быстрой команды — сразу отправляет текст
    private Button CmdBtn(string label, string command)
    {
        var b = MakeCmdBtn(label);
        b.Click += (_, _) => QuickSend(command);
        return b;
    }

    // Базовый вид кнопки командной панели
    private Button MakeCmdBtn(string label)
    {
        var b = new Button
        {
            Text      = label,
            AutoSize  = false,
            Width     = 135,
            Height    = 28,
            BackColor = Clr(50, 50, 80),
            ForeColor = Clr(180, 210, 255),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 8.5f),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(2, 0, 2, 0),
        };
        b.FlatAppearance.BorderColor = Clr(70, 70, 110);
        return b;
    }

    private void QuickSend(string command)
    {
        _input.Text = command;
        _ = SendAsync();
    }

    // Мини-диалог с текстовым вводом
    private string? AskInput(string prompt, string defaultValue)
    {
        using var dlg = new Form
        {
            Text            = "Параметр",
            Size            = new System.Drawing.Size(340, 140),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false,
            MinimizeBox     = false,
            StartPosition   = FormStartPosition.CenterParent,
            BackColor       = Clr(37, 37, 38),
            ForeColor       = Color.White,
            Font            = new Font("Segoe UI", 10f),
        };

        var lbl = new Label
        {
            Text     = prompt,
            Dock     = DockStyle.Top,
            Height   = 30,
            Padding  = new Padding(10, 8, 0, 0),
            ForeColor = Clr(200, 200, 200),
        };

        var box = new TextBox
        {
            Text        = defaultValue,
            Dock        = DockStyle.Top,
            Height      = 28,
            BackColor   = Clr(55, 55, 55),
            ForeColor   = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            Font        = new Font("Consolas", 11f),
            Margin      = new Padding(10, 4, 10, 0),
        };
        box.SelectAll();

        var ok = new Button
        {
            Text         = "OK",
            DialogResult = DialogResult.OK,
            Width        = 80,
            Height       = 28,
            BackColor    = Clr(0, 120, 212),
            ForeColor    = Color.White,
            FlatStyle    = FlatStyle.Flat,
            Anchor       = AnchorStyles.Bottom | AnchorStyles.Right,
        };
        ok.FlatAppearance.BorderSize = 0;

        var btnRow = new FlowLayoutPanel
        {
            Dock          = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height        = 38,
            Padding       = new Padding(6, 4, 6, 4),
            BackColor     = Clr(30, 30, 30),
        };
        btnRow.Controls.Add(ok);

        dlg.Controls.Add(btnRow);
        dlg.Controls.Add(box);
        dlg.Controls.Add(lbl);
        dlg.AcceptButton = ok;

        return dlg.ShowDialog(this) == DialogResult.OK ? box.Text.Trim() : null;
    }

    private static void StyleMenu(ContextMenuStrip menu)
    {
        menu.BackColor = Color.FromArgb(50, 50, 80);
        menu.ForeColor = Color.FromArgb(180, 210, 255);
        menu.Font      = new Font("Segoe UI", 9.5f);
        menu.Renderer  = new ToolStripProfessionalRenderer(new DarkMenuColors());
    }

    private Button Btn(string text, Action onClick)
    {
        var b = new Button
        {
            Text      = text,
            Width     = 96,
            Height    = 30,
            BackColor = Clr(60, 60, 60),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Segoe UI", 9f),
            Cursor    = Cursors.Hand,
            Margin    = new Padding(2, 0, 2, 0),
        };
        b.FlatAppearance.BorderColor = Clr(80, 80, 80);
        b.Click += (_, _) => onClick();
        return b;
    }

    // ── Кнопка «Все возможности» ───────────────────────────────────────────────

    private Button BuildAllCommandsBtn()
    {
        var menu = new ContextMenuStrip();
        StyleMenu(menu);

        void Add(string title, string cmd) =>
            menu.Items.Add(title, null, (_, _) => QuickSend(cmd));

        void Sep() => menu.Items.Add(new ToolStripSeparator());

        Add("↔  Отразить горизонтально",       "отразить горизонтально");
        Add("↕  Отразить вертикально",          "отразить вертикально");
        Add("🔄  Повернуть на 90°",             "повернуть на 90 градусов");
        Add("🔄  Повернуть на 45°",             "повернуть на 45 градусов");
        Sep();
        Add("📐  Изменить размер до 1920px",    "изменить ширину до 1920 пикселей");
        Add("📐  Изменить размер до 1280px",    "изменить ширину до 1280 пикселей");
        Add("📐  Изменить размер до 800px",     "изменить ширину до 800 пикселей");
        Sep();
        Add("🌡  Тепловизор",                   "применить эффект тепловизора");
        Add("⬛  Чёрно-белое",                  "сделать чёрно-белым");
        Add("✏  Обнаружить края",              "обнаружить края");
        Add("🌫  Размытие (слабое)",            "размыть изображение, сила 3");
        Add("🌫  Размытие (сильное)",           "размыть изображение, сила 15");
        Add("☀  Увеличить яркость",            "увеличить яркость на 40");
        Add("🌑  Уменьшить яркость",           "уменьшить яркость на 40");
        Add("📈  Увеличить контраст",          "контраст 1.8");
        Sep();
        Add("🎨  Стиль: аниме",                "сделать в стиле аниме");
        Add("🎨  Стиль: мультфильм",           "мультяшный стиль");
        Add("🎨  Стиль: Disney",               "стиль disney");
        Add("✏  Стиль: эскиз / карандаш",     "карандашный набросок");
        Add("🖼  Стиль: масляная живопись",    "масляная живопись");
        Add("💧  Стиль: акварель",             "акварель");
        Sep();
        Add("🔴  Извлечь красный канал",       "выделить красный канал");
        Add("🟢  Извлечь зелёный канал",       "выделить зелёный канал");
        Add("🔵  Извлечь синий канал",         "выделить синий канал");
        Sep();
        Add("🔍  Найти контуры",               "найти и выделить контуры");
        Add("🟡  Найти красные объекты",       "найти красные объекты");
        Add("🟡  Найти зелёные объекты",       "найти зелёные объекты");

        var btn = Btn("📋 Команды ▾", () => { });
        btn.Click += (_, _) => menu.Show(btn, new System.Drawing.Point(0, btn.Height));
        return btn;
    }

    // ── Настройки ──────────────────────────────────────────────────────────────

    private void OnSettings(object? sender, EventArgs e)
    {
        using var dlg = new SettingsForm(_serverUrl, _modelName);
        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        _serverUrl  = dlg.ServerUrl;
        _modelName  = dlg.ModelName;
        _llm        = new LlmService(_serverUrl, _modelName);
        _modelLabel.Text = $"  {_modelName}  ·  {_serverUrl}";
        AppendAi($"Подключение изменено: {_serverUrl}");
    }

    // ── Тулбар ─────────────────────────────────────────────────────────────────

    private void OnOpen()
    {
        using var dlg = new OpenFileDialog
        {
            Title  = "Открыть изображение",
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp;*.tiff;*.webp|Все файлы|*.*",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var mat = Cv2.ImRead(dlg.FileName);
        if (mat.Empty()) { AppendAi("Не удалось открыть файл."); return; }

        _current?.Dispose();
        while (_history.Count > 0) _history.Pop().Dispose();
        _current = mat;
        _history.Push(_current.Clone());
        RefreshPicture();
        AppendAi($"Загрузил «{Path.GetFileName(dlg.FileName)}» ({_current.Width}×{_current.Height}). Что сделать?");
    }

    private void OnSave()
    {
        if (_current is null) { AppendAi("Сначала открой изображение."); return; }
        using var dlg = new SaveFileDialog
        {
            Title  = "Сохранить изображение",
            Filter = "JPEG|*.jpg|PNG|*.png|BMP|*.bmp",
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;
        Cv2.ImWrite(dlg.FileName, _current);
        AppendAi($"Сохранил: {Path.GetFileName(dlg.FileName)}");
    }

    private void OnUndo()
    {
        if (_history.Count <= 1) { AppendAi("Нечего отменять — это оригинал."); return; }
        _history.Pop().Dispose();
        _current?.Dispose();
        _current = _history.Peek().Clone();
        RefreshPicture();
        AppendAi($"Отменил. Размер: {_current.Width}×{_current.Height}.");
    }

    private void OnReset()
    {
        if (_current is null) return;
        while (_history.Count > 1) _history.Pop().Dispose();
        _current?.Dispose();
        _current = _history.Peek().Clone();
        RefreshPicture();
        AppendAi($"Вернул к оригиналу ({_current.Width}×{_current.Height}).");
    }

    private void OnShowFull()
    {
        if (_current is null) { AppendAi("Нет открытого изображения."); return; }
        var bmp = MatToBitmap(_current);
        var f   = new Form
        {
            Text        = "Просмотр",
            WindowState = FormWindowState.Maximized,
            BackColor   = Color.Black,
        };
        var pb = new PictureBox { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, Image = bmp };
        f.Controls.Add(pb);
        f.FormClosed += (_, _) => { pb.Image?.Dispose(); };
        f.Show(this);
    }

    // ── Чат ────────────────────────────────────────────────────────────────────

    private async Task SendAsync()
    {
        string text = _input.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _input.Clear();
        AppendUser(text);

        if (_current is null)
        {
            AppendAi("Сначала открой изображение кнопкой «Открыть».");
            return;
        }

        SetEnabled(false);
        AppendTyping();

        ImageCommand cmd;
        try
        {
            cmd = await _llm.ParseCommandAsync(text, _current.Width, _current.Height);
        }
        catch (HttpRequestException)
        {
            ReplaceTyping($"Не могу подключиться к серверу {_serverUrl}. Убедись, что сервер запущен.");
            SetEnabled(true);
            return;
        }
        catch (Exception ex)
        {
            ReplaceTyping($"Ошибка: {ex.Message}");
            SetEnabled(true);
            return;
        }

        ReplaceTyping(cmd.Reply ?? FallbackReply(cmd.Type));

        if (cmd.Type != CommandType.Unknown)
        {
            try
            {
                Mat result = await Task.Run(() => _processor.Process(_current, cmd));
                _history.Push(_current.Clone());
                _current.Dispose();
                _current = result;
                RefreshPicture();
                AppendStatus($"✓  Готово  {_current.Width}×{_current.Height}");
            }
            catch (Exception ex)
            {
                AppendStatus($"✗  Ошибка: {ex.Message}");
            }
        }

        SetEnabled(true);
        _input.Focus();
    }

    // ── Рендер чата ───────────────────────────────────────────────────────────

    private void AppendUser(string text)
    {
        if (_chat.TextLength > 0) _chat.AppendText("\n");
        Write("\n  Вы  ", Color.White, bold: true);
        Write(text, Clr(220, 220, 220));
        _chat.ScrollToCaret();
    }

    private void AppendAi(string text)
    {
        if (_chat.TextLength > 0) _chat.AppendText("\n");
        Write("\n  AI  ", Clr(0, 180, 255), bold: true);
        Write(text, Clr(180, 225, 245));
        _chat.ScrollToCaret();
    }

    private void AppendTyping()
    {
        if (_chat.TextLength > 0) _chat.AppendText("\n");
        Write("\n  AI  ", Clr(0, 180, 255), bold: true);
        Write("…", Clr(100, 100, 100));
        _chat.ScrollToCaret();
    }

    private void ReplaceTyping(string text)
    {
        int pos = _chat.Text.LastIndexOf('…');
        if (pos < 0) { AppendAi(text); return; }
        _chat.Select(pos, 1);
        _chat.SelectionColor = Clr(180, 225, 245);
        _chat.SelectionFont  = _chat.Font;
        _chat.SelectedText   = text;
        _chat.ScrollToCaret();
    }

    private void AppendStatus(string text)
    {
        _chat.AppendText("\n");
        Write("        " + text, text.StartsWith("✓") ? Clr(80, 200, 80) : Clr(220, 80, 80));
        _chat.ScrollToCaret();
    }

    private void Write(string text, Color color, bool bold = false)
    {
        _chat.SelectionStart  = _chat.TextLength;
        _chat.SelectionLength = 0;
        _chat.SelectionColor  = color;
        _chat.SelectionFont   = bold ? new Font(_chat.Font, FontStyle.Bold) : _chat.Font;
        _chat.AppendText(text);
        _chat.SelectionColor  = _chat.ForeColor;
    }

    // ── Вспомогательные ───────────────────────────────────────────────────────

    private void RefreshPicture()
    {
        if (_current is null) return;
        var old = _picture.Image;
        _picture.Image  = MatToBitmap(_current);
        _sizeInfo.Text  = $"  {_current.Width} × {_current.Height} пикс.";
        old?.Dispose();
    }

    private void SetEnabled(bool on)
    {
        _input.Enabled = on;
        _send.Enabled  = on;
    }

    private static Bitmap MatToBitmap(Mat mat)
    {
        Cv2.ImEncode(".bmp", mat, out byte[] buf);
        using var ms  = new MemoryStream(buf);
        using var tmp = new Bitmap(ms);
        return new Bitmap(tmp);   // копия разрывает зависимость от MemoryStream
    }

    private static Color Clr(int r, int g, int b) => Color.FromArgb(r, g, b);

    private static string FallbackReply(CommandType t) => t switch
    {
        CommandType.Rotate         => "Поворачиваю изображение.",
        CommandType.Flip           => "Отражаю изображение.",
        CommandType.Resize         => "Изменяю размер.",
        CommandType.ExtractChannel => "Извлекаю канал.",
        CommandType.DetectObjects  => "Ищу объекты.",
        CommandType.StyleTransfer  => "Применяю стиль.",
        CommandType.Grayscale      => "Перевожу в чёрно-белое.",
        CommandType.Blur           => "Размываю изображение.",
        CommandType.Adjust         => "Корректирую яркость и контраст.",
        CommandType.EdgeDetection  => "Обнаруживаю края.",
        CommandType.RemoveRegion   => "Удаляю область.",
        CommandType.Thermal        => "Применяю эффект тепловизора.",
        _                          => "Не смог распознать команду.",
    };

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _current?.Dispose();
        while (_history.Count > 0) _history.Pop().Dispose();
        base.OnFormClosed(e);
    }
}

// Тёмная цветовая схема для ContextMenuStrip
internal class DarkMenuColors : ProfessionalColorTable
{
    public override Color MenuItemSelected         => Color.FromArgb(70, 70, 110);
    public override Color MenuItemBorder           => Color.FromArgb(90, 90, 130);
    public override Color MenuBorder               => Color.FromArgb(70, 70, 110);
    public override Color ToolStripDropDownBackground => Color.FromArgb(50, 50, 80);
    public override Color ImageMarginGradientBegin => Color.FromArgb(50, 50, 80);
    public override Color ImageMarginGradientMiddle=> Color.FromArgb(50, 50, 80);
    public override Color ImageMarginGradientEnd   => Color.FromArgb(50, 50, 80);
}
