using System.Drawing;

namespace AasExcelToXml.Gui;

public sealed class AboutForm : Form
{
    private readonly Label _titleLabel = new();
    private readonly Label _versionLabel = new();
    private readonly Label _repoLabel = new();
    private readonly Label _copyrightLabel = new();
    private readonly Button _closeButton = new();

    public AboutForm()
    {
        Width = 420;
        Height = 260;
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        _titleLabel.Font = new Font(Font, FontStyle.Bold);
        _titleLabel.AutoSize = true;
        _versionLabel.AutoSize = true;
        _repoLabel.AutoSize = true;
        _copyrightLabel.AutoSize = true;
        _closeButton.Click += (_, _) => Close();

        Controls.Add(BuildLayout());
        ApplyLocalization();
    }

    private Control BuildLayout()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(20)
        };

        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(_titleLabel, 0, 0);
        layout.Controls.Add(_versionLabel, 0, 1);
        layout.Controls.Add(_repoLabel, 0, 2);
        layout.Controls.Add(_copyrightLabel, 0, 3);

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Bottom,
            Height = 40
        };
        buttonPanel.Controls.Add(_closeButton);

        var container = new Panel { Dock = DockStyle.Fill };
        container.Controls.Add(layout);
        container.Controls.Add(buttonPanel);
        return container;
    }

    private void ApplyLocalization()
    {
        Text = I18n.T("AboutTitle");
        _titleLabel.Text = I18n.T("AppTitle");
        _versionLabel.Text = $"{I18n.T("AboutVersionLabel")}: {Application.ProductVersion}";
        _repoLabel.Text = $"{I18n.T("AboutRepoLabel")}: AasExcelToXml";
        _copyrightLabel.Text = I18n.T("AboutCopyright");
        _closeButton.Text = I18n.T("SettingsCancel");
    }
}
