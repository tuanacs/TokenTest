using System;
using System.Web;
using System.Data.Entity;


namespace TokenTest.Models
{
    public class TokenDbContext : DbContext
    {
        public TokenDbContext() : base("DefaultConnection") { Database.SetInitializer<TokenDbContext>(null); }

        public DbSet<Token> Tokens { get; set; }

        
    }
}
