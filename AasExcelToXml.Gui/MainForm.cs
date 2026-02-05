using System.Diagnostics;
using System.Drawing;
using AasExcelToXml.Core;

namespace AasExcelToXml.Gui;

public class MainForm : Form
{
    private readonly ListView _queueListView = new();
    private readonly TextBox _outputFolderTextBox = new();
    private readonly TextBox _sheetNameTextBox = new();
    private readonly RadioButton _aas2RadioButton = new();
    private readonly RadioButton _aas3RadioButton = new();
    private readonly Button _addFilesButton = new();
    private readonly Button _removeFilesButton = new();
    private readonly Button _clearFilesButton = new();
    private readonly Button _outputFolderButton = new();
    private readonly Button _convertButton = new();
    private readonly Button _cancelButton = new();
    private readonly Button _openWarningsButton = new();
    private readonly ProgressBar _progressBar = new();
    private readonly TextBox _logTextBox = new();
    private readonly Label _dropHintLabel = new();
    private readonly ToolStripMenuItem _settingsMenuItem = new();
    private readonly ToolStripMenuItem _aboutMenuItem = new();

    private AppSettings _settings;
    private bool _isConverting;
    private bool _cancelRequested;
    private string? _lastWarningsPath;
    private string? _lastOutputPath;

    public MainForm(AppSettings settings)
    {
        _settings = settings;

        Text = I18n.T("AppTitle");
        Width = 920;
        Height = 640;
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Application;
        AllowDrop = true;

        _queueListView.View = View.Details;
        _queueListView.FullRowSelect = true;
        _queueListView.MultiSelect = true;
        _queueListView.AllowDrop = true;
        _queueListView.Dock = DockStyle.Fill;
        _queueListView.Columns.Add("File", 520, HorizontalAlignment.Left);
        _queueListView.Columns.Add("Status", 160, HorizontalAlignment.Left);

        _outputFolderTextBox.ReadOnly = true;
        _sheetNameTextBox.Text = _settings.DefaultSheetName;

        _addFilesButton.Click += (_, _) => OnAddFiles();
        _removeFilesButton.Click += (_, _) => OnRemoveSelected();
        _clearFilesButton.Click += (_, _) => OnClearQueue();
        _outputFolderButton.Click += (_, _) => OnSelectOutputFolder();
        _convertButton.Click += OnConvert;
        _cancelButton.Click += (_, _) => OnCancel();
        _openWarningsButton.Click += (_, _) => OnOpenWarnings();

        _settingsMenuItem.Click += (_, _) => OpenSettings();
        _aboutMenuItem.Click += (_, _) => OpenAbout();

        DragEnter += OnDragEnter;
        DragDrop += OnDragDrop;
        _queueListView.DragEnter += OnDragEnter;
        _queueListView.DragDrop += OnDragDrop;

        _logTextBox.Multiline = true;
        _logTextBox.ScrollBars = ScrollBars.Vertical;
        _logTextBox.ReadOnly = true;

        Controls.Add(BuildLayout());
        ApplyLocalization();
        ApplySettings();
        FormClosing += (_, _) => PersistSettings();
    }

