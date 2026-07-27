using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.WebAPI.Exceptions;
using System.Linq;
using System.Threading.Tasks;

namespace SMEFLOWSystem.WebAPI.Filters
{
    public class ModuleRequirementFilter : IAsyncAuthorizationFilter
    {
        private readonly IModuleSubscriptionService _moduleSubscriptionService;

        public ModuleRequirementFilter(IModuleSubscriptionService moduleSubscriptionService)
        {
            _moduleSubscriptionService = moduleSubscriptionService;
        }

        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            var attribute = context.ActionDescriptor.EndpointMetadata
                .OfType<RequireModuleAttribute>()
                .FirstOrDefault();

            if (attribute == null)
            {
                return;
            }

            // Let the endpoint's authentication/authorization policy produce 401.
            if (context.HttpContext.User.Identity?.IsAuthenticated != true)
                return;

            var hasAccess = await _moduleSubscriptionService.HasUsableModuleAsync(attribute.ModuleCode);
            if (!hasAccess)
            {
                var problem = ApiProblemDetailsFactory.Create(
                    context.HttpContext,
                    StatusCodes.Status403Forbidden,
                    "Forbidden",
                    $"Module '{attribute.ModuleCode}' chưa được kích hoạt trong subscription của bạn.",
                    "MODULE_ACCESS_FORBIDDEN");
                context.Result =
                    ApiProblemDetailsFactory.CreateResult(problem);
            }
        }
    }
}
