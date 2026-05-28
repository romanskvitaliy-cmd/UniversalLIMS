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

    public static IReadOnlyList<TagDefinition> F332 { get; } =
    [
        new("f332_SampleNumber", "№ проби", DataFieldScope.Sample),
        new("f332_SamplingPoint", "Місце та точка відбору", DataFieldScope.Sample),
        new("f332_Quantity", "Кількість, гр", DataFieldScope.Sample, DataFieldType.Number),
        new("f332_Depth", "Глибина відбору, см", DataFieldScope.Sample, DataFieldType.Number),
        new("f332_MethodND", "Науково-технічна документація на метод відбору", DataFieldScope.Sample),
        new("f332_IndicatorName", "Найменування показників", DataFieldScope.Result),
        new("f332_ResultValue", "Результат дослідження у одиницях вимірювання ГДК ОВРВ, мг/кг", DataFieldScope.Result),
        new("f332_ComplianceMark", "Відмітка про відповідність", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F413 { get; } =
    [
        new("f413_SampleNumber", "№ проби", DataFieldScope.Sample),
        new("f413_SamplingLocation", "Місце відбору проб", DataFieldScope.Sample),
        new("f413_DryThermometer", "Показання термометра сухого", DataFieldScope.Result),
        new("f413_WetThermometer", "Показання термометра вологого", DataFieldScope.Result),
        new("f413_AirPressure", "Атмосферний тиск, мм.рт.ст.", DataFieldScope.Result, DataFieldType.Number),
        new("f413_AspirationSpeed", "Швидкість аспірації, л/хв.", DataFieldScope.Result, DataFieldType.Number),
        new("f413_SamplingDuration", "Тривалість відбору проби, хвилин", DataFieldScope.Result, DataFieldType.Number),
        new("f413_SubstanceName", "Назва речовини, що визначається", DataFieldScope.Result),
        new("f413_Concentration", "Концентрація, мг/м³", DataFieldScope.Result),
        new("f413_MPC", "Гранично допустима концентрація, мг/м³", DataFieldScope.Result),
        new("f413_Method", "Методика досліджень", DataFieldScope.Result),
    ];

    public static IReadOnlyList<TagDefinition> F329 { get; } =
    [
        new("f329_AbsorberNumber", "Номер поглинача", DataFieldScope.Sample),
        new("f329_SketchSampleNumber", "Номер проби за ескізом", DataFieldScope.Sample),
        new("f329_SamplingPoint", "Точка відбору проб", DataFieldScope.Sample),
        new("f329_AtmosphericPressure", "Атмосферний тиск, мм.рт.ст.", DataFieldScope.Result, DataFieldType.Number),
        new("f329_AirTemperature", "Температура повітря, °C", DataFieldScope.Result, DataFieldType.Number),
        new("f329_RelativeHumidity", "Відносна вологість, %", DataFieldScope.Result, DataFieldType.Number),
        new("f329_WindDirection", "Напрямок вітру", DataFieldScope.Result),
        new("f329_WindSpeed", "Швидкість вітру, м/сек", DataFieldScope.Result, DataFieldType.Number),
        new("f329_WeatherCondition", "Стан погоди", DataFieldScope.Result),
        new("f329_SamplingStart", "Час відбору: Початок", DataFieldScope.Result),
        new("f329_SamplingEnd", "Час відбору: Кінець", DataFieldScope.Result),
        new("f329_SamplingRate", "Швидкість відбору л/хв", DataFieldScope.Result, DataFieldType.Number),
        new("f329_SubstanceName", "Назва досліджуваної речовини", DataFieldScope.Result),
        new("f329_ConcentrationSingle", "Виявлена концентрація разова, мг/м³", DataFieldScope.Result, DataFieldType.Number),
        new("f329_ConcentrationDaily", "Виявлена концентрація середньодобова, мг/м³", DataFieldScope.Result, DataFieldType.Number),
        new("f329_MPC", "ГДК, мг/м³", DataFieldScope.Result, DataFieldType.Number),
        new("f329_LimitingIndicator", "Лімітуючий показник (ЛДК/ЛПК, тип шкідливості)", DataFieldScope.Result),
        new("f329_MethodND", "НТД на методи досліджень", DataFieldScope.Result),
        new("f329_LabPerformer", "Дослідження проводив (ПІБ)", DataFieldScope.Conclusion),
        new("f329_SanitaryDoctorConclusion", "Висновок санітарного лікаря", DataFieldScope.Conclusion),
        new("f329_HygieneDoctor", "Лікар із загальної гігієни", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F205 { get; } =
    [
        new("f205_RegistrationNumber", "Реєстраційний номер зразка", DataFieldScope.Sample),
        new("f205_SampleDate", "від ______ 2024 р.", DataFieldScope.Sample, DataFieldType.Date),
        new("f205_SampleName", "Найменування зразка", DataFieldScope.Sample),
        new("f205_SamplingLocation", "Місце відбору", DataFieldScope.Sample),
        new("f205_Purpose", "Мета випробування", DataFieldScope.Registration),
        new("f205_Customer", "Замовник", DataFieldScope.Registration),
        new("f205_TotalMicrobialCount", "Загальне мікробне число, в КУО/см³ 37°С", DataFieldScope.Result),
        new("f205_TotalMicrobialCountNorm", "50", DataFieldScope.Result),
        new("f205_TotalMicrobialCountValue", "Отримане значення", DataFieldScope.Result),
        new("f205_TotalMicrobialCountUncertainty", "Розширена невизначеність", DataFieldScope.Result),
        new("f205_TotalMicrobialCountMethod", "MB 10.2.1-113-2005", DataFieldScope.Result),
        new("f205_TotalMicrobialCountCompliance", "Відмітка про відповідність", DataFieldScope.Conclusion),
        new("f205_TotalColiforms", "Загальні коліформи, в 100 см³", DataFieldScope.Result),
        new("f205_TotalColiformsNorm", "не допускається", DataFieldScope.Result),
        new("f205_TotalColiformsValue", "Отримане значення", DataFieldScope.Result),
        new("f205_TotalColiformsUncertainty", "Розширена невизначеність", DataFieldScope.Result),
        new("f205_TotalColiformsMethod", "MB 10.2.1-113-2005", DataFieldScope.Result),
        new("f205_TotalColiformsCompliance", "Відмітка про відповідність", DataFieldScope.Conclusion),
        new("f205_Enterococci", "Ентерококи, в 100 см³", DataFieldScope.Result),
        new("f205_EnterococciNorm", "не допускається", DataFieldScope.Result),
        new("f205_EnterococciValue", "Отримане значення", DataFieldScope.Result),
        new("f205_EnterococciUncertainty", "Розширена невизначеність", DataFieldScope.Result),
        new("f205_EnterococciMethod", "Інструкція 19ПІ-5,8", DataFieldScope.Result),
        new("f205_EnterococciCompliance", "Відмітка про відповідність", DataFieldScope.Conclusion),
        new("f205_EColi", "E. Coli, в 100 см³", DataFieldScope.Result),
        new("f205_EColiNorm", "не допускається", DataFieldScope.Result),
        new("f205_EColiValue", "Отримане значення", DataFieldScope.Result),
        new("f205_EColiUncertainty", "Розширена невизначеність", DataFieldScope.Result),
        new("f205_EColiMethod", "MB 10.2.1-113-2005", DataFieldScope.Result),
        new("f205_EColiCompliance", "Відмітка про відповідність", DataFieldScope.Conclusion),
        new("f205_IssueDate", "Дата видачі результату", DataFieldScope.Result, DataFieldType.Date),
        new("f205_ExecutorSignature", "Підпис виконавця", DataFieldScope.Conclusion),
        new("f205_Conclusion", "Висновок", DataFieldScope.Conclusion),
        new("f205_SanitaryDoctor", "Санітарний лікар", DataFieldScope.Conclusion),
        new("f205_SanitaryDoctorSignature", "Підпис Санітарного лікаря", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F205S { get; } =
    [
        new("f205s_RegistrationNumber", "Реєстраційний номер зразка", DataFieldScope.Sample),
        new("f205s_SampleName", "Найменування зразка", DataFieldScope.Sample),
        new("f205s_SamplingLocation", "Місце відбору зразка", DataFieldScope.Sample),
        new("f205s_Purpose", "Мета випробування", DataFieldScope.Registration),
        new("f205s_Customer", "Замовник", DataFieldScope.Registration),
        new("f205s_Number", "№ п/п", DataFieldScope.Result),
        new("f205s_RegistrationNumberColumn", "Реєстраційний номер", DataFieldScope.Result),
        new("f205s_SamplingLocationColumn", "Місце відбору", DataFieldScope.Result),
        new("f205s_ObjectName", "Найменування об'єктів дослідження", DataFieldScope.Result),
        new("f205s_NormativeValue", "Нормативне значення", DataFieldScope.Result),
        new("f205s_ObtainedValue", "Отримане значення", DataFieldScope.Result),
        new("f205s_ComplianceMark", "Відмітка про відповідність", DataFieldScope.Conclusion),
        new("f205s_DoctorConclusion", "Висновок лікаря", DataFieldScope.Conclusion),
        new("f205s_IssueDate", "Дата видачі результату", DataFieldScope.Result, DataFieldType.Date),
        new("f205s_ExecutorSignature", "Підпис виконавця", DataFieldScope.Conclusion),
    ];

    public static IReadOnlyList<TagDefinition> F325 { get; } =
    [
        new("f325_WaterTemperature", "Температура води у градусах C", DataFieldScope.Result, DataFieldType.Number),
        new("f325_SmellIntensity", "Запах: Інтенсивність у балах", DataFieldScope.Result, DataFieldType.Number),
        new("f325_SmellCharacter", "Запах: Характер (описати)", DataFieldScope.Result),
        new("f325_SmellDilutionThreshold", "Запах: Поріг зникнення (в розведенні)", DataFieldScope.Result),
        new("f325_ColorIntensity", "Кольоровість у градусах", DataFieldScope.Result, DataFieldType.Number),
        new("f325_ColorDescription", "Колір (описати)", DataFieldScope.Result),
        new("f325_ColorDilutionThreshold", "Поріг зникнення кольору (в розведенні)", DataFieldScope.Result),
        new("f325_Turbidity", "Мутність, осад (описати)", DataFieldScope.Result),
        new("f325_Transparency", "Прозорість, см", DataFieldScope.Result, DataFieldType.Number),
        new("f325_FloatingImpurities", "Плаваючі домішки, плівки", DataFieldScope.Result),
        new("f325_SuspendedSolids", "Зважені речовини, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_pH", "pH", DataFieldScope.Result, DataFieldType.Number),
        new("f325_DissolvedOxygen", "Розчинний кисень, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_BOD5", "БСК-5, мгО₂/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_BOD20", "БСК-20, мгО₂/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Oxidizability", "Окисність, мгО₂/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_XCK", "ХСК, мгО₂/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Alkalinity", "Лужність, мг/екв", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Acidity", "Кислотність, мг/екв", DataFieldScope.Result, DataFieldType.Number),
        new("f325_TotalHardness", "Загальна жорсткість, мг-екв/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_DryResidue", "Сухий залишок, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Calcium", "Кальцій, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Magnesium", "Магній, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Iron", "Залізо, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Chlorides", "Хлориди, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Sulfates", "Сульфати, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Ammonia", "Азот аміаку, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Nitrates", "Азот нітратів, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Nitrites", "Азот нітритів, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Fluorine", "Фтор, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_SPAP", "СПАР, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_OilProducts", "Нафтопродукти, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Phenols", "Феноли, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Cyanides", "Ціаніди, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Copper", "Мідь, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Lead", "Свинець, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_ChromiumTrivalent", "Хром трьохвалентний, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_ChromiumTotal", "Хром загальний, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_HCH", "γ-ГХЦГ, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_DDT_1", "ДДТ (перший рядок), мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_DDE", "ДДЕ, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_DDD", "ДДД (четвертий рядок у бланку), мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_TMTD", "ТМТД, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_DDVP", "ДДВФ, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_Actellic", "Актеллік, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_Chlorophos", "Хлорофос, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_Rogor", "Рогор, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_Pesticide_Carbophos", "Карбофос, мг/дм³", DataFieldScope.Result, DataFieldType.Number),
        new("f325_OtherSubstances", "Інші речовини", DataFieldScope.Result),
        new("f325_MethodND", "НТД на методи дослідження", DataFieldScope.Result),
    ];

    /// <summary>Усі теги каталогу (після протокольних списків).</summary>
    public static IReadOnlyList<TagDefinition> All { get; } =
        [.. F327, .. Food, .. F205, .. F205S, .. F345, .. F343, .. F332, .. F413, .. F329, .. F325];
}
