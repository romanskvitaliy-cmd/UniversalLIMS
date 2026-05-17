namespace UniversalLIMS.Infrastructure.Registration;

/// <summary>
/// Зіставляє PDF-теги з конструктора з канонічними <see cref="Domain.Templates.DataField.Key"/>.
/// </summary>
public static class PdfWorkspaceFieldKeyResolver
{
    private static readonly Dictionary<string, string> TagToDataFieldKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ProtocolNumber"] = "Protocol.Number",
        ["ProtocolDate"] = "Protocol.Date",
        ["ApproveDate"] = "Document.ApproveDate",
        ["Global.FacilityName"] = "Facility.Name",
        ["Global.DocNumber"] = "Protocol.Number",
        ["Global.ObjectName"] = "Order.ObjectName",
        ["Global.SamplePoint"] = "Sample.SamplingLocation",
        ["Global.Purpose"] = "Order.Purpose",
        ["Global.SampleDate"] = "Sample.SamplingDate",
        ["Global.SampleTime"] = "Sample.SamplingTime",
        ["Global.NTD"] = "Sample.SamplingStandard",
        ["SampleName"] = "Order.ObjectName",
        ["SamplingLocation"] = "Sample.SamplingLocation",
        ["RegistrationNumber"] = "Sample.RegistrationNumber",
        ["SamplingDate"] = "Sample.SamplingDate",
        ["TestPurpose"] = "Order.Purpose",
        ["SamplingStandard"] = "Sample.SamplingStandard",
        ["MeasuringTools"] = "Result.Equipment",
        ["Air.Pressure"] = "Env.Pressure",
        ["Air.Temperature"] = "Env.Temperature",
        ["Air.Humidity"] = "Env.Humidity",
        ["Air.WindDirection"] = "Env.WindDirection",
        ["Air.WindSpeed"] = "Env.WindSpeed",
        ["Air.Concentration"] = "Result.Concentration",
        ["Air.GDK"] = "Result.MPC",
        ["Water.pH"] = "Result.pH",
        ["Water.Odor"] = "Result.Odor",
        ["Water.Color"] = "Result.Color",
        ["Water.Turbidity"] = "Result.Turbidity",
        ["Global.Conclusion"] = "Expert.Conclusion",
        ["Global.Inspector"] = "Expert.Signer.Sampler",
        ["Global.Researcher"] = "Expert.Signer.Tester",
    };

    private static readonly Dictionary<string, string> DisplayNameByKey = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TestPeriod"] = "Термін проведення випробувань",
        ["TransportConditions"] = "Умови транспортування",
    };

    public static string Resolve(string clientKey)
    {
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            return string.Empty;
        }

        var trimmed = clientKey.Trim();
        return TagToDataFieldKey.TryGetValue(trimmed, out var mapped)
            ? mapped
            : trimmed;
    }

    public static string GetDisplayNameUk(string dataFieldKey)
    {
        if (DisplayNameByKey.TryGetValue(dataFieldKey, out var displayName))
        {
            return displayName;
        }

        return dataFieldKey;
    }

    public static bool IsKnownTag(string clientKey) =>
        !string.IsNullOrWhiteSpace(clientKey) && TagToDataFieldKey.ContainsKey(clientKey.Trim());
}
