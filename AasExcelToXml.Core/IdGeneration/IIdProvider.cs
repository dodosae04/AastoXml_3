namespace AasExcelToXml.Core.IdGeneration;

public interface IIdProvider
{
    string GetAssetId(string aasIdShort);
    string GetSubmodelId(string aasIdShort, string submodelIdShort);
    string GetShellId(string aasIdShort);
    string GetConceptDescriptionId(string idShort);
}
