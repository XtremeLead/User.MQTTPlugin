# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A SimHub plugin (.NET Framework 4.8, WPF, old-style non-SDK `.csproj` with `packages.config`) that subscribes to one MQTT topic and exposes received messages as the SimHub property `MQTTPlugin.MQTTMessage`. It also publishes MQTT messages through user-defined SimHub Actions. MQTT is handled by M2Mqtt 4.3; JSON by Newtonsoft.Json.

**Language level:** the project sets no `LangVersion`, so it compiles as **C# 7.3**. Don't use file-scoped namespaces, nullable reference types, records, `init`, switch expressions or other newer features. SimHub only loads .NET Framework plugins, so upgrading to modern .NET is not an option.

## Git

- **Never commit or push.** The user makes all commits themselves. Leave changes in the working tree and say what changed.
- Work on the `claude` branch (branched from `develop`; `master` is the main branch). Don't switch branches unless the user asks.

## Plan and progress

- [PLAN.md](PLAN.md) is the agreed improvement plan. Bugs, architecture, readability and testability items have fixed codes (B1, A1, L1, T1, ...), work is grouped into phases (F0–F6), and decisions are listed (D1–D3, all decided). Follow the phase order and refer to items by code. Don't change the plan's content without the user's agreement.
- [PROGRESS.md](PROGRESS.md) tracks progress. Update it after every completed step: the phase and item status, any decisions with their date, and a new log line at the top. Mark an item ✅ only after it is built and its tests pass.
- Both files are written in Dutch, so keep them in Dutch.

