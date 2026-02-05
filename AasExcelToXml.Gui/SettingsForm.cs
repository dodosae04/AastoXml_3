using System.Drawing;
using AasExcelToXml.Core;

namespace AasExcelToXml.Gui;

public sealed class SettingsForm : Form
{
    private readonly ComboBox _languageComboBox = new();
    private readonly ComboBox _idSchemeComboBox = new();
    private readonly ComboBox _digitsModeComboBox = new();
    private readonly TextBox _defaultSheetTextBox = new();
    private readonly TextBox _documentIdSeedTextBox = new();
    private readonly TextBox _baseIriTextBox = new();
    private readonly CheckBox _includeAllDocsCheckBox = new();
    private readonly CheckBox _includeKoreanDescCheckBox = new();
    private readonly CheckBox _rememberFoldersCheckBox = new();
    private readonly CheckBox _openFolderCheckBox = new();
    private readonly CheckBox _openFileCheckBox = new();
    private readonly CheckBox _warningsOnlyCheckBox = new();
    private readonly Button _okButton = new();
    private readonly Button _cancelButton = new();

    private AppSettings _settings;

    public SettingsForm(AppSettings settings)
    {
        _settings = settings.Clone();

        Width = 520;
        Height = 520;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _languageComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _idSchemeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
        _digitsModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;

        _okButton.Click += OnOkClicked;
        _cancelButton.Click += (_, _) => Close();
        _idSchemeComboBox.SelectedIndexChanged += (_, _) => UpdateDigitsModeEnabled();

        Controls.Add(BuildLayout());
        ApplySettingsToControls();
        ApplyLocalization();
    }

