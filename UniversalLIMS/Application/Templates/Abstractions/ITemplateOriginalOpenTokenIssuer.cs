namespace UniversalLIMS.Application.Templates.Abstractions;

public interface ITemplateOriginalOpenTokenIssuer
{
    string CreateToken(Guid templateVersionId);

    bool TryValidateToken(string token, out Guid templateVersionId);
}
