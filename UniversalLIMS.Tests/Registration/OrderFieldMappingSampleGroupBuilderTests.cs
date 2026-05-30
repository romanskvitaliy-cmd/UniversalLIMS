using UniversalLIMS.Application.Registration;

namespace UniversalLIMS.Tests.Registration;

public sealed class OrderFieldMappingSampleGroupBuilderTests
{
    [Fact]
    public void Build_GroupsReferralAndProtocolPerSample()
    {
        var referralVersionId = Guid.NewGuid();
        var protocolVersionId = Guid.NewGuid();
        var waterTypeId = Guid.NewGuid();

        var mapping = new OrderFieldMappingPrepareDto
        {
            Templates =
            [
                CreateTemplate(referralVersionId, "REF-MOZ-001", "REF_SamplingDate"),
                CreateTemplate(protocolVersionId, "f327", "f327_SamplingDate")
            ]
        };

        var form = new OrderCreateFormDto
        {
            InvestigationTypes =
            [
                new InvestigationTypeOptionDto
                {
                    Id = waterTypeId,
                    Code = "WATER",
                    NameUk = "Дослідження води"
                }
            ],
            TemplateOptions = [],
            ReferralTemplateOptions = [],
            Branches = []
        };

        var samples = new List<OrderCreateSampleInput>
        {
            new()
            {
                InvestigationTypeId = waterTypeId,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolVersionId]
            }
        };

        var groups = OrderFieldMappingSampleGroupBuilder.Build(samples, form, mapping);

        Assert.Single(groups);
        Assert.Equal("Дослідження води", groups[0].Label);
        Assert.Equal(2, groups[0].Templates.Count);
        Assert.Equal("Направлення", groups[0].Templates[0].DocumentRoleUk);
        Assert.Equal("Протокол", groups[0].Templates[1].DocumentRoleUk);
    }

    [Fact]
    public void Build_MultipleSamples_UseNumberedLabels()
    {
        var referralVersionId = Guid.NewGuid();
        var protocolA = Guid.NewGuid();
        var protocolB = Guid.NewGuid();
        var typeA = Guid.NewGuid();
        var typeB = Guid.NewGuid();

        var mapping = new OrderFieldMappingPrepareDto
        {
            Templates =
            [
                CreateTemplate(referralVersionId, "REF-MOZ-001", "REF_SamplingDate"),
                CreateTemplate(protocolA, "f327", "f327_SamplingDate"),
                CreateTemplate(protocolB, "Food", "Food_SamplingDate")
            ]
        };

        var form = new OrderCreateFormDto
        {
            InvestigationTypes =
            [
                new InvestigationTypeOptionDto { Id = typeA, Code = "WATER", NameUk = "Вода" },
                new InvestigationTypeOptionDto { Id = typeB, Code = "FOOD", NameUk = "Харчові" }
            ],
            TemplateOptions = [],
            ReferralTemplateOptions = [],
            Branches = []
        };

        var samples = new List<OrderCreateSampleInput>
        {
            new()
            {
                InvestigationTypeId = typeA,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolA]
            },
            new()
            {
                InvestigationTypeId = typeB,
                ReferralTemplateVersionId = referralVersionId,
                SelectedTemplateVersionIds = [protocolB]
            }
        };

        var groups = OrderFieldMappingSampleGroupBuilder.Build(samples, form, mapping);

        Assert.Equal(2, groups.Count);
        Assert.Equal("Проба 1 — Вода", groups[0].Label);
        Assert.Equal("Проба 2 — Харчові", groups[1].Label);
        Assert.All(groups, group => Assert.Equal(2, group.Templates.Count));
    }

    private static OrderFieldMappingTemplateDto CreateTemplate(
        Guid templateVersionId,
        string nameUk,
        string tag) =>
        new()
        {
            TemplateVersionId = templateVersionId,
            TemplateNameUk = nameUk,
            VersionNumber = 1,
            Fields =
            [
                new OrderFieldMappingFieldDto
                {
                    TemplateFieldId = Guid.NewGuid(),
                    Tag = tag,
                    Title = tag,
                    CanRead = true,
                    CanWrite = true
                }
            ]
        };
}
