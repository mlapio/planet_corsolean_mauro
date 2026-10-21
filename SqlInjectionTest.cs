using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

namespace PlanetCorsolean.Tests
{
    public class SqlInjectionDemo
    {
        private readonly DbContext _context;

        public SqlInjectionDemo(DbContext context)
        {
            _context = context;
        }

        public async Task Vulnerable(string userName)
        {
            string query = $"SELECT * FROM Users WHERE Name = '{userName}'";
            await _context.Database.ExecuteSqlRawAsync(query);
        }

        public async Task Secure(string userName)
        {
            string query = "SELECT * FROM Users WHERE Name = {0}";
            await _context.Database.ExecuteSqlRawAsync(query, userName);
        }
    }

public void GetUserData(HttpRequest request, SqlConnection connection)
{
    string userId = request.QueryString["id"];
    string query = "SELECT * FROM Users WHERE Id = " + userId;
    SqlCommand command = new SqlCommand(query, connection);
    command.ExecuteReader();
}
}

}