    private Control BuildLayout()
    {
        var menuStrip = new MenuStrip();
        menuStrip.Items.Add(_settingsMenuItem);
        menuStrip.Items.Add(_aboutMenuItem);
        MainMenuStrip = menuStrip;
        Controls.Add(menuStrip);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 150));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));

        layout.Controls.Add(BuildQueueSection(), 0, 0);
        layout.Controls.Add(BuildOptionsSection(), 0, 1);
        layout.Controls.Add(BuildActionSection(), 0, 2);

        layout.Dock = DockStyle.Fill;
        return layout;
    }

    private Control BuildQueueSection()
    {
        var group = new GroupBox { Dock = DockStyle.Fill };
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 2,
            Padding = new Padding(10)
        };

        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _dropHintLabel.Dock = DockStyle.Fill;
        _dropHintLabel.TextAlign = ContentAlignment.MiddleLeft;

        panel.Controls.Add(_dropHintLabel, 0, 0);
        panel.SetColumnSpan(_dropHintLabel, 2);

        panel.Controls.Add(_queueListView, 0, 1);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(0, 4, 0, 0)
        };
        buttonPanel.Controls.Add(_addFilesButton);
        buttonPanel.Controls.Add(_removeFilesButton);
        buttonPanel.Controls.Add(_clearFilesButton);

        panel.Controls.Add(buttonPanel, 1, 1);

        group.Controls.Add(panel);
        return group;
    }

    private Control BuildOptionsSection()
    {
        var group = new GroupBox { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 3,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        for (var i = 0; i < 3; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        }

        layout.Controls.Add(new Label { Name = "OutputFolderLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(_outputFolderTextBox, 1, 0);
        layout.Controls.Add(_outputFolderButton, 2, 0);

        layout.Controls.Add(new Label { Name = "SheetNameLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(_sheetNameTextBox, 1, 1);
        layout.SetColumnSpan(_sheetNameTextBox, 2);

        layout.Controls.Add(new Label { Name = "VersionLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        var versionPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };
        versionPanel.Controls.Add(_aas2RadioButton);
        versionPanel.Controls.Add(_aas3RadioButton);
        layout.Controls.Add(versionPanel, 1, 2);
        layout.SetColumnSpan(versionPanel, 2);

        _outputFolderTextBox.Dock = DockStyle.Fill;
        _sheetNameTextBox.Dock = DockStyle.Fill;

        group.Controls.Add(layout);
        return group;
    }

    private Control BuildActionSection()
    {
        var group = new GroupBox { Dock = DockStyle.Fill };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 4,
            Padding = new Padding(10)
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight
        };
        buttonPanel.Controls.Add(_convertButton);
        buttonPanel.Controls.Add(_cancelButton);
        buttonPanel.Controls.Add(_openWarningsButton);

        layout.Controls.Add(buttonPanel, 0, 0);
        layout.SetColumnSpan(buttonPanel, 2);

        _progressBar.Dock = DockStyle.Fill;
        layout.Controls.Add(_progressBar, 0, 1);
        layout.SetColumnSpan(_progressBar, 2);

        var logLabel = new Label { Name = "LogLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
        layout.Controls.Add(logLabel, 0, 2);
        layout.SetColumnSpan(logLabel, 2);
        layout.Controls.Add(_logTextBox, 0, 3);
        layout.SetColumnSpan(_logTextBox, 2);

        _logTextBox.Dock = DockStyle.Fill;

        group.Controls.Add(layout);
        return group;
    }

    private void ApplyLocalization()
    {
        Text = I18n.T("AppTitle");
        _settingsMenuItem.Text = I18n.T("MenuSettings");
        _aboutMenuItem.Text = I18n.T("MenuAbout");
        _dropHintLabel.Text = I18n.T("DropHint");

        _addFilesButton.Text = I18n.T("ButtonAddFiles");
        _removeFilesButton.Text = I18n.T("ButtonRemoveSelected");
        _clearFilesButton.Text = I18n.T("ButtonClearQueue");
        _outputFolderButton.Text = I18n.T("ButtonBrowse");
        _convertButton.Text = I18n.T("ButtonConvert");
        _cancelButton.Text = I18n.T("ButtonCancel");
        _openWarningsButton.Text = I18n.T("ButtonOpenWarnings");

        _aas2RadioButton.Text = I18n.T("RadioAas2");
        _aas3RadioButton.Text = I18n.T("RadioAas3");

        SetLabelText("OutputFolderLabel", I18n.T("LabelOutputFolder"));
        SetLabelText("SheetNameLabel", I18n.T("LabelSheetName"));
        SetLabelText("VersionLabel", I18n.T("LabelVersion"));
        SetLabelText("LogLabel", I18n.T("LabelLog"));

        _queueListView.Columns[0].Text = I18n.T("ColumnFile");
        _queueListView.Columns[1].Text = I18n.T("ColumnStatus");

        ApplyGroupTitles();
    }

    private void ApplyGroupTitles()
    {
        foreach (var group in Controls.OfType<TableLayoutPanel>()
                     .SelectMany(panel => panel.Controls.OfType<GroupBox>()))
        {
            if (group.Controls.Contains(_queueListView))
            {
                group.Text = I18n.T("LabelQueue");
            }
            else if (group.Controls.Contains(_outputFolderTextBox))
            {
                group.Text = I18n.T("LabelOptions");
            }
            else
            {
                group.Text = I18n.T("LabelProgress");
            }
        }
    }

    private void ApplySettings()
    {
        _aas3RadioButton.Checked = _settings.DefaultVersion == AasVersion.Aas3_0;
        _aas2RadioButton.Checked = _settings.DefaultVersion == AasVersion.Aas2_0;
        _sheetNameTextBox.Text = string.IsNullOrWhiteSpace(_settings.DefaultSheetName)
            ? "사양시트"
            : _settings.DefaultSheetName;

        if (_settings.RememberFolders && !string.IsNullOrWhiteSpace(_settings.LastOutputFolder))
        {
            _outputFolderTextBox.Text = _settings.LastOutputFolder;
        }

        _openWarningsButton.Enabled = false;
        UpdateSheetNameState();
    }

    private void PersistSettings()
    {
        _settings.DefaultSheetName = _sheetNameTextBox.Text.Trim();
        _settings.DefaultVersion = GetSelectedVersion();
        if (_settings.RememberFolders)
        {
            _settings.LastOutputFolder = _outputFolderTextBox.Text.Trim();
        }

        SettingsStore.Save(_settings);
    }

    private void OnAddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Excel/CSV (*.xlsx;*.csv)|*.xlsx;*.csv|All files (*.*)|*.*",
            Multiselect = true
        };

        if (!string.IsNullOrWhiteSpace(_settings.LastInputFolder))
        {
            dialog.InitialDirectory = _settings.LastInputFolder;
        }

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        AddFiles(dialog.FileNames);
    }

    private void AddFiles(IEnumerable<string> files)
    {
        var added = false;
        foreach (var file in files)
        {
            if (!IsSupportedFile(file))
            {
                continue;
            }

            if (_queueListView.Items.Cast<ListViewItem>().Any(item => string.Equals((string)item.Tag, file, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var item = new ListViewItem(Path.GetFileName(file));
            item.SubItems.Add(I18n.T("StatusPending"));
            item.Tag = file;
            _queueListView.Items.Add(item);

            AppendLog(string.Format(I18n.T("LogAddedFiles"), file));
            added = true;
        }

        if (added)
        {
            var first = files.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first))
            {
                if (_settings.RememberFolders)
                {
                    _settings.LastInputFolder = Path.GetDirectoryName(first);
                }

                EnsureOutputFolder(first);
            }
        }

        UpdateSheetNameState();
    }

    private void OnRemoveSelected()
    {
        var selected = _queueListView.SelectedItems.Cast<ListViewItem>().ToList();
        foreach (var item in selected)
        {
            _queueListView.Items.Remove(item);
            AppendLog(string.Format(I18n.T("LogRemovedFiles"), item.Tag));
        }

        UpdateSheetNameState();
    }

    private void OnClearQueue()
    {
        _queueListView.Items.Clear();
        AppendLog(I18n.T("LogClearedQueue"));
        UpdateSheetNameState();
    }

    private void OnSelectOutputFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            SelectedPath = _outputFolderTextBox.Text
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _outputFolderTextBox.Text = dialog.SelectedPath;
        AppendLog(string.Format(I18n.T("LogSelectedOutputFolder"), dialog.SelectedPath));
        if (_settings.RememberFolders)
        {
            _settings.LastOutputFolder = dialog.SelectedPath;
        }
    }

    private async void OnConvert(object? sender, EventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        if (_queueListView.Items.Count == 0)
        {
            AppendLog(I18n.T("MessageNoFiles"));
            return;
        }

        var outputFolder = _outputFolderTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            AppendLog(I18n.T("MessageInvalidOutputFolder"));
            return;
        }

        Directory.CreateDirectory(outputFolder);
        _lastWarningsPath = null;
        _lastOutputPath = null;
        _cancelRequested = false;
        SetConvertingState(true);
        AppendLog(I18n.T("LogStartConversion"));

        var options = BuildOptions();
        var version = GetSelectedVersion();
        var warningsTotal = 0;

        foreach (ListViewItem item in _queueListView.Items)
        {
            if (_cancelRequested)
            {
                item.SubItems[1].Text = I18n.T("StatusCanceled");
                continue;
            }

            var inputPath = (string)item.Tag;
            var outputPath = Path.Combine(outputFolder, Path.GetFileNameWithoutExtension(inputPath) + GetDefaultExtension(version));
            _lastOutputPath = outputPath;

            AppendLog(string.Format(I18n.T("LogConvertingFile"), inputPath));
            try
            {
                var sheetName = Path.GetExtension(inputPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
                    ? _sheetNameTextBox.Text.Trim()
                    : null;

                var result = await Task.Run(() => Converter.Convert(inputPath, outputPath, sheetName, options));
                var warningCount = result.Diagnostics.GetTotalCount();
                warningsTotal += warningCount;
                _lastWarningsPath = result.WarningsPath;

                if (_settings.WriteWarningsOnlyWhenWarnings && warningCount == 0 && File.Exists(result.WarningsPath))
                {
                    File.Delete(result.WarningsPath);
                    _lastWarningsPath = null;
                }

                if (warningCount > 0)
                {
                    AppendLog(string.Format(I18n.T("LogWarnings"), warningCount));
                }

                item.SubItems[1].Text = I18n.T("StatusCompleted");
                AppendLog(string.Format(I18n.T("LogConversionCompleted"), outputPath));
            }
            catch (Exception ex)
            {
                item.SubItems[1].Text = I18n.T("StatusFailed");
                AppendLog(string.Format(I18n.T("LogConversionFailed"), ex.Message));
            }
        }

        if (_cancelRequested)
        {
            AppendLog(I18n.T("LogConversionCanceled"));
        }

        _openWarningsButton.Enabled = !string.IsNullOrWhiteSpace(_lastWarningsPath) && File.Exists(_lastWarningsPath);

        if (warningsTotal == 0)
        {
            AppendLog(I18n.T("LogNoWarnings"));
        }

        if (!_cancelRequested)
        {
            MessageBox.Show(this, I18n.T("MessageConversionComplete"), Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        if (_settings.OpenOutputFolderAfterConversion)
        {
            OpenPath(outputFolder);
        }

        if (_settings.OpenOutputFileAfterConversion && !string.IsNullOrWhiteSpace(_lastOutputPath))
        {
            OpenPath(_lastOutputPath);
        }

        SetConvertingState(false);
    }

    private void OnCancel()
    {
        if (!_isConverting)
        {
            return;
        }

        _cancelRequested = true;
        _cancelButton.Enabled = false;
        AppendLog(I18n.T("LogCancelRequested"));
    }

    private void OnOpenWarnings()
    {
        if (!string.IsNullOrWhiteSpace(_lastWarningsPath) && File.Exists(_lastWarningsPath))
        {
            OpenPath(_lastWarningsPath);
        }
    }

    private void OpenSettings()
    {
        using var dialog = new SettingsForm(_settings);
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _settings = dialog.Settings.Clone();
        SettingsStore.Save(_settings);
        I18n.SetCulture(_settings.Language);
        ApplyLocalization();
        ApplySettings();
    }

    private void OpenAbout()
    {
        using var dialog = new AboutForm();
        dialog.ShowDialog(this);
    }

    private void SetConvertingState(bool isConverting)
    {
        _isConverting = isConverting;
        _addFilesButton.Enabled = !isConverting;
        _removeFilesButton.Enabled = !isConverting;
        _clearFilesButton.Enabled = !isConverting;
        _outputFolderButton.Enabled = !isConverting;
        _convertButton.Enabled = !isConverting;
        _cancelButton.Enabled = isConverting;
        _sheetNameTextBox.Enabled = !isConverting && ShouldEnableSheetName();
        _aas2RadioButton.Enabled = !isConverting;
        _aas3RadioButton.Enabled = !isConverting;
        _openWarningsButton.Enabled = !isConverting && !string.IsNullOrWhiteSpace(_lastWarningsPath) && File.Exists(_lastWarningsPath);
        _progressBar.Style = isConverting ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
    }

    private void UpdateSheetNameState()
    {
        _sheetNameTextBox.Enabled = ShouldEnableSheetName();
    }

    private bool ShouldEnableSheetName()
    {
        if (_queueListView.Items.Count == 0)
        {
            return true;
        }

        return _queueListView.Items.Cast<ListViewItem>()
            .Select(item => (string)item.Tag)
            .Any(path => Path.GetExtension(path).Equals(".xlsx", StringComparison.OrdinalIgnoreCase));
    }

    private void AppendLog(string message)
    {
        _logTextBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void EnsureOutputFolder(string firstFile)
    {
        if (!string.IsNullOrWhiteSpace(_outputFolderTextBox.Text))
        {
            return;
        }

        var folder = _settings.RememberFolders && !string.IsNullOrWhiteSpace(_settings.LastOutputFolder)
            ? _settings.LastOutputFolder
            : Path.GetDirectoryName(firstFile);

        if (!string.IsNullOrWhiteSpace(folder))
        {
            _outputFolderTextBox.Text = folder;
        }
    }

    private ConvertOptions BuildOptions()
    {
        return new ConvertOptions
        {
            Version = GetSelectedVersion(),
            IncludeAllDocumentation = _settings.IncludeAllDocumentation,
            IncludeKoreanDescription = _settings.IncludeKoreanDescription,
            DocumentIdSeed = _settings.DocumentIdSeed,
            BaseIri = _settings.BaseIri,
            IdScheme = _settings.IdScheme,
            ExampleIriDigitsMode = _settings.ExampleIriDigitsMode
        };
    }

    private AasVersion GetSelectedVersion()
    {
        return _aas3RadioButton.Checked ? AasVersion.Aas3_0 : AasVersion.Aas2_0;
    }

    private static bool IsSupportedFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".xlsx", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".csv", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDefaultExtension(AasVersion version)
    {
        return version == AasVersion.Aas3_0 ? ".aas3.xml" : ".aas2.xml";
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
        {
            e.Effect = DragDropEffects.Copy;
        }
    }

    private void OnDragDrop(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetData(DataFormats.FileDrop) is string[] files)
        {
            AddFiles(files);
        }
    }

    private void SetLabelText(string name, string value)
    {
        var label = Controls.Find(name, true).FirstOrDefault() as Label;
        if (label is not null)
        {
            label.Text = value;
        }
    }

    private static void OpenPath(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            UseShellExecute = true
        });
    }
}
