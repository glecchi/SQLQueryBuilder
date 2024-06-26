﻿using CSQLQueryExpress.Scaffolding;
using System.Configuration;

var connectionString = ConfigurationManager.ConnectionStrings["DBConnectionString"].ConnectionString;
var outputFolder = ConfigurationManager.AppSettings["OutputFolder"];
var overwriteExistingDataModelClasses = bool.Parse(ConfigurationManager.AppSettings["OverwriteExistingDataModelClasses"]);
var dataModelClassNamespace = ConfigurationManager.AppSettings["DataModelClassNamespace"];
var decorateWithDatabaseAttribute = bool.Parse(ConfigurationManager.AppSettings["DecorateWithDatabaseAttribute"]);

var scaffoldingParameters = new SQLDataModelCodeGeneratorParameters(
        connectionString,
        outputFolder,
        dataModelClassNamespace,
        SQLDataModelCodeGeneratorEntityType.Table | SQLDataModelCodeGeneratorEntityType.View | SQLDataModelCodeGeneratorEntityType.StoredProcedure,
        overwriteExistingDataModelClasses, 
        decorateWithDatabaseAttribute);

var dataModelCodeGen = new SQLDataModelCodeGenerator(scaffoldingParameters);

var result = dataModelCodeGen.GenerateDataModel();

Console.WriteLine($"Result: {(result.Successfully ? "Successfully" : "With errors")}");

if (result.Errors.Count > 0)
{
    Console.WriteLine("Errors:");
    
    foreach (var entity in result.Errors)
    {
        Console.WriteLine($"EntityType: {entity.Key}");

        foreach (var error in entity.Value)
        {
            Console.WriteLine($"    {error.EntityName} => {error.Error.Message}");
        }
    }
}

Console.ReadLine();

