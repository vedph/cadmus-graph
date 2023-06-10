using Fusi.Tools.Configuration;
using Microsoft.EntityFrameworkCore;
using System;

namespace Cadmus.Graph.Ef.PgSql;

[Tag("graph-repository.ef-pg")]
public sealed class EfPgGraphRepository : EfGraphRepository
{
    /// <summary>
    /// Gets a new DB context configured for <see cref="ConnectionString" />.
    /// </summary>
    /// <returns>DB context.</returns>
    /// <exception cref="InvalidOperationException">No connection string
    /// configured for graph repository</exception>
    protected override CadmusGraphDbContext GetContext()
    {
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException(
                "No connection string configured for graph repository");
        }

        DbContextOptionsBuilder<CadmusGraphDbContext> optionsBuilder = new();
        optionsBuilder.UseNpgsql(ConnectionString);
        return new CadmusGraphDbContext(optionsBuilder.Options);
    }
}
