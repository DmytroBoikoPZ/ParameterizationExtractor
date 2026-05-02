using Quipu.ParameterizationExtractor.Logic.Interfaces;
using Quipu.ParameterizationExtractor.Logic.MSSQL;

namespace Tests.CharacterisationTests.Harness
{
    internal sealed class TestUnitOfWorkFactory : IUnitOfWorkFactory
    {
        private readonly string _connectionString;

        public TestUnitOfWorkFactory(string connectionString)
        {
            _connectionString = connectionString;
        }

        public IUnitOfWork GetUnitOfWork() => new UnitOfWork(_connectionString);

        public IUnitOfWork GetUnitOfWork(string source) => new UnitOfWork(source);
    }
}
