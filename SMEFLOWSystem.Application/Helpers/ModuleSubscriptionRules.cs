using ShareKernel.Common.Enum;
using SMEFLOWSystem.Core.Entities;

namespace SMEFLOWSystem.Application.Helpers;

public static class ModuleSubscriptionRules
{
    public static bool IsUsable(ModuleSubscription? subscription, DateTime nowUtc)
    {
        return subscription != null
            && !subscription.IsDeleted
            && (string.Equals(subscription.Status, StatusEnum.ModuleActive, StringComparison.OrdinalIgnoreCase)
                || string.Equals(subscription.Status, StatusEnum.ModuleTrial, StringComparison.OrdinalIgnoreCase))
            && subscription.StartDate <= nowUtc
            && subscription.EndDate > nowUtc;
    }
}
