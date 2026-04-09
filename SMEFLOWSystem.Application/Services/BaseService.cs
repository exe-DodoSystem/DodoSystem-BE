using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Application.Services
{
    public class BaseService : IBaseService
    {
        public Task<Employee> RequireEmployeeAsync(Guid userId)
        {
            throw new NotImplementedException();
        }
    }
}
