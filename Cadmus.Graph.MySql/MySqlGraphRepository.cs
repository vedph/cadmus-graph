using Cadmus.Graph.Sql;
using Cadmus.Index.Sql.Graph;
using Fusi.Tools.Config;
using MySql.Data.MySqlClient;
using SqlKata.Compilers;
using System.Data;

namespace Cadmus.Graph.MySql
{
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
    }
}
