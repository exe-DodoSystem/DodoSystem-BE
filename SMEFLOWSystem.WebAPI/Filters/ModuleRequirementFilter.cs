using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SMEFLOWSystem.Application.Interfaces.IServices;
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
                context.Result = new ObjectResult(new
                {
                    message = $"Module '{attribute.ModuleCode}' chưa được kích hoạt trong subscription của bạn."
                })
                {
                    StatusCode = 403
                };
            }
        }
    }
}
