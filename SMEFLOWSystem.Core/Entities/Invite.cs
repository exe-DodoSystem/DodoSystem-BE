using SMEFLOWSystem.SharedKernel.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Core.Entities
{
    /// <summary>
    /// Quản lý các lời mời tham gia hệ thống cho nhân viên mới.
    /// </summary>
    public partial class Invite : ITenantEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();  // Thêm Id (GUID default)
        public Guid TenantId { get; set; }
        /// <summary>Email người được mời.</summary>
        public string Email { get; set; } = string.Empty;
        /// <summary>Phân quyền (Role) gán sẵn khi nhân viên đồng ý.</summary>
        public int RoleId { get; set; }
        /// <summary>Phòng ban gán sẵn khi nhân viên đồng ý.</summary>
        public Guid? DepartmentId { get; set; }
        /// <summary>Chức vụ gán sẵn khi nhân viên đồng ý.</summary>
        public Guid? PositionId { get; set; }
        /// <summary>Mã token xác nhận lời mời.</summary>
        public string Token { get; set; } = string.Empty;
        /// <summary>Thời hạn của lời mời.</summary>
        public DateTime ExpiryDate { get; set; }
        /// <summary>Trạng thái lời mời đã sử dụng hay chưa.</summary>
        public bool IsUsed { get; set; } = false;
        public Guid? InvitedByUserId { get; set; }
        public string? Message { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;  // Thêm CreatedAt
        public DateTime? UpdatedAt { get; set; }  // Thêm UpdatedAt (nullable)
        public bool IsDeleted { get; set; } = false;  // Thêm IsDeleted
        public virtual Tenant Tenant { get; set; } = null!;
        public virtual Role Role { get; set; } = null!;
        public virtual Department? Department { get; set; }
        public virtual Position? Position { get; set; }
    }
}