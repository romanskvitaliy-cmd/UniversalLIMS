namespace UniversalLIMS.Application.Security;

/// <summary>Налаштування порталу ролей та входу.</summary>
public sealed class LimsPortalOptions
{
    public const string SectionName = "LimsPortal";

    /// <summary>
    /// Якщо у користувача лише одна доступна робоча роль — одразу відкривати workspace без порталу.
    /// </summary>
    public bool AutoRedirectSingleRole { get; set; } = true;
}
