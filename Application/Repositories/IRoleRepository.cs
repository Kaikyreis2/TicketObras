using Domain;

namespace Application;

public interface IRoleRepository
{
    public Task<IEnumerable<Role>> GetAllAsync();
    public Task<Role> GetByIdAsync(int id);
    public Task<Role> GetByNameAsync(string name);
    public Task<Role> AddAsync(Role role);
    public Task<Role> UpdateAsync(Role role);
    public Task<Role> DeleteAsync(Role role);
}
