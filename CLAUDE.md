# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

A SimHub plugin (.NET Framework 4.8, WPF, old-style non-SDK `.csproj` with `packages.config`) that subscribes to one MQTT topic and exposes received messages as the SimHub property `MQTTPlugin.MQTTMessage`. It also publishes MQTT messages through user-defined SimHub Actions. MQTT is handled by M2Mqtt 4.3; JSON by Newtonsoft.Json.

**Language level:** the project sets no `LangVersion`, so it compiles as **C# 7.3**. Don't use file-scoped namespaces, nullable reference types, records, `init`, switch expressions or other newer features. SimHub only loads .NET Framework plugins, so upgrading to modern .NET is not an option.

## Git

- **Never commit or push.** The user makes all commits themselves. Leave changes in the working tree and say what changed.
- Work on the `claude` branch (branched from `develop`; `master` is the main branch). Don't switch branches unless the user asks.

## Plan and progress

- [PLAN.md](PLAN.md) is the agreed improvement plan. Bugs, architecture, readability and testability items have fixed codes (B1, A1, L1, T1, ...), work is grouped into phases (F0, F0b, F1–F6, all done by 2026-10-24), and decisions are listed (D1–D6, all decided). Refer to items by code; give new bugs or decisions the next free code (B13, D7, ...). Don't change the plan's content without the user's agreement.
- [PROGRESS.md](PROGRESS.md) tracks progress. Update it after every completed step: the phase and item status, any decisions with their date, and a new log line at the top. Mark an item ✅ only after it is built and its tests pass.
- Both files are written in Dutch, so keep them in Dutch.

