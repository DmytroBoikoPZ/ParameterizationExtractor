using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Threading;
using Microsoft.Data.SqlClient;
using Quipu.ParameterizationExtractor.Logic.Interfaces;

namespace Quipu.ParameterizationExtractor.Logic.MSSQL
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly SqlConnection _sqlConnection;
        private SqlTransaction _transaction;
        public UnitOfWork(string connectionString)
        {
            _sqlConnection = new SqlConnection(connectionString);
            _sqlConnection.Open();
        }

        public string Database
        {
            get
            {
                return _sqlConnection.Database;
            }
        }

        public string DataSource
        {
            get
            {
                return _sqlConnection.DataSource;
            }
        }

        private bool IsOpened { get; set; }

        public void BeginTran()
        {
            _transaction = _transaction ?? _sqlConnection.BeginTransaction();
        }

        public void Commit()
        {
            if (_transaction != null)
                _transaction.Commit();
        }

        public void Dispose()
        {
            _sqlConnection.Close();

            if (_transaction != null)
                _transaction.Dispose();

            if (_sqlConnection != null)
                _sqlConnection.Dispose();
        }

        public Task<SqlDataReader> ExecuteReaderAsync(string sql, CancellationToken cancellationToken)
        {            
            cancellationToken.ThrowIfCancellationRequested();

            var com = new SqlCommand(sql, _sqlConnection, _transaction);

            return com.ExecuteReaderAsync(cancellationToken);
        }

        public Task<DataTable> GetSchemaAsync(string collectionName, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.Run(() => _sqlConnection.GetSchema(collectionName), cancellationToken);
        }

        public Task<DataTable> GetSchemaAsync(string collectionName, string[] restrictionValues, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return Task.Run(() => _sqlConnection.GetSchema(collectionName, restrictionValues));
        }

        public void Rollback()
        {
            if (_transaction != null)
                _transaction.Rollback();
        }
    }
}
