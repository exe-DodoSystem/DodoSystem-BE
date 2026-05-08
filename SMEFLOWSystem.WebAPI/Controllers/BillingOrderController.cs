using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.WebAPI.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class BillingOrderController : ControllerBase
    {
        private readonly IBillingOrderService _billingOrderService;
        private readonly ICurrentTenantService _currentTenant;
        public BillingOrderController(IBillingOrderService billingOrderService, ICurrentTenantService currentTenant)
        {
            _billingOrderService = billingOrderService;
            _currentTenant = currentTenant;
        }

        [HttpGet("/me/billing-orders")]
        public async Task<IActionResult> GetBillingOrdersByTenantAsync()
        {
            var tenantId = _currentTenant.TenantId;
            if (!tenantId.HasValue)
            {
                return NotFound();
            }
            var orders = await _billingOrderService.GetBillingOrdersAsync(tenantId.Value);
            return Ok(orders);
        }


    }
}
