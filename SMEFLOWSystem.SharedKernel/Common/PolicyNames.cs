using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.SharedKernel.Common
{
    public static class PolicyNames
    {
        public const string TenantAdmin = "TenantAdmin";
        public const string HrManager = "HrManager";
        public const string Manager = "Manager";
        public const string Employee = "Employee";
        public const string SystemAdmin = "SystemAdmin";

        // Composite: nhiều role cùng được phép
        public const string HrAccess = "HrAccess";       // TenantAdmin + HRManager + Manager
        public const string AdminOrHr = "AdminOrHr";      // TenantAdmin + HRManager
    }
}
