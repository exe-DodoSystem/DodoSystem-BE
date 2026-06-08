using System;

namespace SMEFLOWSystem.WebAPI.Filters
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class RequireModuleAttribute : Attribute
    {
        public string ModuleCode { get; }

        public RequireModuleAttribute(string moduleCode)
        {
            ModuleCode = moduleCode;
        }
    }
}
