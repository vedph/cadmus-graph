using Cadmus.Graph.Sql;
using Fusi.DbManager.MySql;
using Fusi.Tools.Configuration;
using MySql.Data.MySqlClient;
using SqlKata.Compilers;
using System;
using System.Data;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

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

    /// <summary>
    /// Creates the target store if it does not exist.
    /// </summary>
    /// <param name="payload">Optional SQL code for seeding preset data.</param>
    /// <returns>
    /// True if created, false if already existing.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Missing connection string for MySql graph repository, or
    /// Missing database name from connection string for MySql graph repository.
    /// </exception>
    public bool CreateStore(object? payload = null)
    {
        // extract database name from connection string
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string for MySql graph repository");
        }
        Regex nameRegex = new("Database=([^;]+)", RegexOptions.IgnoreCase);
        Match m = nameRegex.Match(ConnectionString);
        if (!m.Success)
        {
            throw new InvalidOperationException(
                "Missing database name from connection string " +
                "for MySql graph repository");
        }

        // create database if required
        MySqlDbManager manager = new(
            nameRegex.Replace(ConnectionString, "Database={0}"));
        if (manager.Exists(m.Groups[1].Value)) return false;

        manager.CreateDatabase(m.Groups[1].Value, GetSchema(), payload as string);

        return true;
    }
}
