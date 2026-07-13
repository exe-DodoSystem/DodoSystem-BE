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
            var currentDirectory = Directory.GetCurrentDirectory();
            var webApiDirectory = Directory.Exists(Path.Combine(currentDirectory, "SMEFLOWSystem.WebAPI"))
                ? Path.Combine(currentDirectory, "SMEFLOWSystem.WebAPI")
                : Path.GetFullPath(Path.Combine(currentDirectory, "../SMEFLOWSystem.WebAPI"));

            var configuration = new ConfigurationBuilder()
                .SetBasePath(webApiDirectory)
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
                ?? configuration.GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException("Missing DefaultConnection connection string.");

            var optionsBuilder = new DbContextOptionsBuilder<SMEFLOWSystemContext>();
            optionsBuilder.UseNpgsql(connectionString);

            return new SMEFLOWSystemContext(optionsBuilder.Options, new DesignTimeTenantService());
        }

        private class DesignTimeTenantService : ICurrentTenantService
        {
            public Guid? TenantId => null;
            public void SetTenantId(Guid? tenantId) { }
        }
    }
}
