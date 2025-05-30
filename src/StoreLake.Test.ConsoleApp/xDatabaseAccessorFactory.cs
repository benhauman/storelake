﻿namespace StoreLake.Test.ConsoleApp
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Dibix;
    using StoreLake.TestStore.Server;

    public class xDatabaseAccessorFactory : Dibix.IDatabaseAccessorFactory
    {
        private readonly StoreLakeDabaseAccessorGate accessorGate;
        private readonly DbProviderFactory dbClient;
        private readonly string connectionString;
        private readonly StoreLakeDbServer dbServer;
        public xDatabaseAccessorFactory(StoreLakeDbServer dbServer, StoreLakeDabaseAccessorGate accessorGate, DbProviderFactory dbClient, string connectionString)
        {
            this.dbServer = dbServer;
            this.accessorGate = accessorGate;
            this.dbClient = dbClient;
            this.connectionString = connectionString;
        }

        public Dibix.IDatabaseAccessor Create()
        {
            var connection = CreateConnection();
            DataSet db = dbServer.GetDatabaseForConnection(connection);
            return new StoreLakeDabaseAccessor(db, accessorGate, new Dibix.Dapper.DapperDatabaseAccessor(connection));
        }

        public DbConnection CreateConnection()
        {
            DbConnection connection = dbClient.CreateConnection();
            connection.ConnectionString = connectionString;
            return connection;
        }
    }
    public sealed class StoreLakeDabaseAccessor : Dibix.IDatabaseAccessor
    {
        private DataSet db;
        private StoreLakeDabaseAccessorGate gate;
        private Dibix.IDatabaseAccessor accessor;
        public StoreLakeDabaseAccessor(DataSet db, StoreLakeDabaseAccessorGate accessorGate, Dibix.IDatabaseAccessor accessor)
        {
            this.db = db;
            this.gate = accessorGate;
            this.accessor = accessor;
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                gate = null;

                var tmp = accessor;
                accessor = null;
                if (tmp != null)
                {
                    tmp.Dispose();
                }
            }
        }

        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        int IDatabaseAccessor.Execute(string sql, CommandType commandType, ParametersVisitor parameters, int? commandTimeout)
        {
            return accessor.Execute(sql, commandType, parameters, commandTimeout);
        }

        int IDatabaseAccessor.Execute(string commandText, CommandType commandType, ParametersVisitor parameters)
        {
            throw new NotImplementedException();
        }

        Task<int> IDatabaseAccessor.ExecuteAsync(string commandText, CommandType commandType, ParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<int> IDatabaseAccessor.ExecuteAsync(string commandText, CommandType commandType, ParametersVisitor parameters, int? commandTimeout, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IParameterBuilder IDatabaseAccessor.Parameters()
        {
            return accessor.Parameters();
        }

        IEnumerable<T> IDatabaseAccessor.QueryMany<T>(string sql, CommandType commandType, ParametersVisitor parameters)
        {
            return accessor.QueryMany<T>(sql, commandType, parameters);
        }

        IEnumerable<TReturn> IDatabaseAccessor.QueryMany<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn)
        {
            return accessor.QueryMany<TReturn>(commandText, commandType, parameters, types, splitOn);
        }

        Task<IEnumerable<T>> IDatabaseAccessor.QueryManyAsync<T>(string sql, CommandType commandType, ParametersVisitor parameters, bool buffered, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<T>> IDatabaseAccessor.QueryManyAsync<T>(string commandText, CommandType commandType, ParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<IEnumerable<TReturn>> IDatabaseAccessor.QueryManyAsync<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        IMultipleResultReader IDatabaseAccessor.QueryMultiple(string sql, CommandType commandType, ParametersVisitor parameters)
        {
            // see : Helpline.Repository.Data.HelplineData.GetUserInfo(databaseAccessorFactory, 710);
            return accessor.QueryMultiple(sql, commandType, parameters);
        }

        Task<IMultipleResultReader> IDatabaseAccessor.QueryMultipleAsync(string sql, CommandType commandType, ParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        T IDatabaseAccessor.QuerySingle<T>(string sql, CommandType commandType, ParametersVisitor parameters)
        {
            return accessor.QuerySingle<T>(sql, commandType, parameters);
        }

        TReturn IDatabaseAccessor.QuerySingle<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn)
        {
            throw new NotImplementedException();
        }

        Task<T> IDatabaseAccessor.QuerySingleAsync<T>(string sql, CommandType commandType, ParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<TReturn> IDatabaseAccessor.QuerySingleAsync<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        T IDatabaseAccessor.QuerySingleOrDefault<T>(string sql, CommandType commandType, ParametersVisitor parameters)
        {
            Func<DataSet, ParametersVisitor, object> handler = gate.TryGetHandlerRead(sql); // 1xSet
            if (handler != null)
            {
                object result_object = handler(db, parameters);
                return (T)result_object;
            }
            return accessor.QuerySingleOrDefault<T>(sql, commandType, parameters);
        }

        TReturn IDatabaseAccessor.QuerySingleOrDefault<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn)
        {
            throw new NotImplementedException();
        }

        Task<T> IDatabaseAccessor.QuerySingleOrDefaultAsync<T>(string commandText, CommandType commandType, ParametersVisitor parameters, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        Task<TReturn> IDatabaseAccessor.QuerySingleOrDefaultAsync<TReturn>(string commandText, CommandType commandType, ParametersVisitor parameters, Type[] types, string splitOn, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
