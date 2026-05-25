using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Domain.Laboratory;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Seed;

public static class LaboratoryDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        await SeedEquipmentAsync(context, cancellationToken);

        var existingKeys = await context.DataFields
            .IgnoreQueryFilters()
            .Select(dataField => dataField.Key)
            .ToListAsync(cancellationToken);

        var existingKeySet = existingKeys.ToHashSet(StringComparer.Ordinal);

        var dataFields = new List<DataField>
        {
            // =============================================
            // 1. ЗАГАЛЬНІ / СИСТЕМНІ (Common → System)
            // =============================================
            Create("Facility.Name", "Найменування закладу", DataFieldScope.System, DataFieldType.Text, "Загальні дані"),
            Create("Protocol.Number", "Номер протоколу", DataFieldScope.System, DataFieldType.Text, "Загальні дані"),
            Create("Protocol.Date", "Дата протоколу", DataFieldScope.System, DataFieldType.Date, "Загальні дані"),
            Create("Document.ApproveDate", "Дата затвердження", DataFieldScope.System, DataFieldType.Date, "Загальні дані"),

            // =============================================
            // 2. ДАНІ ЗАМОВЛЕННЯ / РЕЄСТРАТУРА (Order → Registration)
            // =============================================
            Create("Order.CustomerName", "ПІБ / Назва замовника", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника"),
            Create("Order.CustomerAddress", "Адреса замовника", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника", maxLength: 2000),
            Create("Order.ObjectName", "Назва об'єкта / адреса", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника", maxLength: 2000),
            Create("Order.Purpose", "Мета відбору / випробувань", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника"),
            Create("TestPeriod", "Термін проведення випробувань", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника"),
            Create("TransportConditions", "Умови транспортування", DataFieldScope.Registration, DataFieldType.Text, "Дані замовника"),

            // =============================================
            // 3. ДАНІ ПРОБИ (Sample)
            // =============================================
            Create("Sample.RegistrationNumber", "Реєстраційний № зразка", DataFieldScope.Sample, DataFieldType.Text, "Дані проби"),
            Create("Sample.SamplingDate", "Дата відбору", DataFieldScope.Sample, DataFieldType.Date, "Дані проби"),
            Create("Sample.SamplingTime", "Час відбору", DataFieldScope.Sample, DataFieldType.Text, "Дані проби", format: "HH:mm"),
            Create("Sample.Matrix", "Тип об'єкта (матриця)", DataFieldScope.Sample, DataFieldType.Text, "Дані проби"),
            Create("Sample.SamplingLocation", "Місце / Точка відбору", DataFieldScope.Sample, DataFieldType.Text, "Дані проби", maxLength: 2000),
            Create("Sample.SamplingStandard", "НТД на відбір проб", DataFieldScope.Sample, DataFieldType.Text, "Дані проби"),

            // =============================================
            // 4. УМОВИ ВІДБОРУ ТА МЕТЕОФАКТОРИ (Environment)
            // =============================================
            Create("Env.Temperature", "Температура повітря", DataFieldScope.Sample, DataFieldType.Number, "Метеофактори", "°C"),
            Create("Env.Pressure", "Атмосферний тиск", DataFieldScope.Sample, DataFieldType.Number, "Метеофактори", "мм.рт.ст."),
            Create("Env.Humidity", "Відносна вологість", DataFieldScope.Sample, DataFieldType.Number, "Метеофактори", "%"),
            Create("Env.WindDirection", "Напрямок вітру", DataFieldScope.Sample, DataFieldType.Text, "Метеофактори"),
            Create("Env.WindSpeed", "Швидкість вітру", DataFieldScope.Sample, DataFieldType.Number, "Метеофактори", "м/с"),

            // =============================================
            // 5. РЕЗУЛЬТАТИ ДОСЛІДЖЕНЬ (Result)
            // =============================================
            Create("Result.Concentration", "Виявлена концентрація", DataFieldScope.Result, DataFieldType.Number, "Результати", "мг/м³"),
            Create("Result.pH", "pH (водневий показник)", DataFieldScope.Result, DataFieldType.Number, "Результати"),
            Create("Result.Odor", "Запах (інтенсивність)", DataFieldScope.Result, DataFieldType.Text, "Результати", "бали"),
            Create("Result.Color", "Кольоровість", DataFieldScope.Result, DataFieldType.Number, "Результати", "градуси"),
            Create("Result.Turbidity", "Каламутність", DataFieldScope.Result, DataFieldType.Number, "Результати"),
            Create("Result.MPC", "ГДК (норматив)", DataFieldScope.Result, DataFieldType.Text, "Нормативи"),
            Create("Result.Uncertainty", "Розширена невизначеність", DataFieldScope.Result, DataFieldType.Number, "Результати"),
            Create("Result.TestMethod", "Метод дослідження (НТД)", DataFieldScope.Result, DataFieldType.Text, "Результати"),
            Create("Result.Equipment", "Використане обладнання", DataFieldScope.Result, DataFieldType.Dictionary, "Обладнання"),

            // =============================================
            // 6. ВИСНОВКИ ТА ПІДПИСИ (Expert → Conclusion)
            // =============================================
            Create("Expert.Conclusion", "Висновок", DataFieldScope.Conclusion, DataFieldType.Text, "Висновки", maxLength: 4000),
            Create("Expert.Compliance", "Відповідність нормам", DataFieldScope.Conclusion, DataFieldType.Boolean, "Висновки"),
            Create("Expert.Signer.Sampler", "Особа, що проводила відбір (ПІБ)", DataFieldScope.Conclusion, DataFieldType.Text, "Підписи"),
            Create("Expert.Signer.Tester", "Особа, що проводила дослідження (ПІБ)", DataFieldScope.Conclusion, DataFieldType.Text, "Підписи"),
            Create("Expert.Signer.Head", "Завідувач лабораторії / Керівник", DataFieldScope.Conclusion, DataFieldType.Text, "Підписи"),
        };

        var toAdd = dataFields.Where(dataField => !existingKeySet.Contains(dataField.Key)).ToList();
        if (toAdd.Count == 0)
        {
            return;
        }

        context.DataFields.AddRange(toAdd);
        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task SeedEquipmentAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        var hasActive = await context.Equipment
            .IgnoreQueryFilters()
            .AnyAsync(equipment => equipment.IsActive && !equipment.IsAnnulled, cancellationToken);

        if (hasActive)
        {
            return;
        }

        context.Equipment.Add(new Equipment
        {
            Code = "LAB-GEN-01",
            NameUk = "Універсальне лабораторне обладнання",
            IsActive = true
        });

        await context.SaveChangesAsync(cancellationToken);
    }

    private static DataField Create(
        string key,
        string displayNameUk,
        DataFieldScope scope,
        DataFieldType fieldType,
        string groupName,
        string? unit = null,
        int? maxLength = null,
        string? format = null)
    {
        return new DataField
        {
            Key = key,
            DisplayNameUk = displayNameUk,
            DescriptionUk = groupName,
            Scope = scope,
            FieldType = fieldType,
            Unit = unit,
            Format = format,
            MaxLength = maxLength,
            IsSystem = true,
            IsActive = true
        };
    }
}
