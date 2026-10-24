# Contributing

Thanks for wanting to help. Bug reports and ideas go under [Issues](https://github.com/XtremeLead/User.MQTTPlugin/issues/new/choose). This page is for code changes.

For anything bigger than a small fix, open an issue first, so we can agree on the approach before you put time into it.

## What you need

- Windows with [SimHub](https://www.simhubdash.com/) installed in `C:\Program Files (x86)\SimHub`. The build references SimHub's assemblies from there.
- MSBuild 15 or later, for example from [Build Tools for Visual Studio](https://aka.ms/vs/17/release/vs_BuildTools.exe), with the .NET Framework 4.8 targeting pack.

## Building and testing

Restore the NuGet packages once, then build the solution in **Release** and run the tests:

```powershell
$msbuild = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
$vstest = '.\packages\Microsoft.TestPlatform.17.11.1\tools\net462\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'

& $msbuild .\User.MQTTPlugin.sln -t:restore -p:RestorePackagesConfig=true   # only the first time
& $msbuild .\User.MQTTPlugin.sln -p:Configuration=Release
& $vstest .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll
```

Use Release for this: a Debug build writes the plugin straight into the SimHub folder, which fails while SimHub is running.

To try your change in SimHub, close SimHub, build the plugin in Debug (`& $msbuild .\User.MQTTPlugin.csproj`), and start SimHub again. The plugin logs to `C:\Program Files (x86)\SimHub\Logs\SimHub.txt`.

If the build fails with `MC1000`, run it again with `-t:Rebuild`.

## Code guidelines

- **.NET Framework 4.8 and C# 7.3.** SimHub only loads .NET Framework plugins. Don't use newer C# features such as records, switch expressions, nullable reference types or file-scoped namespaces.
- **Old-style projects.** Both projects use `packages.config` and don't pick up files automatically. Add every new `.cs` file as a `<Compile Include>` to its `.csproj`. A new NuGet package goes in `packages.config` together with its `HintPath` reference and all of its dependencies.
- **Tests are required.** Every change comes with MSTest tests for the logic it adds or changes. Stay on MSTest 3.1.x.
  - Put testable logic in small helpers, like `MessageFormatter` or `ActionRules`, rather than in the WPF code-behind.
  - Tests never connect to a real broker and never write SimHub's settings. Use the fakes in `User.MQTTPlugin.Tests` (for example `ManagerHarness` in `ConnectionFakes.cs`, and `TestState.Reset()`).
- **Logging** goes through the plugin's log: lifecycle at Info, failed connects and dropped messages at Warn, lost data at Error, and individual messages only at Debug.
- **Match the surrounding code** in naming, comments and style.

## Pull requests

1. Fork the repository and create a branch from `develop`.
2. Make your change, with tests, and check that all tests pass.
3. If users will notice the change, add a line to `CHANGELOG.md`, and update the `README.md` if the usage changes.
4. Open the pull request against `develop` and describe what you changed and how you tested it, including in SimHub if the change affects the plugin's behaviour there.

## License

By contributing, you agree that your contribution is published under the [MIT license](LICENSE) of this project.
