using Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public class RoleRepository(Context context)
{
    private readonly Context _context = context;


    public async Task<IEnumerable<Role>> GetAllAsync(int id)
    {
        return await _context.Roles.ToListAsync();
    }

    public async Task<Role> Add(Role role)
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
}
