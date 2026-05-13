using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UniversalLIMS.Application.Abstractions;
using UniversalLIMS.Application.Security;
using UniversalLIMS.Domain.Organization;
using UniversalLIMS.Domain.Registration;
using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Infrastructure.Persistence.Seed;

public sealed class DataSeeder
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ISystemOperationContext _systemOperationContext;

    public DataSeeder(
        ApplicationDbContext context,
        RoleManager<IdentityRole> roleManager,
        ISystemOperationContext systemOperationContext)
    {
        _context = context;
        _roleManager = roleManager;
        _systemOperationContext = systemOperationContext;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        using var operation = _systemOperationContext.Begin("LIMS foundation seed");

        await SeedRolesAsync();
        await SeedBranchesAsync(cancellationToken);
        await SeedDataFieldsAsync(cancellationToken);
        await SeedInvestigationTypesAsync(cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedRolesAsync()
    {
        foreach (var roleName in LimsRoles.All)
        {
            if (await _roleManager.RoleExistsAsync(roleName))
            {
                continue;
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
            if (!result.Succeeded)
            {
                var errors = string.Join("; ", result.Errors.Select(error => error.Description));
                throw new InvalidOperationException($"Failed to seed role '{roleName}': {errors}");
            }
        }
    }

    private async Task SeedBranchesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _context.Branches
            .IgnoreQueryFilters()
            .Select(branch => branch.Code)
            .ToListAsync(cancellationToken);

        var branches = new[]
        {
            new Branch
            {
                Code = "ZHY",
                Name = "Житомирський центр контролю та профілактики хвороб",
                City = "Житомир"
            },
            new Branch
            {
                Code = "BER",
                Name = "Бердичівський відділ контролю та профілактики хвороб",
                City = "Бердичів"
            },
            new Branch
            {
                Code = "KOR",
                Name = "Коростенський відділ контролю та профілактики хвороб",
                City = "Коростень"
            }
        };

        foreach (var branch in branches.Where(branch => !existingCodes.Contains(branch.Code)))
        {
            _context.Branches.Add(branch);
        }
    }

    private async Task SeedDataFieldsAsync(CancellationToken cancellationToken)
    {
        var existingKeys = await _context.DataFields
            .IgnoreQueryFilters()
            .Select(dataField => dataField.Key)
            .ToListAsync(cancellationToken);

        var dataFields = new[]
        {
            CreateSystemField("Customer.FullName", "ПІБ замовника", DataFieldType.Text, DataFieldScope.Registration, true),
            CreateSystemField("Customer.OrganizationName", "Назва організації", DataFieldType.Text, DataFieldScope.Registration, false),
            CreateSystemField("Customer.ContactPhone", "Контактний телефон", DataFieldType.Text, DataFieldScope.Registration, false),
            CreateSystemField("Branch.Code", "Код філії", DataFieldType.Text, DataFieldScope.System, true),
            CreateSystemField("Branch.Name", "Назва філії", DataFieldType.Text, DataFieldScope.System, true),
            CreateSystemField("Sample.Number", "Номер проби", DataFieldType.Text, DataFieldScope.Sample, true),
            CreateSystemField("Sample.RegisteredAt", "Дата реєстрації проби", DataFieldType.Date, DataFieldScope.Sample, true),
            CreateSystemField("Conclusion.Text", "Текст висновку", DataFieldType.Text, DataFieldScope.Conclusion, false),
            CreateDynamicRegistrationField(
                "Registration.SamplingLocation",
                "Місце відбору проби",
                DataFieldType.Text,
                false)
        };

        foreach (var dataField in dataFields.Where(dataField => !existingKeys.Contains(dataField.Key)))
        {
            _context.DataFields.Add(dataField);
        }
    }

    private static DataField CreateDynamicRegistrationField(
        string key,
        string displayNameUk,
        DataFieldType fieldType,
        bool isRequired)
    {
        return new DataField
        {
            Key = key,
            DisplayNameUk = displayNameUk,
            FieldType = fieldType,
            Scope = DataFieldScope.Registration,
            IsRequired = isRequired,
            IsSystem = false,
            IsActive = true,
            MaxLength = 500
        };
    }

    private async Task SeedInvestigationTypesAsync(CancellationToken cancellationToken)
    {
        var existingCodes = await _context.InvestigationTypes
            .IgnoreQueryFilters()
            .Select(item => item.Code)
            .ToListAsync(cancellationToken);

        var investigationTypes = new[]
        {
            new InvestigationType
            {
                Code = "WATER",
                NameUk = "Дослідження води",
                DescriptionUk = "Базовий тип дослідження для реєстратури",
                SortOrder = 1
            },
            new InvestigationType
            {
                Code = "FOOD",
                NameUk = "Дослідження харчових продуктів",
                SortOrder = 2
            }
        };

        foreach (var investigationType in investigationTypes.Where(item => !existingCodes.Contains(item.Code)))
        {
            _context.InvestigationTypes.Add(investigationType);
        }

        await _context.SaveChangesAsync(cancellationToken);

        var publishedTemplates = await _context.Templates
            .Where(template => template.CurrentPublishedVersionId != null)
            .Select(template => new { template.Id, template.Code })
            .ToListAsync(cancellationToken);

        if (publishedTemplates.Count == 0)
        {
            return;
        }

        var investigationTypeIds = await _context.InvestigationTypes
            .Select(item => new { item.Id, item.Code })
            .ToListAsync(cancellationToken);

        var waterTypeId = investigationTypeIds.FirstOrDefault(item => item.Code == "WATER")?.Id;
        if (waterTypeId is null)
        {
            return;
        }

        var existingLinks = await _context.InvestigationTypeTemplates
            .Select(link => new { link.InvestigationTypeId, link.TemplateId })
            .ToListAsync(cancellationToken);

        var sortOrder = 1;
        foreach (var template in publishedTemplates)
        {
            if (existingLinks.Any(link => link.InvestigationTypeId == waterTypeId && link.TemplateId == template.Id))
            {
                continue;
            }

            _context.InvestigationTypeTemplates.Add(new InvestigationTypeTemplate
            {
                InvestigationTypeId = waterTypeId.Value,
                TemplateId = template.Id,
                SortOrder = sortOrder++
            });
        }
    }

    private static DataField CreateSystemField(
        string key,
        string displayNameUk,
        DataFieldType fieldType,
        DataFieldScope scope,
        bool isRequired)
    {
        return new DataField
        {
            Key = key,
            DisplayNameUk = displayNameUk,
            FieldType = fieldType,
            Scope = scope,
            IsRequired = isRequired,
            IsSystem = true,
            IsActive = true
        };
    }
}
