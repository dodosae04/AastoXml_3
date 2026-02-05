using AasExcelToXml.Core;

namespace AasExcelToXml.Wpf.Models;

public sealed class AppSettings
{
    public string Language { get; set; } = "ko-KR";
    public bool RememberLastFolders { get; set; } = true;
    public bool OpenOutputFolderAfterConversion { get; set; }
    public bool OpenOutputFileAfterConversion { get; set; }
    public string? LastInputFolder { get; set; }
    public string? LastOutputFolder { get; set; }
    public string BaseIri { get; set; } = "https://example.com/ids/";
    public IdScheme IdScheme { get; set; } = IdScheme.ExampleIri;
    public ExampleIriDigitsMode ExampleIriDigitsMode { get; set; } = ExampleIriDigitsMode.DeterministicHash;
    public bool IncludeAllDocumentation { get; set; }
    public string DefaultLanguage01 { get; set; } = "kr";
    public string DefaultDocumentVersionId { get; set; } = "V1.2";
    public bool UseFixedSetDate { get; set; }
    public string FixedSetDate { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
    public string DefaultStatusValue { get; set; } = "Released";
    public string DefaultRole { get; set; } = "Author";
    public string DefaultOrganizationName { get; set; } = "Hanuri";
    public string DefaultOrganizationOfficialName { get; set; } = "Hanuri";
    public bool WriteWarningsOnlyWhenNeeded { get; set; } = true;

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Language = Language,
            RememberLastFolders = RememberLastFolders,
            OpenOutputFolderAfterConversion = OpenOutputFolderAfterConversion,
            OpenOutputFileAfterConversion = OpenOutputFileAfterConversion,
            LastInputFolder = LastInputFolder,
            LastOutputFolder = LastOutputFolder,
            BaseIri = BaseIri,
            IdScheme = IdScheme,
            ExampleIriDigitsMode = ExampleIriDigitsMode,
            IncludeAllDocumentation = IncludeAllDocumentation,
            DefaultLanguage01 = DefaultLanguage01,
            DefaultDocumentVersionId = DefaultDocumentVersionId,
            UseFixedSetDate = UseFixedSetDate,
            FixedSetDate = FixedSetDate,
            DefaultStatusValue = DefaultStatusValue,
            DefaultRole = DefaultRole,
            DefaultOrganizationName = DefaultOrganizationName,
            DefaultOrganizationOfficialName = DefaultOrganizationOfficialName,
            WriteWarningsOnlyWhenNeeded = WriteWarningsOnlyWhenNeeded
        };
    }
}
