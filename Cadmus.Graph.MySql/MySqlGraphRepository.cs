using Cadmus.Graph.Sql;
using Fusi.Tools.Configuration;
using MySql.Data.MySqlClient;
using SqlKata.Compilers;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;

namespace Cadmus.Graph.MySql;

/// <summary>
/// MySql graph repository.
/// Tag: <c>graph-repository.sql-my</c>.
/// </summary>
/// <seealso cref="SqlGraphRepositoryBase" />
[Tag("graph-repository.sql-my")]
public sealed class MySqlGraphRepository : SqlGraphRepository,
    IGraphRepository
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MySqlGraphRepository"/>
    /// class.
    /// </summary>
    public MySqlGraphRepository() : base(new MySqlCompiler(), new MySqlHelper())
    {
    }

    /// <summary>
    /// Gets a connection.
    /// </summary>
    /// <returns>Connection.</returns>
    protected override IDbConnection GetConnection()
        => new MySqlConnection(ConnectionString);

    /// <summary>
    /// Gets the SQL DDL code representing the database schema for the
    /// graph.
    /// </summary>
    /// <returns>SQL code.</returns>
    public static string GetSchema()
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Cadmus.Graph.MySql.Assets.Schema.mysql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
