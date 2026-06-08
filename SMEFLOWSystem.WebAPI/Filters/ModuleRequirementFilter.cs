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

            var subscription = await _moduleSubscriptionService.GetMyByModuleCodeAsync(attribute.ModuleCode);
            if (subscription == null)
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
