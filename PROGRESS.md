# Voortgang

Voortgang op het [verbeterplan](PLAN.md). Werk dit bestand bij na elke afgeronde stap. Zet de nieuwste logregel bovenaan.

**Status:** ⬜ open · 🔄 bezig · ✅ klaar (gebouwd en getest) · ⏸️ wacht op beslissing

## Fasen

| Fase | Status | Opmerking |
|---|---|---|
| F0. Testproject | ✅ | Klassiek net48-project met MSTest 3.1.1 en `vstest.console` uit het pakket `Microsoft.TestPlatform`; commando's in CLAUDE.md |
| F1. Snelle, veilige bugfixes | ⬜ | |
| F2. Verbindingsbeheerder | ⬜ | |
| F3. Acties naar de plugin | ⬜ | |
| F4. Instellingenmodel en opslag | ⬜ | |
| F5. Opruimen en leesbaarheid | ⬜ | |
| F6. Documentatie | ⬜ | |

## Punten

| Code | Omschrijving | Fase | Status |
|---|---|---|---|
| B1 | DNS-fout bij opstarten zet de plugin uit | F1 | ⬜ |
| B2 | TLS-vinkje blokkeert het opslaan | F1 | ⬜ |
| B3 | Test verbreekt de live verbinding, Save past niet toe | F2 | ⬜ |
| B4 | Acties pas actief na openen van de instellingen | F3 | ⬜ |
| B5 | Instellingen weg na SimHub-update | F4 | ⬜ |
| B6 | Race conditions rond de verbinding | F2 | ⬜ |
| B7 | "Please provide MQTT settings" wordt nooit getoond | F1 | ⬜ |
| B8 | `AttachDelegate` bij elk bericht | F1 | ⬜ |
| B9 | Foutpaden rond het wachtwoord | F1 | ⬜ |
| B10 | Ruis in het log, stille publish | F1 | ⬜ |
| A1 | Verbindingsbeheerder in plaats van statische toestand | F2 | ⬜ |
| A2 | Eén getypeerd instellingenmodel | F4 | ⬜ |
| A3 | Acties bij de plugin | F3 | ⬜ |
| A4 | Berichtverwerking los van bijwerkingen | F1/F2 | ⬜ |
| A5 | Oude code en template-resten weg | F5 | ⬜ |
| L1–L6 | Leesbaarheid | F1/F4/F5 | ⬜ |
| T1–T5 | Testbaarheid | F1/F2/F4 | ⬜ |

## Beslissingen

| Code | Vraag | Besluit | Datum |
|---|---|---|---|
| D1 | Overstappen naar SimHub-opslag met migratie? | Ja: getypeerd model in `ReadCommonSettings`, eenmalige migratie uit de huidige `user.config` als de nieuwe opslag leeg is | 2026-10-21 |
| D2 | Timestamp: extra ISO 8601-veld? | Ja: `timestamp` blijft `nl-NL`, extra veld `timestampIso` (`DateTimeOffset.ToString("o")`) | 2026-10-21 |
| D3 | Oude `SettingsControl` verwijderen? | Ja, in F5; `DeserializeDict` gaat in F3 naar de serializer-helper | 2026-10-21 |

## Log

- **2026-10-21:** Karakteriseringstests voor de huidige code toegevoegd, vóór F1 en zonder wijzigingen aan de productiecode. Er zijn nu 53 tests, en drie runs na elkaar slaagden allemaal.
  - **Gedekt:** de `Crypt`-paden, het XML-formaat van `mqttactions` (vastgelegd als golden string), `MQTTSettings`, de berichtverwerking in `client_MqttMsgPublishReceived`, de vroege exits van `ConnectAndSubscribe`, `Publish` zonder verbinding en de watchdog-guards.
  - **Bugs vastgelegd als huidig gedrag:** B1, B2, B8, B9 en B10. Die tests zijn gemarkeerd met `Current behaviour (Bx)` en moeten in F1 bewust worden omgezet.
  - **Nieuwe bevinding:** Newtonsoft herschrijft een datumstring met offset in een JSON-payload naar de lokale tijdzone. `+05:00` wordt bijvoorbeeld `07:00:00+02:00`. Ook verdwijnen nullen aan het eind van een decimaal getal (`1.10` wordt `1.1`). Dit staat nog niet in PLAN.md.
  - **Nog niet testbaar zonder seams (T1–T5):** verbinden, abonneren en Test bij een live verbinding (B3), de backoff-stappen na de eerste, de "settings found"-check (B7, alleen de oorzaak is vastgelegd), `Init`/`End`/`AddAction` en de UI-logica in `SettingsControlV2`.

- **2026-10-21:** F0 afgerond. `User.MQTTPlugin.Tests` aangemaakt en opgenomen in `User.MQTTPlugin.sln`, en `InternalsVisibleTo("User.MQTTPlugin.Tests")` toegevoegd aan de plugin. Release-build geslaagd en 4 tests geslaagd (`CryptTests`: round-trip en geen platte tekst).
  - Het project is klassiek met `packages.config`, niet SDK-style. MSBuild 17.8 van de BuildTools kan `Microsoft.NET.Sdk` niet vinden, omdat alleen .NET SDK 5.0 geïnstalleerd is.
  - MSTest staat op 3.1.1. Vanaf 3.2 komen er transitieve pakketten bij, en die moeten met `packages.config` allemaal met de hand worden opgenomen.
  - De tests draaien op `vstest.console.exe` uit het NuGet-pakket `Microsoft.TestPlatform` 17.11.1, omdat de BuildTools geen testrunner bevatten.
  - Bouwen gebeurt in Release, omdat een Debug-build ook de plugin naar de SimHub-map schrijft.

- **2026-10-21:** Skill `code-testing` (dotnet/skills) geïnstalleerd in `.claude/skills/` voor het ontwerpen van nieuwe tests. De kanttekeningen staan in CLAUDE.md onder "Project skills".
- **2026-10-21:** Beslissingen D1, D2 en D3 genomen (zie tabel). B5 bevestigd: op de ontwikkel-pc staan 6 `user.config`-bestanden met MQTT-instellingen in steeds andere bedrijfsmappen (`SimHub`, `뙆`, `삎`, `퉡`, `퍒`, `퍒kak`). SimHub wijzigt per release de CompanyName, de versiemap blijft `1.0.0.0`, en `Upgrade()` lost het daarom niet op. `PluginsData\Common\MQTTPlugin.GeneralSettings.json` heeft updates wel overleefd.
- **2026-10-21:** Codebase geanalyseerd en [PLAN.md](PLAN.md) opgesteld. B1, B2 en het lege-wachtwoordpad van B9 zijn met experimenten bevestigd:
  - B1: de `MqttClient`-constructor gooit `SocketException` bij een onbekende host.
  - B2: een string toekennen aan een bool-instelling gooit `SettingsPropertyWrongTypeException`.
  - B9: `ProtectedData.Unprotect` op een lege waarde gooit `CryptographicException`.
- **2026-10-21:** Opzet: CLAUDE.md geschreven en de skills `csharp-async`, `mqtt-development` en `writing-mstest-tests` geïnstalleerd in `.claude/skills/`. Er wordt gewerkt op de branch `claude`.
