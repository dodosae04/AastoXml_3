using System.Xml.Linq;

namespace AasExcelToXml.Core;

internal sealed class Aas3ElementOrderer
{
    private readonly XNamespace _aasNs;
    private readonly Aas3Profile _profile;

    public Aas3ElementOrderer(XNamespace aasNs, Aas3Profile profile)
    {
        _aasNs = aasNs;
        _profile = profile;
    }

    public XElement BuildElement(string elementName, IEnumerable<Aas3ChildElement> children)
    {
        return new XElement(_aasNs + elementName, OrderChildren(elementName, children));
    }

    public IEnumerable<XElement> OrderChildren(string parentName, IEnumerable<Aas3ChildElement> children)
    {
        var items = children.Where(child => child.Element is not null).ToList();
        var order = _profile.ElementOrders.TryGetValue(parentName, out var list) ? list : null;
        if (order is null || order.Count == 0)
        {
            return items.Select(child => child.Element!);
        }

        var allowed = new HashSet<string>(order, StringComparer.Ordinal);
        return order.SelectMany(name => items.Where(child => child.Name == name && allowed.Contains(child.Name)))
            .Select(child => child.Element!);
    }
}

internal sealed record Aas3ChildElement(string Name, XElement? Element);
