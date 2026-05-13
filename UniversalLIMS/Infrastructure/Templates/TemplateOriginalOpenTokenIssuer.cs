using System.Buffers.Binary;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.WebUtilities;
using UniversalLIMS.Application.Templates.Abstractions;

namespace UniversalLIMS.Infrastructure.Templates;

public sealed class TemplateOriginalOpenTokenIssuer : ITemplateOriginalOpenTokenIssuer
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IDataProtector _protector;

    public TemplateOriginalOpenTokenIssuer(IDataProtectionProvider dataProtectionProvider)
    {
        _protector = dataProtectionProvider.CreateProtector("UniversalLIMS.TemplateOriginalOpen.v2026");
    }

    public string CreateToken(Guid templateVersionId)
    {
        var expiresUnix = DateTimeOffset.UtcNow.Add(Lifetime).ToUnixTimeSeconds();
        var payload = new byte[24];
        templateVersionId.TryWriteBytes(payload);
        BinaryPrimitives.WriteInt64LittleEndian(payload.AsSpan(16), expiresUnix);

        var protectedPayload = _protector.Protect(payload);
        return WebEncoders.Base64UrlEncode(protectedPayload);
    }

    public bool TryValidateToken(string token, out Guid templateVersionId)
    {
        templateVersionId = default;

        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        try
        {
            var protectedPayload = WebEncoders.Base64UrlDecode(token);
            var payload = _protector.Unprotect(protectedPayload);

            if (payload.Length != 24)
            {
                return false;
            }

            templateVersionId = new Guid(payload.AsSpan(0, 16));
            var expiresUnix = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(16));

            if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix)
            {
                templateVersionId = default;
                return false;
            }

            return true;
        }
        catch (CryptographicException)
        {
            templateVersionId = default;
            return false;
        }
        catch (FormatException)
        {
            templateVersionId = default;
            return false;
        }
    }
}
