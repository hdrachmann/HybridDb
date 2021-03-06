using System;
using HybridDb.Config;
using HybridDb.Migrations.Commands;
using Shouldly;
using Xunit;
using Xunit.Extensions;

namespace HybridDb.Tests.Migrations.Commands
{
    public class SqlMigrationTests : HybridDbTests
    {
        [Theory]
        [InlineData(TableMode.UseTempTables)]
        [InlineData(TableMode.UseTempDb)]
        [InlineData(TableMode.UseRealTables)]
        public void AddsColumn(TableMode mode)
        {
            Use(mode);
            UseTableNamePrefix(Guid.NewGuid().ToString());
            new CreateTable(new Table("Entities", new Column("Col1", typeof(int)))).Execute(store.Database);
            new AddColumn("Entities", new Column("Col2", typeof(int))).Execute(store.Database);

            new SqlMigrationCommand("add some index", (sql, db) => sql
                .Append($"alter table {db.FormatTableNameAndEscape("Entities")} add {db.Escape("Col3")} int"))
                .Execute(store.Database);
        }

        [Fact]
        public void IsSafe()
        {
            new SqlMigrationCommand("is always safe", (sql, db) => { }).Unsafe.ShouldBe(false);
        }

        [Fact]
        public void RequiresReprojection()
        {
            new SqlMigrationCommand("add some index", "hansoggrethe", (sql, db) => { }).RequiresReprojectionOf.ShouldBe("hansoggrethe");
        }
    }
}