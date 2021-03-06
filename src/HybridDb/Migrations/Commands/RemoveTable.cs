namespace HybridDb.Migrations.Commands
{
    public class RemoveTable : SchemaMigrationCommand
    {
        public RemoveTable(string tablename)
        {
            Unsafe = true;
            Tablename = tablename;
        }

        public string Tablename { get; private set; }

        public override void Execute(IDatabase database)
        {
            database.RawExecute(string.Format("drop table {0};", database.FormatTableNameAndEscape(Tablename)));
        }

        public override string ToString()
        {
            return string.Format("Remove table {0}", Tablename);
        }
    }
}