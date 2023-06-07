using Cadmus.Graph.Sql;
using Fusi.Tools.Configuration;
using Npgsql;
using SqlKata.Compilers;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace Cadmus.Graph.PgSql;

/// <summary>
/// PostgreSql graph repository.
/// Tag: <c>graph-repository.sql-pg</c>.
/// </summary>
/// <seealso cref="SqlGraphRepositoryBase" />
[Tag("graph-repository.sql-pg")]
public sealed class PgSqlGraphRepository : SqlGraphRepository,
    IGraphRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlGraphRepository"/>
    /// class.
    /// </summary>
    public PgSqlGraphRepository() : base(new PostgresCompiler(), new PgSqlHelper())
    {
    }

    /// <summary>
    /// Gets a connection.
    /// </summary>
    /// <returns>Connection.</returns>
    protected override IDbConnection GetConnection()
        => new NpgsqlConnection(ConnectionString);

    /// <summary>
    /// Gets the SQL DDL code representing the database schema for the
    /// graph.
    /// </summary>
    /// <returns>SQL code.</returns>
    public static string GetSchema()
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Cadmus.Graph.PgSql.Assets.Schema.pgsql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
