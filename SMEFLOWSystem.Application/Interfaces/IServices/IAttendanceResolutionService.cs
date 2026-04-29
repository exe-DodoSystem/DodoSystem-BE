namespace SMEFLOWSystem.Application.Interfaces.IServices;

public interface IAttendanceResolutionService
{
    Task ProcessUnresolvedPunchesAsync();
}