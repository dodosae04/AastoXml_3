using System.ComponentModel;
using System.Runtime.CompilerServices;
using AasExcelToXml.Core;
using AasExcelToXml.Wpf.Models;
using AasExcelToXml.Wpf.Services;

namespace AasExcelToXml.Wpf.ViewModels;

public sealed class SettingsViewModel : INotifyPropertyChanged
{
    private string _language = "ko-KR";
    private bool _rememberLastFolders;
    private bool _openOutputFolderAfterConversion;
    private bool _openOutputFileAfterConversion;
    private string _baseIri = "https://example.com/ids/";
    private IdScheme _idScheme = IdScheme.ExampleIri;
    private ExampleIriDigitsMode _exampleIriDigitsMode = ExampleIriDigitsMode.DeterministicHash;
    private bool _includeAllDocumentation;
    private string _defaultLanguage01 = "kr";
    private string _defaultDocumentVersionId = "V1.2";
    private bool _useFixedSetDate;
    private DateTime _fixedSetDate = DateTime.Today;
    private string _defaultStatusValue = "Released";
    private string _defaultRole = "Author";
    private string _defaultOrganizationName = "Hanuri";
    private string _defaultOrganizationOfficialName = "Hanuri";
    private bool _writeWarningsOnlyWhenNeeded = true;
    private bool _fillMissingCategoryWithConstant;
    private string _missingCategoryConstant = "CONSTANT";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Language
    {
        get => _language;
        set
        {
            if (_language == value)
            {
                return;
            }

            _language = value;
            LocalizationService.Instance.SetCulture(value);
            OnPropertyChanged();
        }
    }

    public bool RememberLastFolders
    {
        get => _rememberLastFolders;
        set => SetField(ref _rememberLastFolders, value);
    }

    public bool OpenOutputFolderAfterConversion
    {
        get => _openOutputFolderAfterConversion;
        set => SetField(ref _openOutputFolderAfterConversion, value);
    }

    public bool OpenOutputFileAfterConversion
    {
        get => _openOutputFileAfterConversion;
        set => SetField(ref _openOutputFileAfterConversion, value);
    }

    public string BaseIri
    {
        get => _baseIri;
        set => SetField(ref _baseIri, value);
    }

    public IdScheme IdScheme
    {
        get => _idScheme;
        set => SetField(ref _idScheme, value);
    }

    public ExampleIriDigitsMode ExampleIriDigitsMode
    {
        get => _exampleIriDigitsMode;
        set => SetField(ref _exampleIriDigitsMode, value);
    }

    public bool IncludeAllDocumentation
    {
        get => _includeAllDocumentation;
        set => SetField(ref _includeAllDocumentation, value);
    }

    public string DefaultLanguage01
    {
        get => _defaultLanguage01;
        set => SetField(ref _defaultLanguage01, value);
    }

    public string DefaultDocumentVersionId
    {
        get => _defaultDocumentVersionId;
        set => SetField(ref _defaultDocumentVersionId, value);
    }

    public bool UseFixedSetDate
    {
        get => _useFixedSetDate;
        set => SetField(ref _useFixedSetDate, value);
    }

    public DateTime FixedSetDate
    {
        get => _fixedSetDate;
        set => SetField(ref _fixedSetDate, value);
    }

    public string DefaultStatusValue
    {
        get => _defaultStatusValue;
        set => SetField(ref _defaultStatusValue, value);
    }

    public string DefaultRole
    {
        get => _defaultRole;
        set => SetField(ref _defaultRole, value);
    }

    public string DefaultOrganizationName
    {
        get => _defaultOrganizationName;
        set => SetField(ref _defaultOrganizationName, value);
    }

    public string DefaultOrganizationOfficialName
    {
        get => _defaultOrganizationOfficialName;
        set => SetField(ref _defaultOrganizationOfficialName, value);
    }

    public bool WriteWarningsOnlyWhenNeeded
    {
        get => _writeWarningsOnlyWhenNeeded;
        set => SetField(ref _writeWarningsOnlyWhenNeeded, value);
    }

    public bool FillMissingCategoryWithConstant
    {
        get => _fillMissingCategoryWithConstant;
        set => SetField(ref _fillMissingCategoryWithConstant, value);
    }

    public string MissingCategoryConstant
    {
        get => _missingCategoryConstant;
        set => SetField(ref _missingCategoryConstant, value);
    }

    public static SettingsViewModel FromSettings(AppSettings settings)
    {
        return new SettingsViewModel
        {
            Language = settings.Language,
            RememberLastFolders = settings.RememberLastFolders,
            OpenOutputFolderAfterConversion = settings.OpenOutputFolderAfterConversion,
            OpenOutputFileAfterConversion = settings.OpenOutputFileAfterConversion,
            BaseIri = settings.BaseIri,
            IdScheme = settings.IdScheme,
            ExampleIriDigitsMode = settings.ExampleIriDigitsMode,
            IncludeAllDocumentation = settings.IncludeAllDocumentation,
            DefaultLanguage01 = settings.DefaultLanguage01,
            DefaultDocumentVersionId = settings.DefaultDocumentVersionId,
            UseFixedSetDate = settings.UseFixedSetDate,
            FixedSetDate = DateTime.TryParse(settings.FixedSetDate, out var parsed) ? parsed : DateTime.Today,
            DefaultStatusValue = settings.DefaultStatusValue,
            DefaultRole = settings.DefaultRole,
            DefaultOrganizationName = settings.DefaultOrganizationName,
            DefaultOrganizationOfficialName = settings.DefaultOrganizationOfficialName,
            WriteWarningsOnlyWhenNeeded = settings.WriteWarningsOnlyWhenNeeded,
            FillMissingCategoryWithConstant = settings.FillMissingCategoryWithConstant,
            MissingCategoryConstant = settings.MissingCategoryConstant
        };
    }

    public void ApplyTo(AppSettings settings)
    {
        settings.Language = Language;
        settings.RememberLastFolders = RememberLastFolders;
        settings.OpenOutputFolderAfterConversion = OpenOutputFolderAfterConversion;
        settings.OpenOutputFileAfterConversion = OpenOutputFileAfterConversion;
        settings.BaseIri = BaseIri;
        settings.IdScheme = IdScheme;
        settings.ExampleIriDigitsMode = ExampleIriDigitsMode;
        settings.IncludeAllDocumentation = IncludeAllDocumentation;
        settings.DefaultLanguage01 = DefaultLanguage01;
        settings.DefaultDocumentVersionId = DefaultDocumentVersionId;
        settings.UseFixedSetDate = UseFixedSetDate;
        settings.FixedSetDate = FixedSetDate.ToString("yyyy-MM-dd");
        settings.DefaultStatusValue = DefaultStatusValue;
        settings.DefaultRole = DefaultRole;
        settings.DefaultOrganizationName = DefaultOrganizationName;
        settings.DefaultOrganizationOfficialName = DefaultOrganizationOfficialName;
        settings.WriteWarningsOnlyWhenNeeded = WriteWarningsOnlyWhenNeeded;
        settings.FillMissingCategoryWithConstant = FillMissingCategoryWithConstant;
        settings.MissingCategoryConstant = MissingCategoryConstant;
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        OnPropertyChanged(name);
    }
}
