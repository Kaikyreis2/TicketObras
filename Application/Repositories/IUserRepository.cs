using Domain;

namespace Application;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllAsync();
    public Task<User> AddAsync(User user);
    public Task<User?> GetByIdAsync(int id);
    public Task<User?> GetByEmailAsync(string email);
    public Task<User> UpdateAsync(User user);
    public Task<User> RemoveAsync(User user);
}
