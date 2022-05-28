using Cadmus.Graph.Sql.Test;
using Cadmus.Index.Sql;
using Fusi.DbManager;
using Fusi.DbManager.MySql;
using Xunit;

namespace Cadmus.Graph.MySql.Test
{
    // https://github.com/xunit/xunit/issues/1999
    [CollectionDefinition(nameof(NonParallelResourceCollection),
        DisableParallelization = true)]
    public class NonParallelResourceCollection { }

    [Collection(nameof(NonParallelResourceCollection))]
    public sealed class MySqlGraphRepositoryTest : SqlGraphRepositoryTest
    {
        private IDbManager _manager;

        public override string ConnectionStringTemplate =>
            "Server=localhost;Database={0};Uid=root;Pwd=mysql;";

        public override IDbManager DbManager =>
            _manager ??= new MySqlDbManager(ConnectionStringTemplate);

        protected override IGraphRepository GetRepository()
        {
            MySqlGraphRepository repository = new();
            repository.Configure(new SqlOptions
            {
                ConnectionString = ConnectionString
            });
            return repository;
        }

        protected override string GetSchema() => MySqlGraphRepository.GetSchema();
    }
}