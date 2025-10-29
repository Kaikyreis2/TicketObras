using Domain;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace Application
{
    /*public static class TokenService
    {
        public static string GenerateToken(User user, string jwt)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(jwt);
            var claims = new List<Claim> { new Claim(ClaimTypes.Email, user.Email) };

            claims.AddRange(user.Roles.Select(c => new Claim(ClaimTypes.Role, c.Name)));

            var tokenDescriptor = new SecurityTokenDescriptor()
            {

                Subject = new ClaimsIdentity(claims),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwt)), SecurityAlgorithms.HmacSha512),
                Expires = DateTime.UtcNow.AddDays(1)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);

        }
    }*/
}
