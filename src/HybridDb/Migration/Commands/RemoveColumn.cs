using HybridDb.Config;

namespace HybridDb.Migration.Commands
{
    public class RemoveColumn : SchemaMigrationCommand
    {
        public RemoveColumn(Table table, string name)
        {
            Unsafe = name == "Document" || name == "Id";

            Table = table;
            Name = name;
        }

        public Table Table { get; private set; }
        public string Name { get; private set; }

        public override void Execute(DocumentStore store)
        {
            store.RawExecute(string.Format("alter table {0} drop column {1};", store.FormatTableNameAndEscape(Table.Name), store.Escape(Name)));
        }
    }
}