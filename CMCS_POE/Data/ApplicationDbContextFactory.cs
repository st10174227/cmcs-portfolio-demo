using CMCS_POE.Security;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;

namespace CMCS_POE.Data;

public sealed class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var directory = Environment.GetEnvironmentVariable("CMCS_PERSONAL_DATA_DIR")
            ?? throw new InvalidOperationException("Set CMCS_PERSONAL_DATA_DIR through scripts/Initialize-Authentication.ps1.");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(
            new SqliteConnectionStringBuilder { DataSource = Path.Combine(directory, "cmcs-personal.db") }.ToString()));
        services.AddPersonalIdentity(development: true);
        // EF tooling owns the returned context for the duration of this short-lived process.
        return services.BuildServiceProvider().GetRequiredService<ApplicationDbContext>();
    }
}
