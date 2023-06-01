using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System;
using System.Collections.Generic;
using System.Data.SqlTypes;

namespace OCEAdmin.Panel
{
    public static class WebToken
    {
        private static readonly string key = "your_secret_key_here";

        public static Dictionary<string, string> Storage = new Dictionary<string, string>();

        public static async Task<string> Create(string gameID)
        {
            var tokenHandler = new JwtSecurityTokenHandler();

            var claims = new[]
            {
                new Claim("gameID", gameID)
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddDays(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.ASCII.GetBytes(key)), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(descriptor);
            var jwt = tokenHandler.WriteToken(token);

            Storage.Add(gameID, jwt);

            return jwt;
        }
    }
}
