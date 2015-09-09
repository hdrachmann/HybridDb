using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Transactions;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Serilog;

namespace HybridDb
{
    public class SqlServerUsingRealTables : SqlServer
    {
        readonly bool useTableMetaData;

        readonly string prefix;

        int numberOfManagedConnections;
        
        public SqlServerUsingRealTables(ILogger logger, string connectionString, string prefix, bool useTableMetaData)
            : base(logger, connectionString)
        {
            this.useTableMetaData = useTableMetaData;

            this.prefix = prefix ?? "";
        }

        public override string FormatTableName(string tablename)
        {
            return prefix + tablename;
        }

        public override ManagedConnection Connect()
        {
            Action complete = () => { };
            Action dispose = () => { numberOfManagedConnections--; };

            try
            {
                if (Transaction.Current == null)
                {
                    var tx = new TransactionScope(
                        TransactionScopeOption.RequiresNew,
                        new TransactionOptions { IsolationLevel = IsolationLevel.ReadCommitted });

                    complete += tx.Complete;
                    dispose += tx.Dispose;
                }

                var connection = new SqlConnection(connectionString);
                connection.InfoMessage += (obj, args) => OnMessage(args);
                connection.Open();

                complete = connection.Dispose + complete;
                dispose = connection.Dispose + dispose;

                numberOfManagedConnections++;

                return new ManagedConnection(connection, complete, dispose);
            }
            catch (Exception)
            {
                dispose();
                throw;
            }
        }

        public override Dictionary<string, Table> QuerySchema()
        {
            var schema = new Dictionary<string, Table>();
            IEnumerable<string> tables;
            if (useTableMetaData)
            {
                tables = RawQuery<string>(string.Format(
                  "SELECT u.name + '.' + t.name AS Name FROM  sysobjects t INNER JOIN  sysusers u ON u.uid = t.uid LEFT OUTER JOIN sys.extended_properties td ON td.major_id = t.id AND td.minor_id = 0 AND td.name = 'MS_Description' WHERE t.type = 'u' and td.value ='" + CreateTable.TableMetaData + "' and t.name LIKE '{0}%'",
                 prefix))
                 .ToList();
            }
            else
            {
                tables = RawQuery<string>(string.Format(
                   "select table_name from information_schema.tables where table_type='BASE TABLE' and table_name LIKE '{0}%'",
                   prefix))
                   .ToList();
            }
            //    "SELECT u.name + '.' + t.name AS Name FROM  sysobjects t INNER JOIN  sysusers u ON u.uid = t.uid LEFT OUTER JOIN sys.extended_properties td ON td.major_id = t.id AND td.minor_id = 0 AND td.name = 'MS_Description' WHERE t.type = 'u' and td.value ='HybridDb' and t.name LIKE '{0}%'",


            foreach (var tableName in tables)
            {
                var columns = RawQuery<QueryColumn>(string.Format("select * from sys.columns where Object_ID = Object_ID(N'{0}')", tableName));

                var formattedTableName = tableName.Remove(0, prefix.Length);

                schema.Add(formattedTableName, new Table(formattedTableName, columns.Select(column => Map(tableName, column, isTempTable: false))));
            }

            return schema;
        }

        public override void Dispose()
        {
            if (numberOfManagedConnections > 0)
                logger.Warning("A ManagedConnection was not properly disposed. You may be leaking sql connections or transactions.");
        }
    }
}