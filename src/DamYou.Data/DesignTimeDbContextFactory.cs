using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DamYou.Data;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<DamYouDbContext>
{
    public DamYouDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DamYouDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new DamYouDbContext(options);
    }
}
