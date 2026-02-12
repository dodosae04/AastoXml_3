using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AasExcelToXml.Core;
using AasExcelToXml.Wpf.Models;
using AasExcelToXml.Wpf.Services;
using AasExcelToXml.Wpf.ViewModels;

namespace AasExcelToXml.Wpf;

// [역할] 사용자가 입력 파일/시트/옵션을 선택해 변환을 실행하는 WPF 메인 화면의 이벤트를 처리한다.
// [입력] UI 컨트롤 값(파일 목록, 시트명, 출력 폴더, 옵션 체크 상태).
// [출력] XML 파일 생성 요청, 로그/경고 표시, 사용자 설정 저장.
// [수정 포인트] Core 옵션 연결(예: 카테고리 상수, 시트 선택, warnings 경로)은 ConvertButton_Click 계열에서 조정한다.
public partial class MainWindow : Window
{
    private readonly ObservableCollection<InputFileItem> _files = new();
    private readonly ObservableCollection<string> _logs = new();
    private readonly ObservableCollection<string> _sheetNames = new();
    private readonly ObservableCollection<string> _externalRefSheetNames = new();
    private AppSettings _settings = new();
    private CancellationTokenSource? _cts;
    private int _warningsCount;
    private string? _lastWarningsPath;
    private string? _lastOutputFolder;
    private string? _lastOutputFile;
    private bool _isConverting;
    private readonly List<string> _sessionWarnings = new();

    public MainWindow()
    {
        InitializeComponent();
        FilesListView.ItemsSource = _files;
        LogListBox.ItemsSource = _logs;
        SheetNameComboBox.ItemsSource = _sheetNames;
        ExternalRefSheetComboBox.ItemsSource = _externalRefSheetNames;
        LoadSettings();
    }

    /// <summary>
    /// 저장된 사용자 설정을 로드하고 언어/출력 경로를 초기화한다.
    /// </summary>
    /// <remarks>
    /// 설정 저장소 위치나 키 구조를 바꾸면 SettingsService와 함께 수정해야 한다.
    /// </remarks>
    private void LoadSettings()
    {
        _settings = SettingsService.Load();
        LocalizationService.Instance.SetCulture(_settings.Language);
        if (_settings.RememberLastFolders && !string.IsNullOrWhiteSpace(_settings.LastOutputFolder))
        {
            OutputFolderTextBox.Text = _settings.LastOutputFolder;
        }
    }

