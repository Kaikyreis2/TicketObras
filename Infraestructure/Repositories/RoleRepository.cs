using Application;
using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class RoleRepository(Context context) : IRoleRepository
{
    private readonly Context _context = context;


    public async Task<IEnumerable<Role>> GetAllAsync()
    {
        return await _context.Roles.ToListAsync();
    }

    public async Task<Role> AddAsync(Role role)
    {
        _context.Roles.Add(role);
        await _context.SaveChangesAsync();
        return role;
    }
    public async Task<Role?> GetByIdAsync(int id)
    {
        return await _context.Roles.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<Role?> GetByNameAsync(string name)
    {
        return await _context.Roles.FirstOrDefaultAsync(r => r.Name == name);
    }

    public async Task<Role> UpdateAsync(Role role)
    {
        _context.Roles.Update(role);
        await _context.SaveChangesAsync();
        return role;
    }
    public async Task<Role> DeleteAsync(Role role)
    {
        _context.Roles.Remove(role);
        await _context.SaveChangesAsync();
        return role;
    }

}
