using UniversalLIMS.Application.Home;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Tests.Home;

public sealed class WorkspaceNavigationCatalogTests
{
    [Fact]
    public void LaboratoryTechnician_HasJournalNavAndQuickLink()
    {
        var navItems = WorkspaceNavigationCatalog.GetNavItems(LimsRoles.LaboratoryTechnician);
        var quickLinks = WorkspaceNavigationCatalog.GetQuickLinks(LimsRoles.LaboratoryTechnician);

        Assert.Contains(navItems, item =>
            item.Controller == "Laboratory" && item.Action == "Index");

        var journalLink = Assert.Single(quickLinks, link => link.Title == "Лабораторний журнал");
        Assert.True(journalLink.IsAvailable);
        Assert.Equal("/Laboratory", journalLink.Url);

        var resultsLink = Assert.Single(quickLinks, link => link.Title == "Результати");
        Assert.True(resultsLink.IsAvailable);
        Assert.Equal("/Laboratory", resultsLink.Url);
    }

    [Fact]
    public void Specialist_InDevelopment_HasLaboratoryDemoQuickLink()
    {
        var quickLinks = WorkspaceNavigationCatalog.GetQuickLinks(LimsRoles.Specialist, isDevelopment: true);

        var journalLink = Assert.Single(quickLinks, link => link.Title == "Лабораторний журнал");
        Assert.True(journalLink.IsAvailable);
        Assert.Equal("/Laboratory", journalLink.Url);
    }

    [Fact]
    public void Specialist_InProduction_HasOnlyComingSoonLinks()
    {
        var quickLinks = WorkspaceNavigationCatalog.GetQuickLinks(LimsRoles.Specialist, isDevelopment: false);

        Assert.Equal(2, quickLinks.Count);
        Assert.All(quickLinks, link => Assert.False(link.IsAvailable));
    }
}