    private void SaveSettings() => SettingsService.Save(_settings);

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() == true)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (!File.Exists(path) || _files.Any(f => string.Equals(f.Path, path, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _files.Add(new InputFileItem(path));
        }

        if (FilesListView.SelectedItem is null && _files.Count > 0)
        {
            FilesListView.SelectedIndex = 0;
        }

        RefreshSheetNamesForSelection();
    }

    private void RemoveButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = FilesListView.SelectedItems.Cast<InputFileItem>().ToList();
        foreach (var item in selected)
        {
            _files.Remove(item);
        }

        RefreshSheetNamesForSelection();
    }

    private void ClearButton_Click(object sender, RoutedEventArgs e)
    {
        _files.Clear();
        _sheetNames.Clear();
        SheetNameComboBox.Text = string.Empty;
    }

    private void DropZone_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void DropZone_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            AddFiles((string[])e.Data.GetData(DataFormats.FileDrop));
        }
    }

    private void FilesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshSheetNamesForSelection();
    }

    private void RefreshSheetNamesForSelection()
    {
        var selected = FilesListView.SelectedItem as InputFileItem;
        if (selected is null)
        {
            return;
        }

        var names = ExcelSpecReader.GetWorksheetNames(selected.Path);
        _sheetNames.Clear();
        foreach (var name in names)
        {
            _sheetNames.Add(name);
        }

        if (_sheetNames.Count == 0)
        {
            return;
        }

        var preferred = _sheetNames.FirstOrDefault(name => string.Equals(name, "사양시트", StringComparison.OrdinalIgnoreCase))
            ?? _sheetNames.First();
        SheetNameComboBox.Text = preferred;
        SheetNameComboBox.SelectedItem = preferred;
    }

    private void ExternalRefBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Excel files (*.xlsx)|*.xlsx|All files (*.*)|*.*",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var selectedPaths = dialog.FileNames?.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray() ?? Array.Empty<string>();
        if (selectedPaths.Length == 0)
        {
            return;
        }

        var joined = string.Join(";", selectedPaths);
        ExternalRefFileTextBox.Text = joined;
        RefreshExternalReferenceSheets(joined);
    }

    private void ExternalRefClearButton_Click(object sender, RoutedEventArgs e)
    {
        ExternalRefFileTextBox.Text = string.Empty;
        _externalRefSheetNames.Clear();
        ExternalRefSheetComboBox.Text = string.Empty;
    }

    private void RefreshExternalReferenceSheets(string path)
    {
        var filePaths = (path ?? string.Empty)
            .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _externalRefSheetNames.Clear();
        _externalRefSheetNames.Add("(전체)");

        if (filePaths.Count == 1)
        {
            foreach (var name in ExcelSpecReader.GetWorksheetNames(filePaths[0]))
            {
                _externalRefSheetNames.Add(name);
            }
        }

        ExternalRefSheetComboBox.Text = "(전체)";
        ExternalRefSheetComboBox.SelectedItem = "(전체)";
    }

    private void OutputBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Select output folder" };
        if (dialog.ShowDialog() == true)
        {
            OutputFolderTextBox.Text = dialog.FolderName;
        }
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConverting)
        {
            return;
        }

        if (_files.Count == 0)
        {
            AddLog("입력 파일이 없습니다.");
            return;
        }

        var sheetName = SheetNameComboBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(sheetName))
        {
            AddLog("시트 이름을 선택하거나 입력하세요.");
            return;
        }

        var externalRefPath = ExternalRefFileTextBox.Text?.Trim();
        var externalRefSheet = ExternalRefSheetComboBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(externalRefPath) || string.IsNullOrWhiteSpace(externalRefSheet))
        {
            externalRefPath = null;
            externalRefSheet = null;
        }

        var outputFolder = OutputFolderTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(outputFolder))
        {
            AddLog("출력 폴더를 선택하세요.");
            return;
        }

        Directory.CreateDirectory(outputFolder);

        _warningsCount = 0;
        _sessionWarnings.Clear();
        UpdateWarningsDisplay();
        _lastWarningsPath = null;
        _lastOutputFolder = outputFolder;
        _lastOutputFile = null;

        foreach (var file in _files)
        {
            file.Status = string.Empty;
        }

        _cts = new CancellationTokenSource();
        _isConverting = true;
        SetUiEnabled(false);
        ProgressBar.Value = 0;
        ProgressBar.Maximum = _files.Count;

        _settings.LastOutputFolder = outputFolder;
        if (_settings.RememberLastFolders)
        {
            _settings.LastInputFolder = Path.GetDirectoryName(_files[0].Path);
        }
        SaveSettings();

        var targetVersion = Aas3Radio.IsChecked == true ? AasVersion.Aas3_0 : AasVersion.Aas2_0;

        for (var i = 0; i < _files.Count; i++)
        {
            if (_cts.IsCancellationRequested)
            {
                AddLog("변환 취소 요청됨. 남은 파일은 건너뜁니다.");
                break;
            }

            var file = _files[i];
            file.Status = "Processing";
            AddLog($"{file.FileName} 변환 시작...");

            var sheets = ExcelSpecReader.GetWorksheetNames(file.Path);
            if (sheets.Count > 0 && !sheets.Any(name => string.Equals(name, sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                file.Status = "Skipped";
                var warning = $"{file.FileName}: '{sheetName}' 시트가 없어 스킵";
                _sessionWarnings.Add(warning);
                AddLog($"경고: {warning}");
                ProgressBar.Value = i + 1;
                continue;
            }

            var outputPath = Path.Combine(outputFolder, BuildOutputFileName(file.Path, targetVersion));
            var options = BuildOptions(file.Path, targetVersion);

            try
            {
                var result = await Task.Run(() => Converter.Convert(file.Path, outputPath, sheetName, options, externalRefPath, externalRefSheet));
                file.Status = "Done";
                _warningsCount += result.Diagnostics.WarningCount;
                _lastWarningsPath = result.Diagnostics.WarningCount > 0 ? result.WarningsPath : _lastWarningsPath;
                _lastOutputFile = result.OutputPath;
                AddLog($"완료: {file.FileName}");
            }
            catch (Exception ex)
            {
                file.Status = "Skipped";
                var warning = $"{file.FileName}: {ex.Message}";
                _sessionWarnings.Add(warning);
                AddLog($"경고: {file.FileName} 스킵 - {ex.Message}");
            }

            ProgressBar.Value = i + 1;
            UpdateWarningsDisplay();
        }

        _isConverting = false;
        SetUiEnabled(true);

        WriteSessionWarnings(outputFolder);


        if (_settings.OpenOutputFolderAfterConversion)
        {
            OpenPath(_lastOutputFolder);
        }

        if (_settings.OpenOutputFileAfterConversion)
        {
            OpenPath(_lastOutputFile);
        }
    }


    private void WriteSessionWarnings(string outputFolder)
    {
        if (_sessionWarnings.Count == 0)
        {
            return;
        }

        var path = Path.Combine(outputFolder, "warnings.txt");
        var lines = new List<string>();
        if (File.Exists(path))
        {
            lines.AddRange(File.ReadAllLines(path));
        }

        lines.AddRange(_sessionWarnings.Select(w => $"[WPF] {w}"));
        File.WriteAllLines(path, lines);
        _lastWarningsPath = path;
        _warningsCount += _sessionWarnings.Count;
        UpdateWarningsDisplay();
    }

    private ConvertOptions BuildOptions(string inputPath, AasVersion version)
    {
        var setDate = _settings.UseFixedSetDate ? _settings.FixedSetDate : string.Empty;
        return new ConvertOptions
        {
            Version = version,
            IncludeAllDocumentation = _settings.IncludeAllDocumentation,
            BaseIri = _settings.BaseIri,
            IdScheme = _settings.IdScheme,
            ExampleIriDigitsMode = _settings.ExampleIriDigitsMode,
            DocumentDefaultLanguage = _settings.DefaultLanguage01,
            DocumentDefaultVersionId = _settings.DefaultDocumentVersionId,
            UseFixedSetDate = _settings.UseFixedSetDate,
            DocumentDefaultSetDate = setDate,
            DocumentDefaultStatusValue = _settings.DefaultStatusValue,
            DocumentDefaultRole = _settings.DefaultRole,
            DocumentDefaultOrganizationName = _settings.DefaultOrganizationName,
            DocumentDefaultOrganizationOfficialName = _settings.DefaultOrganizationOfficialName,
            WriteWarningsOnlyWhenNeeded = _settings.WriteWarningsOnlyWhenNeeded,
            FillMissingCategoryWithConstant = _settings.FillMissingCategoryWithConstant,
            MissingCategoryConstant = _settings.MissingCategoryConstant,
            InputFileName = Path.GetFileNameWithoutExtension(inputPath)
        };
    }

    private static string BuildOutputFileName(string inputPath, AasVersion version)
    {
        var baseName = Path.GetFileNameWithoutExtension(inputPath);
        return version == AasVersion.Aas3_0 ? $"{baseName}.aas3.xml" : $"{baseName}.aas.xml";
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private void UpdateWarningsDisplay()
    {
        WarningsCountText.Text = _warningsCount.ToString(CultureInfo.InvariantCulture);
        WarningsButton.IsEnabled = _warningsCount > 0 && !string.IsNullOrWhiteSpace(_lastWarningsPath);
        OpenOutputFolderButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastOutputFolder);
        OpenOutputFileButton.IsEnabled = !string.IsNullOrWhiteSpace(_lastOutputFile);
    }

    private void AddLog(string message)
    {
        _logs.Add($"[{DateTime.Now:HH:mm:ss}] {message}");
        LogListBox.ScrollIntoView(_logs.LastOrDefault());
    }

    private void SetUiEnabled(bool enabled)
    {
        BrowseButton.IsEnabled = enabled;
        RemoveButton.IsEnabled = enabled;
        ClearButton.IsEnabled = enabled;
        OutputFolderTextBox.IsEnabled = enabled;
        SheetNameComboBox.IsEnabled = enabled;
        ExternalRefFileTextBox.IsEnabled = enabled;
        ExternalRefSheetComboBox.IsEnabled = enabled;
        Aas3Radio.IsEnabled = enabled;
        Aas2Radio.IsEnabled = enabled;
        StartButton.IsEnabled = enabled;
        SettingsButton.IsEnabled = enabled;
    }

    private void WarningsButton_Click(object sender, RoutedEventArgs e) => OpenPath(_lastWarningsPath);
    private void OpenOutputFolderButton_Click(object sender, RoutedEventArgs e) => OpenPath(_lastOutputFolder);
    private void OpenOutputFileButton_Click(object sender, RoutedEventArgs e) => OpenPath(_lastOutputFile);

    private static void OpenPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try
        {
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch
        {
            // ignore
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var viewModel = SettingsViewModel.FromSettings(_settings.Clone());
        var dialog = new SettingsWindow(viewModel) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            viewModel.ApplyTo(_settings);
            SaveSettings();
        }
    }
}
