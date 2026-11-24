[![Build Status](https://dev.azure.com/lubennaumov0149/StoreLake/_apis/build/status/benhauman.storelake?branchName=main)](https://dev.azure.com/lubennaumov0149/StoreLake/_build/latest?definitionId=1&branchName=main)
[![Coverage](https://img.shields.io/azure-devops/coverage/lubennaumov0149/StoreLake/1/main)](https://dev.azure.com/lubennaumov0149/StoreLake/_build/latest?definitionId=1&branchName=main&view=codecoverage-tab)
[![NuGet StoreLake](https://img.shields.io/nuget/v/StoreLake?label=StoreLake)](https://www.nuget.org/packages/StoreLake)
[![NuGet StoreLake.Sdk](https://img.shields.io/nuget/v/StoreLake.Sdk?label=StoreLake.Sdk)](https://www.nuget.org/packages/StoreLake.Sdk)
[![NuGet StoreLake.Sdk.Cli](https://img.shields.io/nuget/v/StoreLake.Sdk.Cli?label=StoreLake.Sdk.Cli)](https://www.nuget.org/packages/StoreLake.Sdk.Cli)

# storelake
Ado.Net in memory database bridge for unit tests  

Typed Data Set generator based on .dacpac input generates composable DataSet extensions

## What it does

StoreLake lets unit tests run data access code (ADO.NET / Dibix accessors) against an **in-memory `DataSet`** instead of a SQL Server database.

1. **Generate** a *test store* assembly from a SQL Server database project (`.dacpac`): a typed `DataSet` model with one typed table per SQL table, plus handler contracts for the stored procedures.
2. **Compose** an in-memory database in the test from one or more generated stores and seed it with typed rows.
3. **Run** the production data access code through the StoreLake ADO.NET provider. Commands are routed to handlers that read and write the in-memory tables.

Typical use cases:
- Unit tests for repositories / services that call stored procedures or SQL text through ADO.NET or Dibix, without a database server.
- Seeding test data with compile-time checked, typed rows (`AddRowWithValues(...)`), with keys, foreign keys and defaults from the database schema.
- Faking a single stored procedure (or SQL text) with C# logic over the in-memory tables.

## Packages

| Package | Content |
|---|---|
| `StoreLake.Sdk.Cli` | `StoreLake.Sdk.Cli.exe`: generates and compiles the test store assembly from a `.dacpac` (.NET Framework 4.8). |
| `StoreLake.Sdk` | The generator library used by the CLI (dacpac import, typed DataSet code generation, T-SQL analysis via ScriptDom). |
| `StoreLake` | Runtime for tests: in-memory database builder and ADO.NET provider (`StoreLakeDbServer`, `DbProviderFactory`, command routing). |

## Generate a test store

Run the CLI from the database project folder:

```
StoreLake.Sdk.Cli.exe
  libdir=<dir with Dibix.dll matching the dacpac assembly>[;<more dirs>]
  inputdacdir=<dir with the .dacpac files>
  inputdlldir=<dir with the dacpac's compiled .dll (Dibix accessors)>
  dacpac=HelplineData.dacpac
  outputdir=<output dir for the generated assembly>
  storesuffix=TestStore
  tempdir=<dir for the generated .cs files and compiler output>
  [generateschema=false] [regeneratereferences=false] [generatemissingreferences=false]
```

Output: `<dacpac name>.<storesuffix>.dll` (for example `HelplineData.TestStore.dll`), compiled from the generated `*.Tables.cs`, `*.Udts.cs`, `*.Procedures.cs` and `*.Accessors.cs` files in `tempdir`.

Notes:
- `libdir` must contain the Dibix version the dacpac's assembly was built with (same assembly version, for example `Dibix 1.8.0.0`), otherwise compilation fails with `CS1705`.
- Referenced dacpacs (for example another database project) are resolved from `inputdacdir`. `generatemissingreferences=true` generates missing referenced stores, `regeneratereferences=true` regenerates existing ones.
- A dacpac without an assembly reference in its header uses the dacpac file name as assembly name (`X.dacpac` -> `X.dll`).
- See `src/StoreLake.Sdk.Cli/Properties/launchSettings.json` for complete examples.

## What gets generated

Per dacpac, in namespace `<Name>.<storesuffix>` (for example `Helpline.Data.TestStore`):

- `<SetName>Extensions` (static)
  - `RegisterDataSetModel(ds)`: adds the dacpac's tables with primary/unique keys, foreign keys and defaults to a `DataSet`.
  - one accessor per table: `db.hlsysagent()` returns the typed table.
- Per table `<table>DataTable` / `<table>Row` (typed `DataTable` / `DataRow`):
  - `AddRowWithValues(...)`: required columns first, then optional ones. Nullable columns, columns with a default and `ROWVERSION` columns are optional parameters.
  - `FindRowByPrimaryKey(...)`, `GetRowByPrimaryKey(...)`, `DeleteRowByPrimaryKey(...)`.
  - typed column properties on the row (nullable value types for `NULL` columns).
  - `IDENTITY` columns use `AutoIncrement`.
  - `ROWVERSION` / `TIMESTAMP` columns behave like on SQL Server: a database-wide, increasing 8-byte value is assigned on every insert and update. The row property is read only; `row.GetRowVersionValueAsLong()` returns the value as number.
- User-defined table types (`*.Udts.cs`) as typed rows for table-valued parameters.
- Stored procedures (`*.Procedures.cs`): `<SetName>ProceduresHandlerFacade` (one overridable method per procedure with typed parameters and result sets) and `<SetName>ProceduresCommandExecuteHandler` (`DbCommand` level).

## Use the store in a test

```csharp
using System.Data;
using System.Data.Common;
using Helpline.Data.TestStore;              // generated
using StoreLake.TestStore.Database;
using StoreLake.TestStore.Server;

// 1. compose the in-memory database
DataSet db = StoreLakeDatabaseServer.CreateDatabase("MyDB")
    .Use(DatabaseGetUtcDateExtension.Register)        // GETUTCDATE() replacement: db.GetUtcDate()
    .Use(HelplineDataExtensions.RegisterDataSetModel) // one .Use(...) per generated store
    .Build()
    .SetCommandExecuteHandlerInstanceForHelplineDataProceduresFacade<MyProceduresHandler>();

// 2. seed typed rows (keys and foreign keys are enforced by the DataSet)
var agent = db.hlsysagent().AddRowWithValues(agentid: 710, name: "InternetAgent", fullname: null, description: null, active: 1);
db.hlsysgroup().AddRowWithValues(700, "Administrators");
db.hlsysagenttogroup().AddRowWithValues(agent.agentid, 700);

// 3. run production data access code against it
StoreLakeDbServer server = new StoreLakeDbServer(db);
server.RegisterCommandHandlerMethods(typeof(MyDml), typeof(MyDmlHandler)); // SQL text commands
DbProviderFactory factory = server.CreateDbProviderFactoryInstance();
// pass 'factory' and a connection string like "Initial Catalog=MyDB" to the code under test
```

Stored procedure behaviour is implemented by overriding the generated facade:

```csharp
internal class MyProceduresHandler : HelplineDataProceduresHandlerFacade
{
    public override CanExecuteResultSets CanExecute(DataSet db, CanExecuteResultSets output, int agentid, int globalid)
    {
        // query the in-memory tables, fill 'output'
        return output;
    }
}
```

SQL text commands are matched with `DynamicSqlTextCompare` (for example "1 parameter, contains `SELECT name FROM` and `hlsysagent`") and handled by a static method `DbDataReader Handler(DataSet db, DbCommand cmd)` or `int Handler(DataSet db, DbCommand cmd)`. See `src/StoreLake.Test.ConsoleApp` (`Program.cs`, `DemoHandler*.cs`) for complete examples.

## Repository layout

| Folder | Content |
|---|---|
| `src/StoreLake` | Runtime: `Database` (database builder, `GetUtcDate`), `Common` (ADO.NET provider: connection, command, parameters, data reader), `Server` (`StoreLakeDbServer`, command routing to handlers). |
| `src/StoreLake.Sdk` | Generator: `CodeGeneration` (dacpac import, typed DataSet adjustments, procedures, UDTs, `TableGenerator` for `CREATE TABLE` scripts), `SqlDom` (T-SQL analysis: procedure result sets, check constraints). |
| `src/StoreLake.Sdk.Cli` | Command line tool around `StoreLakeSdkTool.Run`. |
| `src/StoreLake.Test` | Unit tests: SQL scripts under `SQL/` (tables, views, functions, types, procedures) are fed to the generators (`ProcedureGenerator_Tests`, `TableGenerator_Tests`, `CheckConstraintGenerator_Tests`). |
| `src/StoreLake.Test.ConsoleApp` | Sample usage against generated Helpline stores. |

## Build and test

Requirements: Windows, .NET Framework 4.8 targeting pack, .NET SDK (see `global.json`).

```
dotnet build StoreLake.sln
dotnet test src/StoreLake.Test/StoreLake.Test.csproj
```

CI: Azure Pipelines (`build/azure-pipeline-build.yml`) builds, runs the tests with code coverage and, on demand, publishes the NuGet packages. Package versions come from `version.json` (Nerdbank.GitVersioning): bump `version` before publishing a new release, nuget.org rejects an existing version.
