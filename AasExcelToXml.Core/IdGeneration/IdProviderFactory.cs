namespace AasExcelToXml.Core.IdGeneration;

public static class IdProviderFactory
{
    public static IIdProvider Create(ConvertOptions options)
    {
        return options.IdScheme switch
        {
            IdScheme.UuidUrn => new UuidUrnIdProvider(),
            _ => new ExampleIriIdProvider(options.BaseIri, options.ExampleIriDigitsMode)
        };
    }
}
