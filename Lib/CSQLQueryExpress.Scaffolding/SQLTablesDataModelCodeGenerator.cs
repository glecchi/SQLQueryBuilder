﻿using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.IO;
using System.Reflection;
using System.Text;

namespace CSQLQueryExpress.Scaffolding
{
    internal class SQLTablesDataModelCodeGenerator
    {
        private readonly SQLDataModelCodeGeneratorParameters _parameters;

        public SQLTablesDataModelCodeGenerator(SQLDataModelCodeGeneratorParameters parameters)
        {
            _parameters = parameters;
        }

        public void GenerateDataModel(SQLDataModelCodeGeneratorResult result)
        {
            var scaffoldingScript = GetScaffoldingScript();

            var connectionStringBuilder = new SqlConnectionStringBuilder(_parameters.DatabaseConnectionString);

            var workingFolder = new DirectoryInfo(GetFolderPath(connectionStringBuilder.InitialCatalog));

            if (_parameters.ClearFolder && workingFolder.Exists)
            {
                workingFolder.Delete(true);
            }

            if (!workingFolder.Exists)
            {
                workingFolder.Create();
            }

            using (var connection = new SqlConnection(connectionStringBuilder.ConnectionString))
            {
                connection.Open();

                var tables = new List<Table>();

                using (var cmd = connection.CreateCommand())
                {
                    cmd.CommandText = _parameters.ScaffoldingTablesQuery;

                    using (var rd = cmd.ExecuteReader())
                    {
                        while (rd.Read())
                        {
                            tables.Add(new Table { Database = connectionStringBuilder.InitialCatalog, Schema = rd.GetString(0), Name = rd.GetString(1) });
                        }
                    }
                }

                foreach (var table in tables)
                {
                    try
                    {
                        var fileCs = GetFileCs(workingFolder.FullName, table);
                        if (!_parameters.OverwriteExistingDataModelClasses && File.Exists(fileCs))
                        {
                            continue;
                        }

                        if (File.Exists(fileCs))
                        {
                            File.Delete(fileCs);
                        }

                        using (var cmd = connection.CreateCommand())
                        {
                            cmd.CommandText = GetCommand(table, scaffoldingScript);

                            var dto = (string)cmd.ExecuteScalar();

                            if (dto.HasUnknownTypes(out IList<string> unknownTypes))
                            {
                                throw new Exception($"Unknown types: {string.Join(", ", unknownTypes)}");
                            }

                            File.WriteAllText(fileCs, dto);
                        }
                    }
                    catch (Exception ex)
                    {
                        result.AddError(SQLDataModelCodeGeneratorEntityType.Table, table.Name, ex);
                    }
                }
            }
        }

        string GetFolderPath(string databaseName)
        {
            return !string.IsNullOrWhiteSpace(_parameters.TablesFolder)
                ? Path.Combine(_parameters.OutputRootFolder, databaseName, _parameters.TablesFolder)
                : Path.Combine(_parameters.OutputRootFolder, databaseName);
        }

        string GetFileCs(string databaseFolderPath, Table table)
        {
            var schemaFolderPath = _parameters.GenerateSchemaFolder
                ? Path.Combine(databaseFolderPath, table.Schema)
                : databaseFolderPath;

            if (!Directory.Exists(schemaFolderPath))
            {
                Directory.CreateDirectory(schemaFolderPath);
            }

            var path = Path.Combine(schemaFolderPath, table.GetFileCs(_parameters));

            return path;
        }

        string GetCommand(Table table, string scaffoldingScript)
        {
            var className = table.GetClassName(_parameters);

            return scaffoldingScript
                .Replace("{DatabaseName}", table.Database)
                .Replace("{TableSchema}", table.Schema)
                .Replace("{TableName}", table.Name)
                .Replace("{Namespace}", $"{_parameters.RootNamespace}.{table.Database}")
                .Replace("{ClassName}", className);
        }

        string GetScaffoldingScript()
        {
            var scaffoldingScript = !_parameters.DecorateWithDatabaseAttribute
                ? !_parameters.GenerateSchemaNestedClasses
                    ? "Script_Scaffolding_Table.sql"
                    : "Script_Scaffolding_Table_AsSchemaNestedClass.sql"
                : !_parameters.GenerateSchemaNestedClasses
                    ? "Script_Scaffolding_Table_WithDbDecoration.sql"
                    : "Script_Scaffolding_Table_WithDbDecoration_AsSchemaNestedClass.sql";

            var info = Assembly.GetExecutingAssembly().GetName();
            var name = info.Name;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream($"{name}.Scripts.{scaffoldingScript}"))
            {
                using (var streamReader = new StreamReader(stream, Encoding.UTF8))
                {
                    return streamReader.ReadToEnd();
                }
            }
        }

        class Table
        {
            public string Database { get; set; }

            public string Schema { get; set; }

            public string Name { get; set; }

            public string GetFileCs(SQLDataModelCodeGeneratorParameters parameters)
            {
                return $"{GetClassName(parameters)}.cs";
            }

            public string GetClassName(SQLDataModelCodeGeneratorParameters parameters)
            {
                return $"{parameters.TablePrefix}{Name.Replace(" ", "_")}{parameters.TableSuffix}";
            }
        }
    }
}
