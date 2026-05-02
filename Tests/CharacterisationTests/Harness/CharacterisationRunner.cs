using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using ParameterizationExtractor.Logic.MSSQL;
using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.MSSQL;

namespace Tests.CharacterisationTests.Harness
{
    public sealed class CharacterisationRunner
    {
        private readonly ISourceSchema _schema;
        private readonly IDependencyBuilder _dependencyBuilder;
        private readonly ISqlBuilder _sqlBuilder;

        private CharacterisationRunner(
            ISourceSchema schema,
            IDependencyBuilder dependencyBuilder,
            ISqlBuilder sqlBuilder)
        {
            _schema = schema;
            _dependencyBuilder = dependencyBuilder;
            _sqlBuilder = sqlBuilder;
        }

        public static async Task<CharacterisationRunner> CreateAsync(
            string connectionString,
            IExtractConfiguration config,
            CancellationToken cancellationToken = default)
        {
            var unitOfWorkFactory = new TestUnitOfWorkFactory(connectionString);

            var metaInitializer = new MetaDataInitializer();
            var metaDataProvider = new ObjectMetaDataProvider(
                unitOfWorkFactory,
                NullLogger<ObjectMetaDataProvider>.Instance,
                metaInitializer);

            var schema = new MSSQLSourceSchema(
                unitOfWorkFactory,
                config,
                NullLogger<MSSQLSourceSchema>.Instance,
                metaDataProvider);

            await schema.Init(cancellationToken);

            var dependencyBuilder = new DependencyBuilder(
                unitOfWorkFactory,
                schema,
                NullLogger<DependencyBuilder>.Instance,
                config);

            var sqlBuilder = new MSSqlBuilder(
                config,
                NullLogger<MSSqlBuilder>.Instance);

            return new CharacterisationRunner(schema, dependencyBuilder, sqlBuilder);
        }

        public async Task<string> RunScenarioAsync(
            ISourceForScript scenario,
            CancellationToken cancellationToken = default)
        {
            var records = await _dependencyBuilder.PrepareAsync(cancellationToken, scenario);
            var sql = _sqlBuilder.Build(records, _schema, scenario);
            return SqlNormalizer.Normalize(sql);
        }
    }
}
