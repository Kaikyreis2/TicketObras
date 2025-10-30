using BCrypt.Net;
using static BCrypt.Net.BCrypt;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Domain;

namespace Application
{
    public class AccountService(IUserRepository repository)
    {
        private readonly IUserRepository _repository = repository;

        public async Task<User> CheckLoginAsync(string email, string password)
        {
            var user = await _repository.GetByEmailAsync(email);

            if (user is null)
                return null;

            
            if(!Verify(password, user.PasswordHash))
                return null;

            return user;
            
        }

       
    }
}
