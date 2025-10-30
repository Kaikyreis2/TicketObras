using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Repositories
{
    public interface IUserRoleRepository
    {
        public Task<IEnumerable<Role>> GetUserRoles(User user);
        public Task<IEnumerable<Role>> GetUsersInRole(Role role);
        public Task<IEnumerable<Role>> AddUserInRole(User user, Role role);

    }
}
