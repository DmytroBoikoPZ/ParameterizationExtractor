# 03 — Replace Stale Deps — Swap to `CommandLineParser`; remove FCLP

## Goal

Replace `FluentCommandLineParser` with `CommandLineParser` (TM Henrik). The `IAppArgs` contract and its consumers are unchanged. Step 02's tests must remain green — any divergence is a deliberate decision recorded in `## Notes`, not a silent quirk.

## Track

dotnet (refactor)

## What Exists

- `ParameterizationExtractor.csproj` references `FluentCommandLineParser.NETStandard 1.5.0.31-commands`.
- `AppBootstrap.cs`: `using Fclp;` + `AppArgs.GetAppArgs(...)` body using `FluentCommandLineParser<AppArgs>`.
- 14-15 tests in `Tests/AppArgsTests.cs` (from step 02) pinning every option, default, short/long-form, required-throw, and the two pinned-bug paired-args validations.

## What to Build

- Add `<PackageReference Include="CommandLineParser" Version="2.9.1" />` to `ParameterizationExtractor.csproj`.
- Decorate the existing `AppArgs` POCO with `CommandLine` attributes:
  - `[Option('n', "connectionName", Default = "SourceDB")] public string ConnectionName { get; set; }`
  - `[Option('p', "package", Required = true, HelpText = "Path to package")] public string PathToPackage { get; set; }`
  - `[Option('d', "database")] public string DBName { get; set; }`
  - `[Option('s', "serverName")] public string ServerName { get; set; }`
  - `[Option('o', "outputFolder", Default = "Output")] public string OutputFolder { get; set; }`
  - `[Option('i', "Interactive", Default = false)] public bool Interactive { get; set; }`
  - Note: `Interactive`'s long form keeps the capital `I` to match FCLP's behaviour. CommandLineParser is case-sensitive on long names.
- Rewrite `AppArgs.GetAppArgs(string[] args)` body:
  ```csharp
  var parserResult = Parser.Default.ParseArguments<AppArgs>(args);
  if (parserResult is not Parsed<AppArgs> parsed)
      throw new Exception("Argument parsing failed."); // or surface CommandLineParser's structured errors
  var a = parsed.Value;
  // pinned cross-arg validation (preserve flipped messages from FCLP version)
  if (string.IsNullOrEmpty(a.ServerName) && !string.IsNullOrEmpty(a.DBName))
      throw new Exception("Please specify DBName!");
  if (string.IsNullOrEmpty(a.DBName) && !string.IsNullOrEmpty(a.ServerName))
      throw new Exception("Please specify ServerName!");
  return a;
  ```
- Remove `using Fclp;` from `AppBootstrap.cs`.
- Remove `<PackageReference Include="FluentCommandLineParser.NETStandard" Version="1.5.0.31-commands" />` from `ParameterizationExtractor.csproj`.
- Verify no transitive code in the repo imports anything from `Fclp.*`.

## Acceptance Criteria

- [ ] `ParameterizationExtractor.csproj` references `CommandLineParser 2.9.1` and **no longer** references `FluentCommandLineParser.NETStandard`.
- [ ] No `using Fclp;` or `Fclp.` reference remains in the repo.
- [ ] All 14-15 tests from step 02 pass against the new parser. If any test reveals a CommandLineParser behaviour that diverges from FCLP (e.g., a different default-value precedence), the divergence is recorded in `## Notes` with a *deliberate* decision: either adapt the test or adapt the new code path. Silent test-modifications are not allowed.
- [ ] `dotnet build "SQL Buldozer.sln"` — 0 errors; FCLP-related warnings gone.
- [ ] `dotnet test "SQL Buldozer.sln"` — green. Total test count = 18 (pre-feature) + 14-15 (step 02) = ~32-33.
- [ ] Pinned-bug error messages preserved (the two paired-args validation throws keep saying the wrong thing — the bug fix is its own follow-up).

## References

- Related ADRs: —
- Related methodology: `docs/methodology/dotnet-cli.md`.
- Depends on: `02-replace-stale-deps-args-tests.md`.
