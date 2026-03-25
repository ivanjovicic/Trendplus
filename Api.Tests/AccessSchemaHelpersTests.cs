using System.Data;
using Api.Services;
using Xunit;

namespace Api.Tests
{
    public class AccessSchemaHelpersTests
    {
        [Fact]
        public void ResolveTableName_Uses_TABLE_NAME_if_present()
        {
            var table = new DataTable();
            table.Columns.Add("TABLE_NAME", typeof(string));
            table.Columns.Add("TABLE_TYPE", typeof(string));
            var row = table.NewRow();
            row["TABLE_NAME"] = "MyTable";
            row["TABLE_TYPE"] = "TABLE";
            table.Rows.Add(row);

            var name = AccessImportService.ResolveTableName(row, table);
            Assert.Equal("MyTable", name);
        }

        [Fact]
        public void ResolveTableName_Uses_TABLE_if_TABLE_NAME_missing()
        {
            var table = new DataTable();
            table.Columns.Add("TABLE", typeof(string));
            var row = table.NewRow();
            row["TABLE"] = "OtherTable";
            table.Rows.Add(row);

            var name = AccessImportService.ResolveTableName(row, table);
            Assert.Equal("OtherTable", name);
        }

        [Fact]
        public void ResolveTableName_Uses_NAME_when_no_table_columns()
        {
            var table = new DataTable();
            table.Columns.Add("name", typeof(string));
            var row = table.NewRow();
            row[0] = "NameCol";
            table.Rows.Add(row);

            var name = AccessImportService.ResolveTableName(row, table);
            Assert.Equal("NameCol", name);
        }

        [Fact]
        public void ResolveTableName_FallsBackToFirstColumn()
        {
            var table = new DataTable();
            table.Columns.Add("X", typeof(string));
            var row = table.NewRow();
            row[0] = "First";
            table.Rows.Add(row);

            var name = AccessImportService.ResolveTableName(row, table);
            Assert.Equal("First", name);
        }

        [Fact]
        public void CheckIsUserTable_ReturnsTrue_when_TABLE_TYPE_missing()
        {
            var table = new DataTable();
            table.Columns.Add("SOME", typeof(string));
            var row = table.NewRow();
            row[0] = "val";
            table.Rows.Add(row);

            var isUser = AccessImportService.CheckIsUserTable(row, table);
            Assert.True(isUser);
        }

        [Fact]
        public void CheckIsUserTable_Respects_TABLE_TYPE_value()
        {
            var table = new DataTable();
            table.Columns.Add("TABLE_TYPE", typeof(string));
            var rowTable = table.NewRow();
            rowTable["TABLE_TYPE"] = "TABLE";
            table.Rows.Add(rowTable);

            Assert.True(AccessImportService.CheckIsUserTable(rowTable, table));

            var rowView = table.NewRow();
            rowView["TABLE_TYPE"] = "VIEW";
            table.Rows.Add(rowView);

            Assert.False(AccessImportService.CheckIsUserTable(rowView, table));
        }
    }
}
