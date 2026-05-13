using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Registration;
using UniversalLIMS.Application.Registration.Abstractions;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Infrastructure.Persistence;

namespace UniversalLIMS.Infrastructure.Registration;

public sealed class OrderFieldValueService : IOrderFieldValueService
{
    private static readonly HashSet<string> ReservedStaticKeys =
    [
        "Customer.FullName",
        "Customer.OrganizationName",
        "Customer.ContactPhone",
        "Sample.Number",
        "Sample.RegisteredAt",
        "Branch.Code",
        "Branch.Name",
        "Conclusion.Text"
    ];

    private readonly ApplicationDbContext _context;

    public OrderFieldValueService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task UpsertAsync(
        Guid orderId,
        IReadOnlyList<OrderFieldValueInput> values,
        CancellationToken cancellationToken = default)
    {
        if (values.Count == 0)
        {
            return;
        }

        var dataFieldIds = values.Select(value => value.DataFieldId).Distinct().ToList();
        var dataFields = await _context.DataFields
            .Where(dataField => dataFieldIds.Contains(dataField.Id))
            .Select(dataField => new { dataField.Id, dataField.Key })
            .ToListAsync(cancellationToken);

        if (dataFields.Count != dataFieldIds.Count)
        {
            throw new InvalidOperationException("Одне або кілька полів даних не знайдено.");
        }

        var reservedField = dataFields.FirstOrDefault(dataField => ReservedStaticKeys.Contains(dataField.Key));
        if (reservedField is not null)
        {
            throw new InvalidOperationException(
                $"Поле '{reservedField.Key}' зберігається у статичній колонці сутності, а не в OrderFieldValue.");
        }

        var existingValues = await _context.OrderFieldValues
            .Where(fieldValue => fieldValue.OrderId == orderId)
            .ToListAsync(cancellationToken);

        foreach (var input in values)
        {
            var existing = existingValues.FirstOrDefault(fieldValue =>
                fieldValue.DataFieldId == input.DataFieldId &&
                fieldValue.SampleId == input.SampleId);

            if (existing is null)
            {
                _context.OrderFieldValues.Add(new OrderFieldValue
                {
                    OrderId = orderId,
                    SampleId = input.SampleId,
                    DataFieldId = input.DataFieldId,
                    StoredValue = input.StoredValue?.Trim()
                });
            }
            else
            {
                existing.StoredValue = input.StoredValue?.Trim();
            }
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OrderFieldValueInput>> GetAsync(
        Guid orderId,
        Guid? sampleId = null,
        CancellationToken cancellationToken = default)
    {
        return await _context.OrderFieldValues
            .Where(fieldValue => fieldValue.OrderId == orderId && fieldValue.SampleId == sampleId)
            .Select(fieldValue => new OrderFieldValueInput
            {
                DataFieldId = fieldValue.DataFieldId,
                SampleId = fieldValue.SampleId,
                StoredValue = fieldValue.StoredValue
            })
            .ToListAsync(cancellationToken);
    }
}
