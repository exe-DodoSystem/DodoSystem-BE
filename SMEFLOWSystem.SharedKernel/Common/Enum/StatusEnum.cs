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

        // Module Subscription Status 
        public const string ModuleTrial = "Trial";
        public const string ModuleActive = "Active";
        public const string ModuleSuspended = "Suspended";

        // Order Status
        public const string OrderPending = "Pending";
        public const string OrderPaid = "Paid";
        public const string OrderCancelled = "Cancelled";
        public const string OrderFailed = "Failed";
        public const string OrderCompleted = "Completed";

        // Payment Status 
        public const string PaymentPending = "Pending";
        public const string PaymentPaid = "Paid";
        public const string PaymentFailed = "Failed";

        // Employee Status 
        public const string EmployeeWorking = "Working";
        public const string EmployeeResigned = "Resigned";
        public const string EmployeeOnLeave = "OnLeave";
        public const string EmployeeProbation = "Probation";

        // Attendance status
        public const string AttendancePresent = "Present";
        public const string AttendanceLate = "Late";
        public const string AttendanceEarlyLeave = "EarlyLeave";
        public const string AttendanceAbsent = "Absent";

        // Approval status
        public const string ApprovalPending = "PendingApproval";
        public const string ApprovalApproved = "Approved";
        public const string ApprovalRejected = "Rejected";

        // Payroll Status
        public const string PayrollDraft = "Draft";
        public const string PayrollApproved = "Approved";
        public const string PayrollPaid = "Paid";
        public const string PayrollRejected = "Rejected";

        // Outbox Message Status
        public const string OutboxPending = "Pending";
        public const string OutboxProcessed = "Processed";
        public const string OutboxFailed = "Failed";
        public const string OutboxProcessing = "Processing";

    }
}
