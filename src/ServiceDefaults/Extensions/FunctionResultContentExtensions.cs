using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.AI;

namespace ServiceDefaults.Extensions;

public static class FunctionResultContentExtensions
{
    public static string AsDisplayString(this FunctionResultContent content)
    {
        string result = content.Result?.ToString() ?? "???";

        if (TryFormatXml(result, out string formattedXml))
        {
            return formattedXml;
        }

        return TryFormatJson(result, out string formattedJson) ? formattedJson : result;
    }

    private static bool TryFormatJson(string value, out string formattedJson)
    {
        formattedJson = value;
        string trimmedValue = value.TrimStart();

        if (string.IsNullOrWhiteSpace(value) || trimmedValue[0] is not ('{' or '['))
        {
            return false;
        }

        try
        {
            JsonSerializerOptions options = new()
            {
                WriteIndented = true
            };

            using JsonDocument document = JsonDocument.Parse(value);
            formattedJson = JsonSerializer.Serialize(document.RootElement, options);

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool TryFormatXml(string value, out string formattedXml)
    {
        formattedXml = value;

        if (string.IsNullOrWhiteSpace(value) || !value.TrimStart().StartsWith('<'))
        {
            return false;
        }

        try
        {
            XDocument document = XDocument.Parse(value, LoadOptions.PreserveWhitespace);
            XmlWriterSettings settings = new()
            {
                Indent = true,
                OmitXmlDeclaration = document.Declaration == null
            };

            using StringWriter stringWriter = new();
            using XmlWriter xmlWriter = XmlWriter.Create(stringWriter, settings);
            document.Save(xmlWriter);
            xmlWriter.Flush();
            formattedXml = stringWriter.ToString();

            return true;
        }
        catch (XmlException)
        {
            return false;
        }
    }
}
