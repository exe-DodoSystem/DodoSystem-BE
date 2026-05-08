using Microsoft.EntityFrameworkCore;
using SharedKernel.DTOs;
using SMEFLOWSystem.Application.Interfaces.IRepositories;
using SMEFLOWSystem.Core.Entities;
using SMEFLOWSystem.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace SMEFLOWSystem.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly SMEFLOWSystemContext _context;

        public UserRepository(SMEFLOWSystemContext context)
        {
            _context = context;
        }

        public async Task<User?> AddUserAsync(User user)
        {
            var newUser = await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
            return newUser.Entity;
        }

        public async Task<(List<User> Items, int TotalCount)> GetAllUserPagingAsync(int pageNumber, int pageSize)
        {
            if (pageNumber < 1) pageNumber = 1;
            if (pageSize < 1) pageSize = 10;

            var query = _context.Users.AsNoTracking()
                .Include(x => x.Tenant)
                .OrderBy(x => x.CreatedAt);

            var totalCount = await query.CountAsync();
            var skip = (pageNumber - 1) * pageSize;
            var users = await query
                .Skip(skip)
                .Take(pageSize)
                .ToListAsync();

            return (users, totalCount);
        }

        public async Task<List<User>> GetAllUsersAsync()
        {
            return await _context.Users
                .AsNoTracking()
                .Include(x => x.Tenant)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsSplitQuery()
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();
        }

        public async Task<User?> GetUserByEmailAsync(string email)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(x => x.Tenant)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user != null)
            {
                // Load UserRoles separately with IgnoreQueryFilters
                var userRoles = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.UserId == user.Id)
                    .Include(ur => ur.Role)
                    .ToListAsync();

                user.UserRoles = userRoles;
            }

            return user;
        }

        public async Task<User?> GetUserByIdAsync(Guid id)
        {
            return await _context.Users
                .Include(x => x.Tenant)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(x => x.Employees)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<List<User>> GetUserByNameAsync(string name)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(u => u.FullName.Contains(name))
                .Include(x => x.Tenant)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsSplitQuery()
                .ToListAsync();
        }      

        public async Task<bool> IsEmailExistAsync(string email)
        {
            var normalized = NormalizeEmail(email);
            if (string.IsNullOrEmpty(normalized)) return false;

            return await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.Email != null && u.Email.ToLower() == normalized);
        }

        private static string NormalizeEmail(string email)
        {
            return (email ?? string.Empty).Trim().ToLowerInvariant();
        }

        public async Task<User?> UpdatePasswordAsync(Guid id, string password)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            if (user == null)
            {
                return null;
            }
            user.PasswordHash = password;
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User?> UpdatePasswordIgnoreTenantAsync(Guid id, string password)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return null;
            }

            user.PasswordHash = password;
            await _context.SaveChangesAsync();
            return user;
        }

        public async Task<User?> UpdateUserAsync(User user)
        {
            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existingUser == null) return null;

            existingUser.FullName = user.FullName;
            existingUser.Phone = user.Phone;
            existingUser.IsActive = user.IsActive;
            existingUser.IsDeleted = user.IsDeleted;

            await _context.SaveChangesAsync();
            return existingUser;

        }

        public async Task<User?> GetByIdIgnoreTenantAsync(Guid id)
        {
            var user = await _context.Users
                .IgnoreQueryFilters()
                .Include(x => x.Tenant)
                .Include(x => x.Employees)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user != null)
            {
                // Load UserRoles separately with IgnoreQueryFilters
                var userRoles = await _context.UserRoles
                    .IgnoreQueryFilters()
                    .Where(ur => ur.UserId == user.Id)
                    .Include(ur => ur.Role)
                    .ToListAsync();

                user.UserRoles = userRoles;
            }

            return user;
        }

        public async Task<User?> UpdateUserIgnoreTenantAsync(User user)
        {
            var existingUser = await _context.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == user.Id);
            if (existingUser == null) return null;

            existingUser.FullName = user.FullName;
            existingUser.Phone = user.Phone;
            existingUser.IsActive = user.IsActive;
            existingUser.IsDeleted = user.IsDeleted;

            await _context.SaveChangesAsync();
            return existingUser;
        }

        public async Task<bool?> CheckUserIsDeleted(Guid id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
            return user?.IsDeleted;
        }

        public async Task<List<Role>> GetRolesByUserIdAsync(Guid userId)
        {
            var user = await _context.Users
                .AsNoTracking()
                .Include(u => u.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == userId);

            return user?.UserRoles.Select(ur => ur.Role).ToList() ?? new List<Role>();
        }

        public async Task<bool> AddRoleToUserAsync(Guid userId, int roleId)
        {
            var exists = await _context.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (exists) return false;

            var userRole = new UserRole { UserId = userId, RoleId = roleId };
            await _context.UserRoles.AddAsync(userRole);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> RemoveRoleFromUserAsync(Guid userId, int roleId)
        {
            var userRole = await _context.UserRoles.FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);
            if (userRole == null) return false;

            _context.UserRoles.Remove(userRole);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task AddAsync(User user)
        {
            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();
        }

        public async Task<User> GetOwnerUserByIdAsync(Guid? ownerUserId)
        {
            var user = await _context.Users
                .Include(x => x.Tenant)
                .Include(x => x.UserRoles)
                    .ThenInclude(ur => ur.Role)
                .Include(x => x.Employees)
                .AsSplitQuery()
                .FirstOrDefaultAsync(u => u.Id == ownerUserId);

            return user!;
        }
    }
}