    public AppSettings Settings => _settings;

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 12,
            Padding = new Padding(16),
            AutoSize = true
        };

        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        for (var i = 0; i < layout.RowCount; i++)
        {
            layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        }

        layout.Controls.Add(new Label { Name = "LanguageLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
        layout.Controls.Add(_languageComboBox, 1, 0);

        layout.Controls.Add(new Label { Name = "IdSchemeLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
        layout.Controls.Add(_idSchemeComboBox, 1, 1);

        layout.Controls.Add(new Label { Name = "DigitsModeLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
        layout.Controls.Add(_digitsModeComboBox, 1, 2);

        layout.Controls.Add(new Label { Name = "DefaultSheetLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
        layout.Controls.Add(_defaultSheetTextBox, 1, 3);

        layout.Controls.Add(new Label { Name = "DocumentIdSeedLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 4);
        layout.Controls.Add(_documentIdSeedTextBox, 1, 4);

        layout.Controls.Add(new Label { Name = "BaseIriLabel", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 5);
        layout.Controls.Add(_baseIriTextBox, 1, 5);

        layout.Controls.Add(_includeAllDocsCheckBox, 1, 6);
        layout.Controls.Add(_includeKoreanDescCheckBox, 1, 7);
        layout.Controls.Add(_rememberFoldersCheckBox, 1, 8);
        layout.Controls.Add(_openFolderCheckBox, 1, 9);
        layout.Controls.Add(_openFileCheckBox, 1, 10);
        layout.Controls.Add(_warningsOnlyCheckBox, 1, 11);

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(16),
            Height = 48
        };
        buttonPanel.Controls.Add(_okButton);
        buttonPanel.Controls.Add(_cancelButton);

        var container = new Panel { Dock = DockStyle.Fill };
        container.Controls.Add(layout);
        container.Controls.Add(buttonPanel);

        _languageComboBox.Dock = DockStyle.Fill;
        _idSchemeComboBox.Dock = DockStyle.Fill;
        _digitsModeComboBox.Dock = DockStyle.Fill;
        _defaultSheetTextBox.Dock = DockStyle.Fill;
        _documentIdSeedTextBox.Dock = DockStyle.Fill;
        _baseIriTextBox.Dock = DockStyle.Fill;

        return container;
    }

    private void ApplySettingsToControls()
    {
        RebuildOptionLists();

        _defaultSheetTextBox.Text = _settings.DefaultSheetName;
        _documentIdSeedTextBox.Text = _settings.DocumentIdSeed.ToString();
        _baseIriTextBox.Text = _settings.BaseIri;
        _includeAllDocsCheckBox.Checked = _settings.IncludeAllDocumentation;
        _includeKoreanDescCheckBox.Checked = _settings.IncludeKoreanDescription;
        _rememberFoldersCheckBox.Checked = _settings.RememberFolders;
        _openFolderCheckBox.Checked = _settings.OpenOutputFolderAfterConversion;
        _openFileCheckBox.Checked = _settings.OpenOutputFileAfterConversion;
        _warningsOnlyCheckBox.Checked = _settings.WriteWarningsOnlyWhenWarnings;

        SelectComboValue(_languageComboBox, _settings.Language);
        SelectComboValue(_idSchemeComboBox, _settings.IdScheme);
        SelectComboValue(_digitsModeComboBox, _settings.ExampleIriDigitsMode);
        UpdateDigitsModeEnabled();
    }

    private void ApplyLocalization()
    {
        Text = I18n.T("SettingsTitle");

        SetLabelText("LanguageLabel", I18n.T("SettingsLanguage"));
        SetLabelText("IdSchemeLabel", I18n.T("SettingsIdScheme"));
        SetLabelText("DigitsModeLabel", I18n.T("SettingsDigitsMode"));
        SetLabelText("DefaultSheetLabel", I18n.T("SettingsDefaultSheet"));
        SetLabelText("DocumentIdSeedLabel", I18n.T("SettingsDocumentIdSeed"));
        SetLabelText("BaseIriLabel", I18n.T("SettingsBaseIri"));

        _includeAllDocsCheckBox.Text = I18n.T("SettingsIncludeAllDocs");
        _includeKoreanDescCheckBox.Text = I18n.T("SettingsIncludeKoreanDesc");
        _rememberFoldersCheckBox.Text = I18n.T("SettingsRememberFolders");
        _openFolderCheckBox.Text = I18n.T("SettingsAfterOpenFolder");
        _openFileCheckBox.Text = I18n.T("SettingsAfterOpenFile");
        _warningsOnlyCheckBox.Text = I18n.T("SettingsWarningsOnly");
        _okButton.Text = I18n.T("SettingsOk");
        _cancelButton.Text = I18n.T("SettingsCancel");

        RebuildOptionLists();
    }

    private void RebuildOptionLists()
    {
        var selectedLanguage = GetSelectedValue<string>(_languageComboBox);
        _languageComboBox.DataSource = new List<OptionItem<string>>
        {
            new("ko-KR", I18n.T("LanguageKorean")),
            new("en", I18n.T("LanguageEnglish")),
            new("zh-Hans", I18n.T("LanguageChinese"))
        };
        _languageComboBox.DisplayMember = nameof(OptionItem<string>.Display);
        _languageComboBox.ValueMember = nameof(OptionItem<string>.Value);
        SelectComboValue(_languageComboBox, selectedLanguage ?? _settings.Language);

        var selectedIdScheme = GetSelectedValue<IdScheme>(_idSchemeComboBox);
        _idSchemeComboBox.DataSource = new List<OptionItem<IdScheme>>
        {
            new(IdScheme.ExampleIri, I18n.T("IdSchemeExampleIri")),
            new(IdScheme.UuidUrn, I18n.T("IdSchemeUuidUrn"))
        };
        _idSchemeComboBox.DisplayMember = nameof(OptionItem<IdScheme>.Display);
        _idSchemeComboBox.ValueMember = nameof(OptionItem<IdScheme>.Value);
        SelectComboValue(_idSchemeComboBox, selectedIdScheme ?? _settings.IdScheme);

        var selectedDigitsMode = GetSelectedValue<ExampleIriDigitsMode>(_digitsModeComboBox);
        _digitsModeComboBox.DataSource = new List<OptionItem<ExampleIriDigitsMode>>
        {
            new(ExampleIriDigitsMode.DeterministicHash, I18n.T("SettingsDigitsModeDeterministic")),
            new(ExampleIriDigitsMode.RandomSecure, I18n.T("SettingsDigitsModeRandom"))
        };
        _digitsModeComboBox.DisplayMember = nameof(OptionItem<ExampleIriDigitsMode>.Display);
        _digitsModeComboBox.ValueMember = nameof(OptionItem<ExampleIriDigitsMode>.Value);
        SelectComboValue(_digitsModeComboBox, selectedDigitsMode ?? _settings.ExampleIriDigitsMode);
    }

    private void UpdateDigitsModeEnabled()
    {
        var selected = GetSelectedValue<IdScheme>(_idSchemeComboBox);
        _digitsModeComboBox.Enabled = selected == IdScheme.ExampleIri;
    }

    private void OnOkClicked(object? sender, EventArgs e)
    {
        if (!long.TryParse(_documentIdSeedTextBox.Text.Trim(), out var seed))
        {
            MessageBox.Show(this, I18n.T("MessageInvalidDocumentIdSeed"), Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _settings.Language = GetSelectedValue<string>(_languageComboBox) ?? "ko-KR";
        _settings.IdScheme = GetSelectedValue<IdScheme>(_idSchemeComboBox);
        _settings.ExampleIriDigitsMode = GetSelectedValue<ExampleIriDigitsMode>(_digitsModeComboBox);
        _settings.DefaultSheetName = _defaultSheetTextBox.Text.Trim();
        _settings.DocumentIdSeed = seed;
        _settings.BaseIri = _baseIriTextBox.Text.Trim();
        _settings.IncludeAllDocumentation = _includeAllDocsCheckBox.Checked;
        _settings.IncludeKoreanDescription = _includeKoreanDescCheckBox.Checked;
        _settings.RememberFolders = _rememberFoldersCheckBox.Checked;
        _settings.OpenOutputFolderAfterConversion = _openFolderCheckBox.Checked;
        _settings.OpenOutputFileAfterConversion = _openFileCheckBox.Checked;
        _settings.WriteWarningsOnlyWhenWarnings = _warningsOnlyCheckBox.Checked;

        DialogResult = DialogResult.OK;
        Close();
    }

    private static void SelectComboValue<T>(ComboBox comboBox, T? value)
    {
        if (value is null)
        {
            return;
        }

        foreach (var item in comboBox.Items)
        {
            if (item is OptionItem<T> option && EqualityComparer<T>.Default.Equals(option.Value, value))
            {
                comboBox.SelectedItem = item;
                break;
            }
        }
    }

    private static T? GetSelectedValue<T>(ComboBox comboBox)
    {
        return comboBox.SelectedItem is OptionItem<T> option ? option.Value : default;
    }

    private void SetLabelText(string name, string value)
    {
        var label = Controls.Find(name, true).FirstOrDefault() as Label;
        if (label is not null)
        {
            label.Text = value;
        }
    }

    private sealed record OptionItem<T>(T Value, string Display);
}
