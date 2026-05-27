using Microsoft.AspNetCore.Identity.UI.Services;

namespace UniversalLIMS.Infrastructure.Identity;

/// <summary>Заглушка для листів Identity (лог у консоль у Development).</summary>
public sealed class IdentityEmailSender : IEmailSender
{
    private readonly ILogger<IdentityEmailSender> _logger;

    public IdentityEmailSender(ILogger<IdentityEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        _logger.LogInformation("Identity email to {Email}: {Subject}", email, subject);
        return Task.CompletedTask;
    }
}
