using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Data
{
    public class SMEFLOWSystemContextFactory : IDesignTimeDbContextFactory<SMEFLOWSystemContext>
    {
        public SMEFLOWSystemContext CreateDbContext(string[] args)
        {
            // đọc appsettings.json từ WebAPI
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../SMEFLOWSystem.WebAPI"))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var optionsBuilder = new DbContextOptionsBuilder<SMEFLOWSystemContext>();
            optionsBuilder.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection")
            );

            return new SMEFLOWSystemContext(optionsBuilder.Options, new DesignTimeTenantService());
        }

        private class DesignTimeTenantService : ICurrentTenantService
        {
            public Guid? TenantId => null;
            public void SetTenantId(Guid? tenantId) { }
        }
    }
}
