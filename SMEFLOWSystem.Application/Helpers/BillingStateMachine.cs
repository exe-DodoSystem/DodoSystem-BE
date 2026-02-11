using ShareKernel.Common.Enum;
using System;

namespace SMEFLOWSystem.Application.Helpers
{
    public static class BillingStateMachine
    {
        public static bool CanSetOrderToCompleted(string currentOrderStatus)
        {
            return string.Equals(currentOrderStatus, StatusEnum.OrderPending, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentOrderStatus, "New", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanSetOrderToCancelled(string currentOrderStatus)
        {
            return string.Equals(currentOrderStatus, StatusEnum.OrderPending, StringComparison.OrdinalIgnoreCase)
            || string.Equals(currentOrderStatus, "New", StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanSetPaymentToPaid(string currentPaymentStatus)
        {
            return string.Equals(currentPaymentStatus, StatusEnum.PaymentPending, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanSetPaymentToFailed(string currentPaymentStatus)
        {
            return string.Equals(currentPaymentStatus, StatusEnum.PaymentPending, StringComparison.OrdinalIgnoreCase);
        }

        public static bool CanActivateTenant(string currentTenantStatus)
        {
            return string.Equals(currentTenantStatus, StatusEnum.TenantPending, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentTenantStatus, StatusEnum.TenantTrial, StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentTenantStatus, StatusEnum.TenantSuspended, StringComparison.OrdinalIgnoreCase);
        }
    }
}
