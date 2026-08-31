using Microsoft.EntityFrameworkCore;
using OneJevelsCompany.Web.Data;

namespace OneJewelsCompany.UnitTests;

internal static class TestDb
{
    public static AppDbContext Create()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }
}
