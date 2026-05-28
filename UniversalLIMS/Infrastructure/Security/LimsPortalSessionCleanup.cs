using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Security;

/// <summary>Очищення сесійного стану порталу після виходу з облікового запису.</summary>
public static class LimsPortalSessionCleanup
{
    public static void ClearAuthenticatedSessionState(ISession? session)
    {
        if (session is null)
        {
            return;
        }

        session.Remove(SessionKeys.ActiveLimsRole);
        session.Remove(SessionKeys.ActiveLaboratoryBranchId);
    }
}
