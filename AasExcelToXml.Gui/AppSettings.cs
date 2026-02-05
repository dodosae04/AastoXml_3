using AasExcelToXml.Core;

namespace AasExcelToXml.Gui;

public sealed class AppSettings
{
    public string Language { get; set; } = "ko-KR";
    public AasVersion DefaultVersion { get; set; } = AasVersion.Aas3_0;
    public IdScheme IdScheme { get; set; } = IdScheme.ExampleIri;
    public ExampleIriDigitsMode ExampleIriDigitsMode { get; set; } = ExampleIriDigitsMode.DeterministicHash;
    public string DefaultSheetName { get; set; } = "사양시트";
    public bool IncludeAllDocumentation { get; set; }
    public bool IncludeKoreanDescription { get; set; }
    public long DocumentIdSeed { get; set; } = 64879470;
    public string BaseIri { get; set; } = "https://example.com/ids";
    public bool RememberFolders { get; set; } = true;
    public string? LastInputFolder { get; set; }
    public string? LastOutputFolder { get; set; }
    public bool OpenOutputFolderAfterConversion { get; set; }
    public bool OpenOutputFileAfterConversion { get; set; }
    public bool WriteWarningsOnlyWhenWarnings { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Language = Language,
            DefaultVersion = DefaultVersion,
            IdScheme = IdScheme,
            ExampleIriDigitsMode = ExampleIriDigitsMode,
            DefaultSheetName = DefaultSheetName,
            IncludeAllDocumentation = IncludeAllDocumentation,
            IncludeKoreanDescription = IncludeKoreanDescription,
            DocumentIdSeed = DocumentIdSeed,
            BaseIri = BaseIri,
            RememberFolders = RememberFolders,
            LastInputFolder = LastInputFolder,
            LastOutputFolder = LastOutputFolder,
            OpenOutputFolderAfterConversion = OpenOutputFolderAfterConversion,
            OpenOutputFileAfterConversion = OpenOutputFileAfterConversion,
            WriteWarningsOnlyWhenWarnings = WriteWarningsOnlyWhenWarnings
        };
    }
}
