using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShareKernel.Common.Enum
{
    public static class StatusEnum
    {
        public const string TenantPending = "PendingPayment"; // Chờ thanh toán
        public const string TenantTrial = "Trial";            // Dùng thử
        public const string TenantActive = "Active";          // Đang hoạt động
        public const string TenantSuspended = "Suspended";    // Bị treo (hết hạn)

        // --- Module Subscription Status ---
        public const string ModuleTrial = "Trial";
        public const string ModuleActive = "Active";
        public const string ModuleSuspended = "Suspended";

        // --- Order Status ---
        public const string OrderPending = "Pending";
        public const string OrderPaid = "Paid";
        public const string OrderCancelled = "Cancelled";
        public const string OrderFailed = "Failed";
        public const string OrderCompleted = "Completed";
        // --- Attendance Status ---
        public const string AttendancePresent = "Present";
        public const string AttendanceAbsent = "Absent";

        // --- Payment Status ---
        public const string PaymentPending = "Pending";
        public const string PaymentPaid = "Paid";
        public const string PaymentFailed = "Failed";
    }
}
