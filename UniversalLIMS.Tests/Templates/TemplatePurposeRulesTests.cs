using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Tests.Templates;

public sealed class TemplatePurposeRulesTests
{
    [Theory]
    [InlineData("REF-MOZ-001", TemplatePurpose.Referral)]
    [InlineData("ref-water", TemplatePurpose.Referral)]
    [InlineData("REF", TemplatePurpose.Referral)]
    [InlineData("REF_MOZ", TemplatePurpose.Referral)]
    [InlineData("f327_water", TemplatePurpose.Protocol)]
    [InlineData("Food_001", TemplatePurpose.Protocol)]
    [InlineData("PDF-SAVE", TemplatePurpose.Protocol)]
    [InlineData("CONCLUSION-001", TemplatePurpose.Conclusion)]
    [InlineData("", TemplatePurpose.Protocol)]
    public void InferFromCode_ReturnsExpectedPurpose(string code, TemplatePurpose expected) =>
        Assert.Equal(expected, TemplatePurposeRules.InferFromCode(code));
}