## Build

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe' .\User.MQTTPlugin.csproj
```

- **Debug** builds write output straight into `C:\Program Files (x86)\SimHub\`, which is how the plugin gets deployed. That means the build needs write access to that folder, and it fails if SimHub is running, because SimHub locks the DLL. **Release** builds write to `bin\Release\`.
- If the WPF markup compiler fails with `MC1000` ("Unknown build error, 'Object reference not set…'"), usually after a XAML file was removed, run the build again with `-t:Rebuild`.
- SimHub assemblies (`SimHub.Plugins`, `GameReaderCommon`, `SimHub.Logging`, `log4net`) are referenced from the SimHub install folder, so SimHub must be installed to build.
- NuGet packages go in `packages\`. If they are missing, restore them with `nuget restore User.MQTTPlugin.sln` or `msbuild -t:restore -p:RestorePackagesConfig=true`.
- There is no linter. Behaviour that unit tests can't cover (SimHub integration, the WPF UI, a real broker) is verified by building, restarting SimHub, and checking the SimHub log. Everything is logged through `SimHub.Logging.Current`: lifecycle at Info, failed connects and dropped publishes at Warn, failures that lose data at Error, and every received or published message at Debug only.

## Unit tests

Unit tests are required. Use **MSTest**.

- **New and changed code:** every change comes with unit tests for the logic it adds or modifies.
- **Existing code:** all testable logic is covered (F0, F0b). Keep it that way: logic you move or refactor keeps its tests.
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

- **Characterization tests:** to pin a known bug before fixing it, start the test's comment with `Current behaviour (Bx)`; the fix changes that test on purpose. None are left. Tests that need a failing host use `broker.invalid`, which always fails DNS and never connects.
- **Connection tests:** never start `MqttConnectionManager.Shared`. Build a manager with [ConnectionFakes.cs](User.MQTTPlugin.Tests/ConnectionFakes.cs) `ManagerHarness`, which wires up `FakeMqttClientFactory`/`FakeMqttClient` (connect, drop, receive, block a connect through `ConnectHook`), `FakePluginLog`, `FakeClock`, `FakeTimer`, an instant backoff delay and editable saved settings. Wait on the returned tasks with a timeout.
- **Settings seams:** `MQTTSettings.Reconnect` and `MQTTSettings.Log` are static fields that tests replace, and `MQTTSettings.Initialize` takes the store (`IPluginSettingsStore`) and the legacy loader. [TestState.cs](User.MQTTPlugin.Tests/TestState.cs) `Reset()` initializes `MQTTSettings` from a `FakePluginSettingsStore` with migrated defaults, a `FakePluginLog` and a no-op reconnect. Call it in `[TestInitialize]` and `[TestCleanup]`. Only `LegacySettingsTests` reads the real `Properties.Settings` (the test host has no `user.config`); nothing writes it.
- **Log assertions:** code with an `IPluginLog` is checked with `FakePluginLog`. For code that still logs through `SimHub.Logging.Current` directly (`Crypt`), [LogCapture.cs](User.MQTTPlugin.Tests/LogCapture.cs) attaches a log4net `MemoryAppender`; use it in a `using` block.
- **Pure helpers:** `MessageFormatter`, `SettingsMigration`, `ActionsSerializer`, `ActionRules`, `SettingsRules`, `SettingsViewLogic`, `ConnectionSettings.FromSettings`, `MultiClickCounter` and `MqttConnectionManager.GetBackoffDelaySeconds`. Put new testable logic there, or in similar helpers, rather than in the WPF code-behind.
- **Adding test files:** the project doesn't glob, so add each new `.cs` file as `<Compile Include>` in the test `.csproj`. Add a new package to `packages.config` together with its `HintPath` reference, and list every dependency it has.
- **Testability:** don't let tests connect to a real broker, write SimHub's settings file or write `Properties.Settings`. What still can't be unit tested (`Init`, `End`, `AddAction`, which need SimHub's `PluginManager`, and the WPF controls) is checked by hand in SimHub.

## Architecture

- **`MQTTPlugin`** ([MQTTPlugin.cs](MQTTPlugin.cs)) is the SimHub entry point (`IPlugin`, `IDataPlugin`, `IWPFSettingsV2`). SimHub rebuilds plugins on every game change, so `Init`/`End` run many times per session. `MQTTPlugin.Instance` is a static pointer to the live instance; the settings page uses it to re-register actions after "Save actions". The only SimHub property is `MQTTPlugin.MQTTMessage` (the template's `CurrentDateTime` was removed in F5). `DataUpdate` is intentionally empty, because data arrives from MQTT and not from the game loop.
- **`MqttConnectionManager`** ([MqttConnectionManager.cs](MqttConnectionManager.cs)) owns the live connection (F2). There is one shared instance, `MqttConnectionManager.Shared` (D4).
  - `Init` subscribes to `MessageReceived` and calls `Start(this)`. That starts the 5s watchdog and connects in the background, so `Init` doesn't block (D5). `End` calls `Stop(this)` and unsubscribes. Start and Stop take the plugin as owner, so the `End` of an old instance can't stop the connection a newer instance started.
  - The client field is guarded by `sync`. Only one connect runs at a time (`connectGate`), and the blocking M2Mqtt `Connect` runs outside the lock. A connect that finishes after `Stop` is discarded.
  - `ReconnectAsync()` (used by Save through `MQTTSettings.Reconnect`) always drops the connection and reconnects with the saved settings, so a new topic replaces the old one. `TestAsync(ConnectionSettings)` uses a separate client and never touches the live one.
  - The watchdog (`CheckConnection`) waits the backoff from `BackoffSchedule` (2, 5, 10, 30, 60 s) and reconnects. Stop cancels the wait.
  - Received messages go through `MessageFormatter`. That parses the payload as JSON without converting date strings or decimals, and falls back to the raw string. It wraps the result as `{topic, payload, timestamp, timestampIso}`, where `timestamp` uses the `nl-NL` culture (D2) and `timestampIso` is ISO 8601 with offset. The result is raised as `MessageReceived`, the plugin stores it in `MqttMessage`, and dashboards `JSON.parse` this value (see README).
  - Dependencies come in through the constructor: `IMqttClientFactory`/`IMqttClient` ([MqttClientAdapter.cs](MqttClientAdapter.cs), wrapping M2Mqtt), `IPluginLog` and `IClock` ([PluginServices.cs](PluginServices.cs)), the settings loader, the delay and the timer.
  - The async methods return tasks that UI code can await.
- **Settings** (F4, A2, B5): one typed model, `MQTTPluginSettings` ([MQTTPluginSettings.cs](MQTTPluginSettings.cs)): `Server`, `Port` (int), `Username`, `EncryptedPassword`, `Topic`, `UseTls` and `Actions` (a list of `MqttActionSetting`), plus `SettingsVersion`.
  - SimHub stores the model as JSON through `ReadCommonSettings`/`SaveCommonSettings` ([SettingsStore.cs](SettingsStore.cs) `SimHubSettingsStore`), in `C:\Program Files (x86)\SimHub\PluginsData\Common\MQTTPlugin.GeneralSettings.json`. Unlike `user.config`, this location survives SimHub updates. SimHub only rewrites the file when it changes and keeps old versions in `_Backups`.
  - **`MQTTSettings`** ([MQTTSettings.cs](MQTTSettings.cs)) holds the current model. `Init` calls `Initialize`, and the settings page saves with `SaveConnectionAsync` (encrypts the password, then reconnects) and `SaveActions`. Every save writes a copy and only then replaces `Current`, so readers on other threads see a complete set and a failed save changes nothing. `End` doesn't save.
  - **Migration (D1):** when `SettingsVersion` is 0 (a file from before F4), `Initialize` reads the old `Properties.Settings` once (`LegacySettings.Load`, the `user.config` of the current SimHub version), converts it with `SettingsMigration.FromLegacy`, and saves it as version 1. A new user without `user.config` gets the defaults. `user.config` is never written; `Properties/Settings.settings` stays only for this.
  - The settings page writes nothing while typing. Save builds a `ConnectionSettings` from the fields. A port must be 1–65535; a migrated port that wasn't valid is stored as 0.
- **Password**: [Crypt.cs](Crypt.cs) encrypts it with DPAPI (`DataProtectionScope.CurrentUser`, fixed entropy) and stores it base64-encoded in `EncryptedPassword` (the same format as the old `mqttpass`, so it migrates as is). `UnprotectString` never throws: an empty stored value, invalid base64 or a failed decryption all give `""`, and failures are logged as Error. `ProtectString` stores `""` when encryption fails.
- **Actions**: these are stored in `MQTTPluginSettings.Actions`. Each one appears in SimHub as `MQTTPlugin.<name>` and publishes its message to its topic through `MqttConnectionManager.Shared.Publish`. Before F4 they were a `DataContractSerializer` XML dump in `mqttactions`; `ActionsSerializer` still reads that format for the migration.
  - The plugin owns registration (F3, A3). `Init` calls `RegisterActions(MQTTSettings.Current.Actions)`. That calls `ActionRegistration.Register`, which clears all of the plugin's actions (`PluginManager.ClearActions(typeof(MQTTPlugin))`) and adds the current set, so renamed and removed actions disappear (B4).
  - The settings page only shows the actions. Its "Save actions" validates the rows with `ActionRules.Validate`: incomplete rows are skipped, and duplicate names (ignoring case) refuse the whole save (D6). It then saves them with `MQTTSettings.SaveActions` and calls `MQTTPlugin.Instance.RegisterActions`.
  - `IActionRegistry` is the test seam; `SimHubActionRegistry` is the real one.
- **Settings UI**: `GetWPFSettingsControl` returns `SettingsControlV2`. The legacy `SettingsControl` was removed in F4 (D3).
  - Handlers are named after their control (`btnTest_Click`, `btnSave_Click`, `btnSaveActions_Click`, `txtPort_KeyDown`). All connection fields share `ConnectionField_Changed`, which disables Save until a new successful Test.
  - `CreateControlsEditor` builds the control-mapping editor below a saved action, and `SettingsViewLogic.SimHubActionName` gives its action name (`MQTTPlugin.<name>`).
  - The page scrolls itself with one `ScrollViewer`, because SimHub doesn't scroll plugin pages (B12). Don't add nested scroll areas, because they swallow the mouse wheel.
  - A triple click in any text or password field selects all its text. WPF's `ClickCount` doesn't reach 3 there, so `MultiClickCounter` counts the clicks itself.

## Known pitfalls

- SimHub calls `End` on the old plugin instance before `Init` on the new one when the game changes (seen in F2), so the connection briefly stops and starts again.
- Renaming an action creates a new action name. A control mapping made under Controls and events for the old name stays behind and has to be mapped again.
- `M2MqttClientFactory` passes `null` as the CA certificate, so TLS connections don't verify the broker certificate against a CA.
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
