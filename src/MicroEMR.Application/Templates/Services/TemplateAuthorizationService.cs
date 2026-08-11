using MicroEMR.Application.PatientDocuments.Contracts;
using MicroEMR.Application.Templates.Contracts;

namespace MicroEMR.Application.Templates.Services;

public interface ITemplateAuthorizationService
{
    bool CanView(DocumentTemplateDetailsResponse template, TemplateAccessContext context);
    bool CanMutate(DocumentTemplateDetailsResponse template, TemplateAccessContext context);
    void EnsureCanCreate(string scope, long? ownerUserId, TemplateAccessContext context);
}

public sealed class TemplateAuthorizationService : ITemplateAuthorizationService
{
    public bool CanView(DocumentTemplateDetailsResponse template, TemplateAccessContext context) =>
        !template.TemplateScope.Equals("Personal", StringComparison.OrdinalIgnoreCase)
        || template.OwnerUserId == context.UserId || context.IsClinicAdministrator;

    public bool CanMutate(DocumentTemplateDetailsResponse template, TemplateAccessContext context) => template.TemplateScope switch
    {
        "System" => false,
        "Clinic" => context.IsClinicAdministrator,
        "Personal" => template.OwnerUserId == context.UserId || context.IsClinicAdministrator,
        _ => false
    };

    public void EnsureCanCreate(string scope, long? ownerUserId, TemplateAccessContext context)
    {
        if (scope == "System") throw new UnauthorizedAccessException("Tenant users cannot create system templates.");
        if (scope == "Clinic" && !context.IsClinicAdministrator) throw new UnauthorizedAccessException("Clinic template administration is required.");
        if (scope == "Personal" && (!ownerUserId.HasValue || (ownerUserId != context.UserId && !context.IsClinicAdministrator))) throw new UnauthorizedAccessException("Personal templates require an authorized owner.");
        if (scope is not ("Clinic" or "Personal")) throw new ArgumentException("Template scope must be Clinic or Personal.", nameof(scope));
    }
}
