using Microsoft.EntityFrameworkCore;
using SharedKernel.DTOs;
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
    public class RoleRepository : IRoleRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public RoleRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task<Role?> AddRoleAsync(Role role)
        {
            var newRole = await _context.Roles.AddAsync(role);
            await _context.SaveChangesAsync();
            return newRole.Entity;
        }

        public async Task<bool> ExistByNameAsync(string name)
        {
            return await _context.Roles.AnyAsync(r => r.Name == name); 
        }

        public async Task<List<Role>> GetAllRolesAsync()
        {
            return await _context.Roles.ToListAsync();
        }

        public async Task<Role?> GetRoleByIdAsync(int id)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
        }

        public async Task<Role?> GetRoleByNameAsync(string name)
        {
            return await _context.Roles.FirstOrDefaultAsync(r => r.Name == name);
        }

        public async Task<Role?> UpdateRoleAsync(int id, string name, string description, bool isSystemRole)
        {
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == id);
            if (role == null)
            {
                return null;
            }
            role.Name = name;
            role.Description = description;
            role.IsSystemRole = isSystemRole;

            await _context.SaveChangesAsync();
            return role;
        }

        public async Task<(List<Role> Items, int TotalCount)> GetAllRolesPagingAsync(int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Roles
                .AsNoTracking()
                .Include(u => u.UserRoles)
                .OrderBy(u => u.Id);

            var totalCount = await query.CountAsync();

            var skip = (pageNumber - 1) * pageSize;
            var items = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            return (items, totalCount);
        }

        public async Task<List<User>> GetUsersByRoleIdAsync(int roleId)
        {
            return await _context.UserRoles
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.User)
                .ToListAsync();
        }

        public Task<List<Role>> GetByIdsAsync(IEnumerable<int> roleIds)
        {
            var ids = roleIds?.Distinct().ToList() ?? new List<int>();
            if (ids.Count == 0) return Task.FromResult(new List<Role>());

            return _context.Roles
                .Where(r => ids.Contains(r.Id))
                .ToListAsync();
        }

        public async Task AddAsync(Role role)
        {
            await _context.Roles.AddAsync(role);
            await _context.SaveChangesAsync();
        }
    }
}
