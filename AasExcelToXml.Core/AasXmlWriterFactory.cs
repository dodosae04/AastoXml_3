using System.Xml.Linq;

namespace AasExcelToXml.Core;

public static class AasXmlWriterFactory
{
    public static XDocument Write(AasEnvironmentSpec spec, ConvertOptions options, SpecDiagnostics diagnostics, DocumentIdGenerator documentIdGenerator)
    {
        return options.Version switch
        {
            AasVersion.Aas3_0 => new AasV3XmlWriter(options, diagnostics, documentIdGenerator).Write(spec),
            _ => new AasV2XmlWriter(options, diagnostics, documentIdGenerator).Write(spec)
        };
    }
}
