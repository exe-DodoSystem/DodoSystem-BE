using Microsoft.AspNetCore.Http;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Tenancy
{
    public class CurrentTenantService : ICurrentTenantService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private static readonly AsyncLocal<Guid?> OverrideTenantId = new();

        public CurrentTenantService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public Guid? TenantId
        {
            get
            {
                if (OverrideTenantId.Value.HasValue)
                {
                    return OverrideTenantId.Value;
                }

                var context = _httpContextAccessor.HttpContext;
                if (context == null) return null;

                // 1. Lấy từ User Claims (nếu đã login)
                var claimTenantId = context.User?.FindFirst("tenantId")?.Value;
                if (!string.IsNullOrEmpty(claimTenantId) && Guid.TryParse(claimTenantId, out var parsedId))
                {
                    return parsedId;
                }

                // 2. Lấy từ Header (lúc chưa login)
                if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerTenantId))
                {
                    if (Guid.TryParse(headerTenantId, out var headerParsedId))
                    {
                        return headerParsedId;
                    }
                }

                return null;
            }
        }

        public void SetTenantId(Guid? tenantId)
        {
            OverrideTenantId.Value = tenantId;
        }
    }
}
