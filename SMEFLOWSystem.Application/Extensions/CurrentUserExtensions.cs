using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.SharedKernel.Interfaces;

namespace SMEFLOWSystem.Application.Extensions;

public static class CurrentUserExtensions
{
    /// <summary>TenantAdmin — quyền quản trị đầy đủ</summary>
    public static bool IsAdmin(this ICurrentUserService user)
        => user.IsInRole(RoleConstants.TenantAdmin);

    /// <summary>Manager — quản lý phòng ban</summary>
    public static bool IsManager(this ICurrentUserService user)
        => user.IsInRole(RoleConstants.Manager);

    //<summary>HRManager — quản lý nhân sự</summary>
    public static bool IsHrManager(this ICurrentUserService user)
       => user.IsInRole(RoleConstants.HrManager);


    /// <summary>Employee — nhân viên thông thường</summary>
    public static bool IsEmployee(this ICurrentUserService user)
        => user.IsInRole(RoleConstants.Employee);

    /// <summary>Có quyền HR (Manager trở lên)</summary>
    public static bool HasHrAccess(this ICurrentUserService user)
        => user.IsAdmin() || user.IsManager();

    /// <summary>Throw nếu chưa đăng nhập</summary>
    public static Guid RequireUserId(this ICurrentUserService user)
        => user.UserId ?? throw new UnauthorizedAccessException("Unauthenticated");

    /// <summary>Throw nếu không phải Admin</summary>
    public static void EnsureAdmin(this ICurrentUserService user)
    {
        if (!user.IsAdmin()) throw new UnauthorizedAccessException("Forbidden");
    }

    /// <summary>Throw nếu không có quyền HR</summary>
    public static void EnsureHrAccess(this ICurrentUserService user)
    {
        if (!user.HasHrAccess()) throw new UnauthorizedAccessException("Forbidden");
    }
}
