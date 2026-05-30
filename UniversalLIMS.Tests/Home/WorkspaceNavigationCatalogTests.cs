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
    public void Specialist_HasExpertQuickLinks()
    {
        var navItems = WorkspaceNavigationCatalog.GetNavItems(LimsRoles.Specialist);
        var quickLinks = WorkspaceNavigationCatalog.GetQuickLinks(LimsRoles.Specialist);

        Assert.Contains(navItems, item =>
            item.Controller == "Expert" && item.Action == "Index");

        Assert.Equal(2, quickLinks.Count);
        Assert.All(quickLinks, link => Assert.True(link.IsAvailable));

        var queueLink = Assert.Single(quickLinks, link => link.Title == "Черга експерта");
        Assert.Equal("/Expert", queueLink.Url);

        var approvedLink = Assert.Single(quickLinks, link => link.Title == "Затверджені");
        Assert.Equal("/Expert?reviewStatus=2", approvedLink.Url);
    }

    [Fact]
    public void SystemAdministrator_HasBranchesNavAndQuickLink()
    {
        var navItems = WorkspaceNavigationCatalog.GetNavItems(LimsRoles.SystemAdministrator);
        var quickLinks = WorkspaceNavigationCatalog.GetQuickLinks(LimsRoles.SystemAdministrator);

        Assert.Contains(navItems, item =>
            item.Controller == "Branches" && item.Action == "Index");

        Assert.Contains(navItems, item =>
            item.Controller == "Users" && item.Action == "Index");

        var branchesLink = Assert.Single(quickLinks, link => link.Title == "Філії");
        Assert.True(branchesLink.IsAvailable);
        Assert.Equal("/Branches", branchesLink.Url);

        var usersLink = Assert.Single(quickLinks, link => link.Title == "Користувачі");
        Assert.True(usersLink.IsAvailable);
        Assert.Equal("/Users", usersLink.Url);
    }
}
