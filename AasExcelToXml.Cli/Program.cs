using AasExcelToXml.Core;
using Microsoft.Extensions.Logging;
using System.Windows.Forms;

internal class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var loggerFactory = LoggerFactory.Create(b =>
        {
            b.AddSimpleConsole(o =>
            {
                o.SingleLine = true;
                o.TimestampFormat = "HH:mm:ss ";
            });
            b.SetMinimumLevel(LogLevel.Information);
        });
        var logger = loggerFactory.CreateLogger("AasExcelToXml");

        // CLI지만 사용자 편의를 위해 Windows 다이얼로그를 사용합니다.
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var useRepoOut = false;
        var sheetName = "사양시트";
        var version = AasVersion.Aas2_0;
        var includeAllDocumentation = false;
        var goldenPath = string.Empty;
        var runDiff = false;
        var failOnDiff = false;
        var failOnWarnings = false;
        var positional = new List<string>();

        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--repoOut", StringComparison.OrdinalIgnoreCase))
            {
                useRepoOut = true;
                continue;
            }

            if (string.Equals(arg, "--allDocs", StringComparison.OrdinalIgnoreCase)
                || string.Equals(arg, "--allDocuments", StringComparison.OrdinalIgnoreCase))
            {
                includeAllDocumentation = true;
                continue;
            }

            if (string.Equals(arg, "--diff", StringComparison.OrdinalIgnoreCase))
            {
                runDiff = true;
                continue;
            }

            if (string.Equals(arg, "--failOnDiff", StringComparison.OrdinalIgnoreCase))
            {
                failOnDiff = true;
                continue;
            }

            if (string.Equals(arg, "--failOnWarnings", StringComparison.OrdinalIgnoreCase))
            {
                failOnWarnings = true;
                continue;
            }

            if (arg.StartsWith("--golden=", StringComparison.OrdinalIgnoreCase))
            {
                goldenPath = arg.Substring("--golden=".Length);
                continue;
            }

            if (string.Equals(arg, "--golden", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    logger.LogError("옵션 --golden 뒤에 파일 경로가 필요합니다.");
                    return;
                }

                goldenPath = args[i + 1];
                i++;
                continue;
            }

            if (arg.StartsWith("--sheet=", StringComparison.OrdinalIgnoreCase))
            {
                sheetName = arg.Substring("--sheet=".Length);
                continue;
            }

            if (string.Equals(arg, "--sheet", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    logger.LogError("옵션 --sheet 뒤에 시트명이 필요합니다.");
                    return;
                }

                sheetName = args[i + 1];
                i++;
                continue;
            }

            if (arg.StartsWith("--version=", StringComparison.OrdinalIgnoreCase))
            {
                var value = arg.Substring("--version=".Length);
                if (!TryParseVersion(value, out version))
                {
                    logger.LogError("알 수 없는 AAS 버전입니다: {Value}", value);
                    return;
                }

                continue;
            }

            if (string.Equals(arg, "--version", StringComparison.OrdinalIgnoreCase))
            {
                if (i + 1 >= args.Length)
                {
                    logger.LogError("옵션 --version 뒤에 2 또는 3이 필요합니다.");
                    return;
                }

                if (!TryParseVersion(args[i + 1], out version))
                {
                    logger.LogError("알 수 없는 AAS 버전입니다: {Value}", args[i + 1]);
                    return;
                }

                i++;
                continue;
            }

            positional.Add(arg);
        }

        if (positional.Count == 0)
        {
            RunInteractive(logger);
            return;
        }

        var inputPath = positional[0];
        var baseDir = AppContext.BaseDirectory;
        var outputDir = ResolveDefaultOutputDirectory(logger, baseDir, inputPath, useRepoOut);
        var outputPath = positional.Count >= 2
            ? positional[1]
            : Path.Combine(outputDir, Path.GetFileNameWithoutExtension(inputPath) + GetDefaultExtension(version));
        RunWithArgs(logger, inputPath, outputPath, sheetName, version, includeAllDocumentation, runDiff, goldenPath, failOnWarnings, failOnDiff);
    }

    private static void RunInteractive(ILogger logger)
    {
        using var openDialog = new OpenFileDialog
        {
            Filter = "엑셀/CSV (*.xlsx;*.csv)|*.xlsx;*.csv|모든 파일 (*.*)|*.*",
            Title = "입력 파일 선택"
        };

        if (openDialog.ShowDialog() != DialogResult.OK)
        {
            logger.LogInformation("사용자가 입력 파일 선택을 취소했습니다.");
            return;
        }

        var inputPath = openDialog.FileName;
        var isXlsx = Path.GetExtension(inputPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase);
        var sheetName = "사양시트";
        if (isXlsx)
        {
            sheetName = ShowSheetNameDialog(sheetName);
            if (string.IsNullOrWhiteSpace(sheetName))
            {
                logger.LogInformation("사용자가 시트명 입력을 취소했습니다.");
                return;
            }
        }

        var selectedVersion = ShowVersionDialog();
        if (selectedVersion is null)
        {
            logger.LogInformation("사용자가 AAS 버전 선택을 취소했습니다.");
            return;
        }

        var version = selectedVersion.Value;
        var defaultFileName = Path.GetFileNameWithoutExtension(inputPath) + GetDefaultExtension(version);
        var defaultDirectory = Path.GetDirectoryName(inputPath) ?? string.Empty;

        using var saveDialog = new SaveFileDialog
        {
            Filter = "AAS XML (*.xml)|*.xml|모든 파일 (*.*)|*.*",
            Title = "저장 위치 선택",
            FileName = defaultFileName,
            InitialDirectory = defaultDirectory
        };

        if (saveDialog.ShowDialog() != DialogResult.OK)
        {
            logger.LogInformation("사용자가 저장 위치 선택을 취소했습니다.");
            return;
        }

        var outputPath = saveDialog.FileName;

        if (TryConvert(logger, inputPath, outputPath, sheetName, version, includeAllDocumentation: true, runDiff: false, goldenPath: string.Empty, failOnWarnings: false, failOnDiff: false))
        {
            MessageBox.Show($"변환이 완료되었습니다.{Environment.NewLine}{outputPath}", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private static void RunWithArgs(ILogger logger, string inputPath, string outputPath, string sheetName, AasVersion version, bool includeAllDocumentation, bool runDiff, string goldenPath, bool failOnWarnings, bool failOnDiff)
    {
        if (TryConvert(logger, inputPath, outputPath, sheetName, version, includeAllDocumentation, runDiff, goldenPath, failOnWarnings, failOnDiff))
        {
            logger.LogInformation("변환이 완료되었습니다: {Output}", outputPath);
        }
    }

    private static bool TryConvert(ILogger logger, string inputPath, string outputPath, string sheetName, AasVersion version, bool includeAllDocumentation, bool runDiff, string goldenPath, bool failOnWarnings, bool failOnDiff)
    {
        try
        {
            if (!File.Exists(inputPath))
            {
                logger.LogError("입력 파일을 찾을 수 없습니다: {Path}", inputPath);
                return false;
            }

            logger.LogInformation("입력: {Input}", inputPath);
            logger.LogInformation("출력: {Output}", outputPath);
            logger.LogInformation("AAS 버전: {Version}", version == AasVersion.Aas2_0 ? "2.0" : "3.0");
            logger.LogInformation("Documentation 전체 포함: {Enabled}", includeAllDocumentation ? "예" : "아니오");
            logger.LogInformation("Golden diff 실행: {Enabled}", runDiff ? "예" : "아니오");

            if (Path.GetExtension(inputPath).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogInformation("시트: {Sheet}", sheetName);
            }

            var result = Converter.Convert(
                inputPath,
                outputPath,
                sheetName,
                new ConvertOptions { Version = version, IncludeAllDocumentation = includeAllDocumentation }
            );

            logger.LogInformation("경고 리포트: {Path}", result.WarningsPath);
            if (result.Diagnostics.HasWarnings)
            {
                logger.LogWarning("경고가 {Count}건 있습니다. warnings.txt를 확인하세요.", result.Diagnostics.GetTotalCount());
                if (failOnWarnings)
                {
                    logger.LogError("경고가 존재하여 실패로 처리합니다.");
                    Environment.ExitCode = 1;
                    return false;
                }
            }

            if (runDiff || failOnDiff)
            {
                if (string.IsNullOrWhiteSpace(goldenPath))
                {
                    logger.LogError("Golden diff를 위해 --golden 경로가 필요합니다.");
                    Environment.ExitCode = 1;
                    return false;
                }

                if (version != AasVersion.Aas3_0)
                {
                    logger.LogError("Golden diff는 AAS 3.0 출력에만 지원됩니다.");
                    Environment.ExitCode = 1;
                    return false;
                }

                var report = AasExcelToXml.Core.GoldenDiff.Aas3GoldenDiffAnalyzer.Analyze(goldenPath, outputPath);
                var summary = AasExcelToXml.Core.GoldenDiff.Aas3GoldenDiffAnalyzer.BuildSummary(report);
                var details = AasExcelToXml.Core.GoldenDiff.Aas3GoldenDiffAnalyzer.BuildReport(report);
                logger.LogInformation(summary);
                logger.LogInformation(details);

                if (report.HasDiffs && failOnDiff)
                {
                    logger.LogError("Golden diff 불일치가 존재하여 실패로 처리합니다.");
                    Environment.ExitCode = 1;
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "변환 중 오류가 발생했습니다: {Message}", ex.Message);
            return false;
        }
    }

    private static string? ShowSheetNameDialog(string defaultSheetName)
    {
        using var dialog = new SheetNameDialog(defaultSheetName);
        return dialog.ShowDialog() == DialogResult.OK ? dialog.SheetName : null;
    }

    private static AasVersion? ShowVersionDialog()
    {
        var result = MessageBox.Show(
            "AAS 버전을 선택하세요.\n예: AAS 2.0\n아니오: AAS 3.0",
            "AAS 버전 선택",
            MessageBoxButtons.YesNoCancel,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1
        );

        return result switch
        {
            DialogResult.Yes => AasVersion.Aas2_0,
            DialogResult.No => AasVersion.Aas3_0,
            _ => null
        };
    }

    private static string? FindRepoRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        for (var i = 0; i < 10 && dir is not null; i++)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, ".git"))
                || File.Exists(Path.Combine(dir.FullName, "AasExcelToXml.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return null;
    }

    private static bool TryParseVersion(string value, out AasVersion version)
    {
        var normalized = value.Trim();
        if (string.Equals(normalized, "2", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "2.0", StringComparison.OrdinalIgnoreCase))
        {
            version = AasVersion.Aas2_0;
            return true;
        }

        if (string.Equals(normalized, "3", StringComparison.OrdinalIgnoreCase)
            || string.Equals(normalized, "3.0", StringComparison.OrdinalIgnoreCase))
        {
            version = AasVersion.Aas3_0;
            return true;
        }

        version = AasVersion.Aas2_0;
        return false;
    }

    private static string ResolveDefaultOutputDirectory(ILogger logger, string baseDir, string inputPath, bool useRepoOut)
    {
        if (useRepoOut)
        {
            var repoRoot = FindRepoRoot(baseDir);
            if (!string.IsNullOrWhiteSpace(repoRoot))
            {
                return Path.Combine(repoRoot, "out");
            }

            logger.LogWarning("레포 루트를 찾지 못해 기본 출력 경로를 사용합니다.");
            return Path.Combine(baseDir, "out");
        }

        var directory = Path.GetDirectoryName(inputPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            return baseDir;
        }

        return directory;
    }

    private static string GetDefaultExtension(AasVersion version)
    {
        return version == AasVersion.Aas3_0 ? ".aas3.xml" : ".aas2.xml";
    }

    private sealed class SheetNameDialog : Form
    {
        private readonly TextBox _input = new();

        public SheetNameDialog(string defaultSheetName)
        {
            Text = "시트명 입력";
            Width = 360;
            Height = 160;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            var label = new Label
            {
                Text = "XLSX 시트명을 입력하세요.",
                AutoSize = true,
                Dock = DockStyle.Top,
                Padding = new Padding(12, 12, 12, 4)
            };

            _input.Text = defaultSheetName;
            _input.Dock = DockStyle.Top;
            _input.Margin = new Padding(12);

            var okButton = new Button
            {
                Text = "확인",
                DialogResult = DialogResult.OK,
                Width = 90
            };

            var cancelButton = new Button
            {
                Text = "취소",
                DialogResult = DialogResult.Cancel,
                Width = 90
            };

            var buttonPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Bottom,
                Padding = new Padding(12),
                AutoSize = true
            };
            buttonPanel.Controls.Add(okButton);
            buttonPanel.Controls.Add(cancelButton);

            Controls.Add(buttonPanel);
            Controls.Add(_input);
            Controls.Add(label);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string SheetName => _input.Text.Trim();
    }
}
