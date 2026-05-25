using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using UniversalLIMS.Application.Security;

namespace UniversalLIMS.Infrastructure.Filters;

/// <summary>
/// Потребує активної робочої ролі (сесія або єдина роль у Identity).
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireActiveLimsRoleAttribute : Attribute, IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var roleService = context.HttpContext.RequestServices.GetRequiredService<IActiveLimsRoleService>();
        var role = roleService.ResolveActiveRole(context.HttpContext.User);
        if (string.IsNullOrWhiteSpace(role))
        {
            if (context.Controller is Controller controller)
            {
                controller.TempData["RoleSelectError"] =
                    "Оберіть робочу роль на порталі, щоб відкрити цей розділ.";
            }

            context.Result = new RedirectToActionResult("Index", "Home", null);
            return;
        }

        await next();
    }
}
