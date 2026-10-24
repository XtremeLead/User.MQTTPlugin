---
paths:
  - "SettingsControlV2*"
  - "SettingsViewLogic.cs"
  - "MultiClickCounter.cs"
---

# Settings UI conventions

- `GetWPFSettingsControl` returns `SettingsControlV2`; the legacy `SettingsControl` was removed (D3).
- Handlers are named after their control (`btnTest_Click`, `btnSave_Click`, `btnSaveActions_Click`, `txtPort_KeyDown`). All connection fields share `ConnectionField_Changed`, which disables Save until a new successful Test.
- Save builds a `ConnectionSettings` from the fields; a port must be 1–65535. The page shows actions and only saves them on "Save actions", then calls `MQTTPlugin.Instance.RegisterActions`.
- Status texts and other logic go in `SettingsViewLogic`, with tests, not in the code-behind.
- Handlers update WPF controls after `await`, so never add `ConfigureAwait(false)` there.
- The page scrolls itself with one `ScrollViewer`, because SimHub doesn't scroll plugin pages (B12). Don't add nested scroll areas, because they swallow the mouse wheel. Status lines are `TextBlock`s with `TextWrapping="Wrap"`.
- `CreateControlsEditor` builds the control-mapping editor below a saved action; `SettingsViewLogic.SimHubActionName` gives its action name (`MQTTPlugin.<name>`).
- A triple click in any text or password field selects all its text. WPF's `ClickCount` doesn't reach 3 in SimHub, so `MultiClickCounter` counts the clicks itself (within the Windows double-click time).
