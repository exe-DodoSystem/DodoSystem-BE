using Microsoft.EntityFrameworkCore;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class InviteRepository : IInviteRepository
    {
        private readonly SMEFLOWSystemContext _context;
        public InviteRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task AddInviteAsync(Invite invite)
        {
            await _context.Invites.AddAsync(invite);
            await _context.SaveChangesAsync();
        }

        public async Task<Invite?> GetInviteByTokenAsync(string token)
        {
            return await _context.Invites
                .IgnoreQueryFilters()
                .Include(i => i.Role)
                .Include(i => i.Department)
                .Include(i => i.Position)
                .FirstOrDefaultAsync(i => i.Token == token && !i.IsUsed && i.ExpiryDate > DateTime.UtcNow && !i.IsDeleted);
        }

        public async Task UpdateInviteAsync(Invite invite)
        {
            _context.Invites.Update(invite);
            await _context.SaveChangesAsync();
        }
    }
}
