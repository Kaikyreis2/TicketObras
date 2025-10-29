using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain
{
    public class User
    {
        public int Id { get; set; }
        public string PasswordHash { get; set; }
        public string Email { get; set; }
        public bool IsValid { get; set; } = false;
        public List<Role> Roles { get; set; } = [];
    }
}