## Build

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' .\User.MQTTPlugin.csproj
```

- **Debug** builds write output straight into `C:\Program Files (x86)\SimHub\`, which is how the plugin gets deployed. That means the build needs write access to that folder, and it fails if SimHub is running, because SimHub locks the DLL. **Release** builds write to `bin\Release\`.
- SimHub assemblies (`SimHub.Plugins`, `GameReaderCommon`, `SimHub.Logging`, `log4net`) are referenced from the SimHub install folder, so SimHub must be installed to build.
- NuGet packages go in `packages\`. If they are missing, restore them with `nuget restore User.MQTTPlugin.sln` or `msbuild -t:restore -p:RestorePackagesConfig=true`.
- There is no linter. Behaviour that unit tests can't cover (SimHub integration, the WPF UI, a real broker) is verified by building, restarting SimHub, and checking the SimHub log, since everything is logged through `SimHub.Logging.Current.Info`.

## Unit tests

Unit tests are required. Use **MSTest**.

- **New and changed code:** every change comes with unit tests for the logic it adds or modifies.
- **Existing code:** there are no tests yet. Existing testable logic must get tests too, for example the payload parsing and wrapping in `client_MqttMsgPublishReceived`, the `Crypt` round-trip, the `mqttactions` (de)serialization, and the settings parsing in `ConnectAndSubscribe`.
- **Test project:** [User.MQTTPlugin.Tests/](User.MQTTPlugin.Tests/) is an old-style .NET Framework 4.8 project with `packages.config`, like the main project. It uses MSTest 3.1.1 and runs on `vstest.console.exe` from the `Microsoft.TestPlatform` package.
  - It isn't SDK-style because the BuildTools MSBuild can't resolve `Microsoft.NET.Sdk` here (the only .NET SDK installed is 5.0). Since `packages.config` doesn't restore transitive dependencies, stay on MSTest 3.1.x: 3.2 and later pull in `Microsoft.Testing.Platform` packages that would all have to be listed by hand.
  - The main assembly has `InternalsVisibleTo("User.MQTTPlugin.Tests")`, so tests can reach `internal` types.
  - Build in **Release**. A Debug build of the solution also builds the plugin into the SimHub folder, which fails while SimHub is running.

  ```powershell
  $msb = 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
  $vstest = '.\packages\Microsoft.TestPlatform.17.11.1\tools\net462\Common7\IDE\Extensions\TestPlatform\vstest.console.exe'
  & $msb .\User.MQTTPlugin.sln -t:restore -p:RestorePackagesConfig=true   # only when packages\ is missing
  & $msb .\User.MQTTPlugin.sln -p:Configuration=Release
  & $vstest .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll                    # all tests
  & $vstest .\User.MQTTPlugin.Tests\bin\Release\User.MQTTPlugin.Tests.dll /TestCaseFilter:"FullyQualifiedName~CryptTests.ProtectString_DoesNotReturnPlainText"   # one test
  ```

- **Characterization tests:** tests whose comment starts with `Current behaviour (Bx)` pin a known bug from [PLAN.md](PLAN.md). The phase that fixes Bx changes them on purpose. Tests that need a failing host use `broker.invalid`, which always fails DNS and never connects.
- **Seams (F0b):** `MQTTSettings.Store` (`ISettingsStore`), `MQTTSettings.Reconnect` and `MQTTClient.SleepSeconds` are static fields that tests replace. [TestState.cs](User.MQTTPlugin.Tests/TestState.cs) `Reset()` swaps in `FakeSettingsStore` (same names, defaults and type check as `Properties.Settings`), a no-op reconnect and a no-op sleep, and resets the remaining private statics through reflection. Call it in `[TestInitialize]` and `[TestCleanup]`. Only `PropertiesSettingsStoreTests` touches the real `Properties.Settings`, in memory, restoring it with `Reload()`. Never call `Save()` on it in tests.
- **Pure helpers (F0b):** `MessageFormatter`, `ActionsSerializer`, `SettingsRules`, `SettingsViewLogic` and `MQTTClient.GetBackoffDelaySeconds`. Put new testable logic there, or in similar helpers, rather than in the WPF code-behind or the M2Mqtt callbacks.
- **Adding test files:** the project doesn't glob, so add each new `.cs` file as `<Compile Include>` in the test `.csproj`. Add a new package to `packages.config` together with its `HintPath` reference, and list every dependency it has.
- **Testability:** much of the logic sits in static methods mixed with side effects (M2Mqtt client, `Properties.Settings`, `MQTTPlugin.Instance`, logging). To test it, extract pure helpers (for example a method that turns topic and payload bytes into the message JSON) and leave behaviour unchanged. Don't let tests connect to a real broker or write to the user's `Properties.Settings`.

## Architecture

- **`MQTTPlugin`** ([MQTTPlugin.cs](MQTTPlugin.cs)) is the SimHub entry point (`IPlugin`, `IDataPlugin`, `IWPFSettingsV2`). SimHub rebuilds plugins on every game change, so `Init`/`End` run many times per session. `MQTTPlugin.Instance` is a static pointer to the live instance. `DataUpdate` is intentionally empty, because data arrives from MQTT and not from the game loop.
- **`MQTTClient`** ([MQTTClient.cs](MQTTClient.cs)) is entirely static and holds the single `CLIENT`, which outlives plugin instances.
  - Access to `CLIENT` is guarded by `ClientLock`.
  - A 5s `System.Timers.Timer` watchdog reconnects using the backoff schedule in `BackoffSchedule`. It starts in `Init` and stops in `End`.
  - `client_MqttMsgPublishReceived` decodes the payload as UTF-8 and tries `JToken.Parse`; if that fails, it falls back to the raw string. It wraps the result as `{topic, payload, timestamp}` (timestamp uses the `nl-NL` culture) and sets `MQTTPlugin.Instance.MqttMessage`. Dashboards then `JSON.parse` this value (see README).
  - The M2Mqtt `Connect()` call blocks, so UI code must use the `*Async` wrappers (`ConnectAndSubscribeAsync`, `TestConnectionAsync`).
- **Two settings stores, which are easy to confuse:**
  - `Properties.Settings` ([Properties/Settings.settings](Properties/Settings.settings)) holds the real config: `mqttserver`, `mqttport`, `mqttuser`, `mqttpass`, `mqtttopic`, `mqtttls`, `mqttactions`, `lastConnectionSuccessful`. **`MQTTSettings`** ([MQTTSettings.cs](MQTTSettings.cs)) wraps it. `LoadSettings()` returns every value as a string (so booleans come back as `"True"`/`"False"`). `RememberSetting()` stages edits in memory, and `SaveSettingsAsync()` writes them and then reconnects.
  - `MQTTPluginSettings` is SimHub's `ReadCommonSettings`/`SaveCommonSettings` store. It holds only leftover template content and is effectively unused.
- **Password**: [Crypt.cs](Crypt.cs) encrypts it with DPAPI (`DataProtectionScope.CurrentUser`, fixed entropy) and stores it base64-encoded in `mqttpass`. `UnprotectString` can throw or return null when decryption fails, so callers wrap it in try/catch.
- **Actions**: these are stored in `mqttactions` as a `DataContractSerializer` XML dump of `Dictionary<string, (string topic, string message)>`. Each one is registered through `MQTTPlugin.AddAction(name, topic, message)` and appears in SimHub as `MQTTPlugin.<name>`. Actions are registered only when the settings UI is constructed (`LoadExistingActions`) or when the user saves.
- **Settings UI**: `GetWPFSettingsControl` returns `SettingsControlV2` (the redesign, which is the active one). The legacy `SettingsControl` is still compiled because a constant switch selects between the two, and because V2 reuses `SettingsControl.DeserializeDict`. Changes to the settings UI belong in V2.

## Known pitfalls

- Saved actions are registered with SimHub only in `SettingsControlV2` (constructor and save), not in `MQTTPlugin.Init`. After a SimHub restart, actions may not fire until the plugin's settings page has been opened.
- `ConnectMQTT` and `TestConnectionAsync` pass `null` as the CA certificate, so TLS connections don't verify the broker certificate against a CA.
- The UI event handlers in `SettingsControlV2` update WPF controls after `await`. Don't add `ConfigureAwait(false)` there, because the continuation must run on the UI thread.

## Project skills

[.claude/skills/](.claude/skills/) holds third-party skills installed from skills.sh. [skills-lock.json](skills-lock.json) records their source and hash; update them with `npx skills update -p`. The CLI may install a skill into `.agents/skills/` and leave only a junction in `.claude/skills/`. Replace that junction with a real copy of the folder and delete `.agents/`, so every skill is an ordinary folder under `.claude/skills/`.
- `code-testing` (dotnet/skills): the entry skill for designing and writing new tests; use `writing-mstest-tests` alongside it for the MSTest APIs.
  - Our per-phase test work counts as its **focused** scope, so do it inline. Keep any broad-scope `research.md`/`plan.md` in the scratchpad, never in the repo.
  - It refers to agents (`test-engineer`, `code-testing-*`) and skills (`code-testing-extensions`, `test-gap-analysis`, `assertion-quality`, `run-tests`) that aren't installed. Skip those references; don't spawn substitutes for them.
  - Its C# examples use target-typed `new()`, which isn't C# 7.3.
  - It says to prefer mocking, but no mocking library is installed. Use hand-written fakes behind the interfaces from T2–T5 unless a library is agreed on.
  - Its rule "fix the assertion, not the production code" applies to tests of existing behaviour. In bug-fix phases the tests pin the fixed behaviour from [PLAN.md](PLAN.md); if a test shows the plan is wrong, report it instead of bending the test.
- `csharp-async` (github/awesome-copilot): async/await guidance. Its `ValueTask` and `IAsyncEnumerable` suggestions need extra NuGet packages on .NET Framework 4.8, and its `ConfigureAwait(false)` advice doesn't apply to the UI handlers (see above).
- `mqtt-development` (mindrally/skills): general MQTT guidance (QoS, LWT, sessions, TLS). It is not specific to M2Mqtt, so check its suggestions against the M2Mqtt 4.3 API.
- `writing-mstest-tests` (dotnet/skills): MSTest conventions and assertion APIs, gated by the installed MSTest version. New-test design goes through `code-testing`. Some examples use newer C# such as collection expressions (`[ ... ]`); write tests in C# 7.3. It says to verify with `dotnet test`; for this classic non-SDK project, use the build and test commands recorded under Unit tests instead. Its APIs must exist in MSTest 3.1.1 (for example, use `Assert.ThrowsException`, not `Assert.ThrowsExactly`).
