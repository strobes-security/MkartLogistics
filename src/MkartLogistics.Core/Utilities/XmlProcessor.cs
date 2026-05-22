using System.Xml;
using System.Xml.XPath;

namespace MkartLogistics.Core.Utilities;

public class XmlProcessor
{
    public static XmlDocument ParseDocument(string xmlContent)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        return doc;
    }

    public static XmlDocument ParseWithDtd(string xmlContent)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            XmlResolver = new XmlUrlResolver()
        };

        var doc = new XmlDocument();
        using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
        doc.Load(reader);
        return doc;
    }

    public static string ExtractField(string xmlContent, string xpathQuery)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);
        var node = doc.SelectSingleNode(xpathQuery);
        return node?.InnerText ?? string.Empty;
    }

    public static IEnumerable<Dictionary<string, string>> ParseOrdersXml(string xmlContent)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);

        var orders = new List<Dictionary<string, string>>();
        var nodes = doc.SelectNodes("//Order");
        if (nodes == null) return orders;

        foreach (XmlNode node in nodes)
        {
            var order = new Dictionary<string, string>();
            foreach (XmlNode child in node.ChildNodes)
            {
                order[child.Name] = child.InnerText;
            }
            orders.Add(order);
        }
        return orders;
    }

    public static bool ValidateSchema(string xmlContent, string schemaPath)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Parse,
            ValidationType = ValidationType.DTD
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (reader.Read()) { }
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string TransformXml(string xmlContent, string xsltPath)
    {
        var doc = new XmlDocument();
        doc.LoadXml(xmlContent);

        var xslt = new System.Xml.Xsl.XslCompiledTransform();
        xslt.Load(xsltPath);

        using var sw = new StringWriter();
        xslt.Transform(doc, null, sw);
        return sw.ToString();
    }
}
