using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;

namespace SMEFLOWSystem.Infrastructure.Repositories;

public class RawPunchLogRepository : IRawPunchLogRepository
{
    private readonly SMEFLOWSystemContext _context;

    public RawPunchLogRepository(SMEFLOWSystemContext context)
    {
        _context = context;
    }

    public async Task AddAsync(RawPunchLog punchLog)
    {
        await _context.RawPunchLogs.AddAsync(punchLog);
        await _context.SaveChangesAsync();
    }
}