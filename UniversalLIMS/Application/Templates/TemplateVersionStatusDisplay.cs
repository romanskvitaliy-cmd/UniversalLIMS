using UniversalLIMS.Domain.Templates;

namespace UniversalLIMS.Application.Templates;

public static class TemplateVersionStatusDisplay
{
    public static string ToUk(TemplateVersionStatus status) =>
        status switch
        {
            TemplateVersionStatus.Draft => "Чернетка",
            TemplateVersionStatus.ReadyForPublication => "Готовий до публікації",
            TemplateVersionStatus.Published => "Опубліковано",
            TemplateVersionStatus.Superseded => "Замінено",
            TemplateVersionStatus.Annulled => "Анульовано",
            _ => status.ToString()
        };

    public static string BadgeClass(TemplateVersionStatus status) =>
        status switch
        {
            TemplateVersionStatus.Draft => "lims-status-badge--draft",
            TemplateVersionStatus.ReadyForPublication => "lims-status-badge--pending",
            TemplateVersionStatus.Published => "lims-status-badge--registered",
            TemplateVersionStatus.Superseded => "lims-status-badge--neutral",
            TemplateVersionStatus.Annulled => "lims-status-badge--neutral",
            _ => "lims-status-badge--neutral"
        };
}
