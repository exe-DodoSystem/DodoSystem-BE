using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Application.Interfaces.IServices;
using SMEFLOWSystem.Application.Services;
using SMEFLOWSystem.SharedKernel.Common;
using SMEFLOWSystem.WebAPI.Controllers;

namespace SMEFLOWSystem.Tests;

public sealed class PhaseZeroAuthorizationContractTests
{
    [KnownBugFact("BE-AUTH-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-AUTH-01")]
    public void ModuleSubscriptionsController_RequiresAuthenticatedUser()
    {
        var authorizeAttributes = typeof(ModuleSubscriptionsController)
            .GetCustomAttributes<AuthorizeAttribute>(inherit: true);

        Assert.NotEmpty(authorizeAttributes);
    }

    [KnownBugFact("BE-AUTH-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-AUTH-01")]
    public void CancelModuleSubscription_RequiresTenantAdminPolicy()
    {
        var action = typeof(ModuleSubscriptionsController)
            .GetMethod(nameof(ModuleSubscriptionsController.CancelMyModuleSubscription));

        Assert.NotNull(action);
        Assert.Contains(
            action!.GetCustomAttributes<AuthorizeAttribute>(inherit: true),
            attribute => string.Equals(
                attribute.Policy,
                PolicyNames.TenantAdmin,
                StringComparison.Ordinal));
    }

    [KnownBugFact("BE-LEAVE-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-LEAVE-01")]
    public void LeaveRequestService_DependsOnCentralHrAuthorization()
    {
        AssertConstructorDependsOn<IHrAuthorizationService>(typeof(LeaveRequestService));
    }

    [KnownBugFact("BE-LEAVE-01")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-LEAVE-01")]
    public void LeaveRequestListQueries_AcceptDepartmentScope()
    {
        AssertMethodAcceptsDepartmentScope(
            typeof(ILeaveRequestRepository),
            nameof(ILeaveRequestRepository.GetPendingAsync));
        AssertMethodAcceptsDepartmentScope(
            typeof(ILeaveRequestRepository),
            nameof(ILeaveRequestRepository.GetAllAsync));
    }

    [KnownBugFact("BE-MGR-05")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-05")]
    public void ManualTimesheetService_DependsOnCentralHrAuthorization()
    {
        AssertConstructorDependsOn<IHrAuthorizationService>(typeof(ManualTimesheetService));
    }

    [KnownBugFact("BE-MGR-05")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-05")]
    public void ManualTimesheetMonthQuery_AcceptsDepartmentScope()
    {
        AssertMethodAcceptsDepartmentScope(
            typeof(IManualMonthlyTimesheetRepository),
            nameof(IManualMonthlyTimesheetRepository.GetByTenantMonthYearAsync));
    }

    [KnownBugFact("BE-MGR-06")]
    [Trait("Phase", "0")]
    [Trait("Gap", "BE-MGR-06")]
    public void WebApi_HasAGlobalExceptionHandler()
    {
        var handlerTypes = typeof(ModuleSubscriptionsController).Assembly
            .GetTypes()
            .Where(type =>
                !type.IsAbstract &&
                !type.IsInterface &&
                typeof(IExceptionHandler).IsAssignableFrom(type));

        Assert.NotEmpty(handlerTypes);
    }

    private static void AssertConstructorDependsOn<TDependency>(Type serviceType)
    {
        var hasDependency = serviceType
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(TDependency));

        Assert.True(
            hasDependency,
            $"{serviceType.Name} must depend on {typeof(TDependency).Name}.");
    }

    private static void AssertMethodAcceptsDepartmentScope(
        Type repositoryType,
        string methodName)
    {
        var method = repositoryType.GetMethod(methodName);

        Assert.NotNull(method);
        Assert.Contains(
            method!.GetParameters(),
            parameter =>
                parameter.Name?.Contains(
                    "department",
                    StringComparison.OrdinalIgnoreCase) == true);
    }
}
