using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Infrastructure.Data;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Tests;

internal static class PhaseZeroTestContext
{
    public static SMEFLOWSystemContext Create(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<SMEFLOWSystemContext>()
            .UseInMemoryDatabase($"phase-zero-{Guid.NewGuid():N}")
            .Options;

        return new SMEFLOWSystemContext(options, new MutableCurrentTenantService(tenantId));
    }

    internal sealed class MutableCurrentTenantService : ICurrentTenantService
    {
        public MutableCurrentTenantService(Guid? tenantId)
        {
            TenantId = tenantId;
        }

        public Guid? TenantId { get; private set; }

        public void SetTenantId(Guid? tenantId)
        {
            TenantId = tenantId;
        }
    }
}
