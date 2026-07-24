using Microsoft.Extensions.Logging;

namespace SMEFLOWSystem.Application.Logging;

public static class SystemAdminLogEvents
{
    public static readonly EventId TenantStatusChanged = new(5101, nameof(TenantStatusChanged));
    public static readonly EventId SubscriptionExtended = new(5102, nameof(SubscriptionExtended));
    public static readonly EventId SubscriptionSuspended = new(5103, nameof(SubscriptionSuspended));
    public static readonly EventId SubscriptionReactivated = new(5104, nameof(SubscriptionReactivated));
    public static readonly EventId ModuleUpdated = new(5105, nameof(ModuleUpdated));
}
