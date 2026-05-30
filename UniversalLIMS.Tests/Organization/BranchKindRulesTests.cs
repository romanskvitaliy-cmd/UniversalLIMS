using UniversalLIMS.Domain.Organization;

namespace UniversalLIMS.Tests.Organization;

public sealed class BranchKindRulesTests
{
    [Theory]
    [InlineData("REG-ZHY", BranchKind.Registration)]
    [InlineData("reg-ber", BranchKind.Registration)]
    [InlineData("REG", BranchKind.Registration)]
    [InlineData("LAB-BACT-ZHY", BranchKind.Laboratory)]
    [InlineData("EXP-ZHY", BranchKind.Expert)]
    [InlineData("EXP", BranchKind.Expert)]
    [InlineData("MIX-BER", BranchKind.Mixed)]
    [InlineData("ZHY", BranchKind.Laboratory)]
    [InlineData("BER", BranchKind.Laboratory)]
    public void InferFromCode_ReturnsExpectedKind(string code, BranchKind expected) =>
        Assert.Equal(expected, BranchKindRules.InferFromCode(code));
}
