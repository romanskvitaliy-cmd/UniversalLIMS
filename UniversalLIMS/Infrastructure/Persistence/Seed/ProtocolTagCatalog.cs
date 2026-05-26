using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Seed;

/// <summary>Каталог семантичних ключів протоколів (локальні префікси). Джерело: docs/data/protocol-tags-*.json</summary>
public static class ProtocolTagCatalog
{
    public sealed record TagDefinition(
        string Key,
        string DisplayNameUk,
        DataFieldScope Scope = DataFieldScope.Registration,
        DataFieldType FieldType = DataFieldType.Text);

    public static IReadOnlyList<TagDefinition> F327 { get; } =
    [
        // Спільні (локальні копії)
        new("f327_FacilityName", "Найменування закладу", DataFieldScope.System),
        new("f327_ProtocolNumber", "ПРОТОКОЛ №", DataFieldScope.System),
        new("f327_SamplingDate", "Дата і час відбору", DataFieldScope.Sample, DataFieldType.Date),
        new("f327_SamplingPoint", "Місце відбору проби", DataFieldScope.Sample),
        new("f327_WaterSource", "Найменування вододжерела", DataFieldScope.Sample),
        // Специфічні
        new("f327_Smell", "Запах", DataFieldScope.Result),
        new("f327_Taste", "Присмак", DataFieldScope.Result),
        new("f327_Turbidity", "Каламутність", DataFieldScope.Result),
        new("f327_Sediment", "Осад (описати)", DataFieldScope.Result),
        new("f327_Transparency", "Прозорість", DataFieldScope.Result),
        new("f327_pH", "pH", DataFieldScope.Result, DataFieldType.Number),
        new("f327_ResidualChlorine", "Залишковий хлор", DataFieldScope.Result),
        new("f327_Fluorides", "Фториди", DataFieldScope.Result),
        new("f327_Aluminium", "Залишковий алюміній", DataFieldScope.Result),
        new("f327_Polyphosphates", "Поліфосфати", DataFieldScope.Result),
        new("f327_Cobalt", "Кобальт", DataFieldScope.Result),
        new("f327_Nickel", "Нікель", DataFieldScope.Result),
        new("f327_Manganese", "Марганець", DataFieldScope.Result),
        new("f327_Trihalomethanes", "Тригалогенметани (сума)", DataFieldScope.Result),
        new("f327_SurfaceActiveSubstances", "Поверхнево-активні речовини", DataFieldScope.Result),
        new("f327_Phenols", "Феноли", DataFieldScope.Result),
        new("f327_OilProducts", "Нафтопродукти", DataFieldScope.Result),
        new("f327_TotalHardness", "Загальна жорсткість", DataFieldScope.Result),
        new("f327_DryResidue", "Сухий залишок", DataFieldScope.Result),
        new("f327_Chlorides", "Хлориди", DataFieldScope.Result),
        new("f327_Sulfates", "Сульфати", DataFieldScope.Result),
        new("f327_Iron", "Залізо", DataFieldScope.Result),
        new("f327_Copper", "Мідь", DataFieldScope.Result),
        new("f327_Zinc", "Цинк", DataFieldScope.Result),
        new("f327_Lead", "Свинець", DataFieldScope.Result),
    ];

    public static IReadOnlyList<TagDefinition> Food { get; } =
    [
        new("Food_ProtocolNumber", "ПРОТОКОЛ №", DataFieldScope.System),
        new("Food_RegistrationNumber", "Реєстраційний номер зразка", DataFieldScope.Sample),
        new("Food_SampleName", "Найменування зразка", DataFieldScope.Sample),
        new("Food_SamplingPoint", "Місце відбору", DataFieldScope.Sample),
        new("Food_Purpose", "Мета випробування", DataFieldScope.Registration),
        new("Food_Customer", "Замовник", DataFieldScope.Registration),
        new("Food_SamplingDate", "Дата відбору", DataFieldScope.Sample, DataFieldType.Date),
        new("Food_AnalysisDate", "Дата видачі результату", DataFieldScope.Result, DataFieldType.Date),
        new("Food_SignaturePerformer", "Підпис виконання", DataFieldScope.Conclusion),
        new("Food_Conclusion", "Висновок", DataFieldScope.Conclusion),
        new("Food_SanitaryDoctor", "Санітарний лікар (підпис)", DataFieldScope.Conclusion),
        new("Food_IndicatorName", "Назва показника", DataFieldScope.Result),
        new("Food_Norm", "Норма", DataFieldScope.Result),
        new("Food_ResultValue", "Отримане значення", DataFieldScope.Result),
        new("Food_MethodND", "НД на метод випробувань", DataFieldScope.Result),
        new("Food_ComplianceMark", "Відмітка про відповідність", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F345 { get; } =
    [
        new("f345_ProductName", "Назва страви, напівфабрикату", DataFieldScope.Sample),
        new("f345_ObjectAddress", "Назва об'єкта, адреса", DataFieldScope.Sample),
        new("f345_Weight", "Вага порції в грамах", DataFieldScope.Result, DataFieldType.Number),
        new("f345_ChemicalComposition", "Хімічний склад в одиницях виміру", DataFieldScope.Result),
        new("f345_MineralComposition", "Мінеральний склад в одиницях виміру", DataFieldScope.Result),
        new("f345_CaloricValue", "Калорійність", DataFieldScope.Result),
        new("f345_OrganolepticIndicators", "Органолептичні показники", DataFieldScope.Result),
        new("f345_CoefficientImplementation", "Коефіцієнт виконання", DataFieldScope.Result, DataFieldType.Number),
        new("f345_PercentageDeviation", "% відхилення", DataFieldScope.Result),
        new("f345_Norm", "Норма", DataFieldScope.Result),
        new("f345_ResultValue", "Отримане значення", DataFieldScope.Result),
        new("f345_MethodND", "НД на метод випробувань", DataFieldScope.Result),
        new("f345_ComplianceMark", "Відмітка про відповідність", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F343 { get; } =
    [
        new("f343_ProductName", "Назва показників інгредієнтів та інше", DataFieldScope.Result),
        new("f343_DetectedConcentration", "Виявлена концентрація", DataFieldScope.Result),
        new("f343_Unit", "Одиниці вимірювання", DataFieldScope.Result),
        new("f343_Norm", "Норма по НТД", DataFieldScope.Result),
        new("f343_MethodND", "НТД на методи досліджень", DataFieldScope.Result),
        new("f343_ComplianceMark", "Відмітка про відповідність", DataFieldScope.Conclusion),
    ];

    /// <summary>Усі теги каталогу (після протокольних списків).</summary>
    public static IReadOnlyList<TagDefinition> All { get; } = [.. F327, .. Food, .. F345, .. F343];
}
