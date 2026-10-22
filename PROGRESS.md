# Voortgang

Voortgang op het [verbeterplan](PLAN.md). Werk dit bestand bij na elke afgeronde stap. Zet de nieuwste logregel bovenaan.

**Status:** ⬜ open · 🔄 bezig · ✅ klaar (gebouwd en getest) · ⏸️ wacht op beslissing

## Fasen

| Fase | Status | Opmerking |
|---|---|---|
| F0. Testproject | ✅ | Klassiek net48-project met MSTest 3.1.1 en `vstest.console` uit het pakket `Microsoft.TestPlatform`; commando's in CLAUDE.md |
| F0b. Seams zonder gedragswijziging | ✅ | Toegevoegd op verzoek van de gebruiker, zie PLAN.md. 139 tests groen en handmatig gecontroleerd in SimHub |
| F1. Snelle, veilige bugfixes | ✅ | 158 tests groen; B1, B2 en acties handmatig bevestigd in SimHub |
| F2. Verbindingsbeheerder | ⬜ | |
| F3. Acties naar de plugin | ⬜ | |
| F4. Instellingenmodel en opslag | ⬜ | |
| F5. Opruimen en leesbaarheid | ⬜ | |
| F6. Documentatie | ⬜ | |

## Punten

| Code | Omschrijving | Fase | Status |
|---|---|---|---|
| B1 | DNS-fout bij opstarten zet de plugin uit | F1 | ✅ (ook handmatig bevestigd in SimHub) |
| B2 | TLS-vinkje blokkeert het opslaan | F1 | ✅ (ook handmatig bevestigd in SimHub) |
| B3 | Test verbreekt de live verbinding, Save past niet toe | F2 | ⬜ |
| B4 | Acties pas actief na openen van de instellingen | F3 | ⬜ |
| B5 | Instellingen weg na SimHub-update | F4 | ⬜ |
| B6 | Race conditions rond de verbinding | F2 | ⬜ |
| B7 | "Please provide MQTT settings" wordt nooit getoond | F1 | ✅ |
| B8 | `AttachDelegate` bij elk bericht | F1 | ✅ |
| B9 | Foutpaden rond het wachtwoord | F1 | ✅ |
| B10 | Ruis in het log, stille publish | F1 | ✅ |
| B11 | JSON-payloads worden herschreven (tijdzone, decimalen) | F1 | ✅ |
| A1 | Verbindingsbeheerder in plaats van statische toestand | F2 | ⬜ |
| A2 | Eén getypeerd instellingenmodel | F4 | ⬜ |
| A3 | Acties bij de plugin | F3 | ⬜ |
| A4 | Berichtverwerking los van bijwerkingen | F1/F2 | 🔄 formatter en `timestampIso` klaar; event i.p.v. `Instance` in F2 |
| A5 | Oude code en template-resten weg | F5 | ⬜ |
| L1–L6 | Leesbaarheid | F1/F4/F5 | 🔄 L6 ✅ (F1, behalve het oude `SettingsControl`, dat in F5 verdwijnt) |
| T1–T5 | Testbaarheid | F0b/F2 | 🔄 T1 en T3 ✅ (F0b), T5 deels (tijdstip als parameter, `SleepSeconds`), T2 en T4 in F2 |

## Beslissingen

| Code | Vraag | Besluit | Datum |
|---|---|---|---|
| D1 | Overstappen naar SimHub-opslag met migratie? | Ja: getypeerd model in `ReadCommonSettings`, eenmalige migratie uit de huidige `user.config` als de nieuwe opslag leeg is | 2026-10-21 |
| D2 | Timestamp: extra ISO 8601-veld? | Ja: `timestamp` blijft `nl-NL`, extra veld `timestampIso` (`DateTimeOffset.ToString("o")`) | 2026-10-21 |
| D3 | Oude `SettingsControl` verwijderen? | Ja, in F5; `DeserializeDict` gaat in F3 naar de serializer-helper | 2026-10-21 |

## Log

- **2026-10-22:** D2 bevestigd. De gebruiker gebruikt zelf geen timestamps, maar `timestamp` en `timestampIso` blijven voor andere gebruikers van de plugin. De plugin vult ze alleen (`MessageFormatter.Format`) en leest ze nergens; ze staan voor dashboardbouwers beschreven in de README. In F6 moet de README `timestampIso` en het ongewijzigd doorgeven van payloads (B11) beschrijven.

- **2026-10-22:** F1 afgerond. De gebruiker heeft B2 bevestigd in SimHub:
  - TLS aan en uit zetten, Test en Save schrijft `mqtttls` naar `user.config`.
  - Met TLS aan via `user.config` blijft de plugin opnieuw verbinden met de broker, die geen TLS heeft.
  - TLS uit in SimHub, Test en Save geeft weer verbinding.

  Acties werken nog. Het dashboard (nieuwe payload-weergave en `timestampIso`) is niet apart genoemd.

- **2026-10-22:** B1 handmatig bevestigd in SimHub. Met `mqttserver=broker.invalid` in de actieve `user.config` toont het log "MQTT watchdog started" en "Connecting MQTT on broker.invalid", gevolgd door de Warn "Failed to connect … No such host is known" en "attempting reconnect (attempt 1, backoff 2s)". Na Test en Save met de juiste broker is de verbinding terug. `mqtttls` ontbreekt in `user.config`, omdat de instelling door B2 nooit is opgeslagen. B2 wordt zonder TLS-broker getest door het vinkje aan en weer uit te zetten, en dan Test en Save te doen.

- **2026-10-22:** Aanvulling op B5: de huidige `SimHubWPF.exe` heeft CompanyName `SimHub` (versie 1.0.0.0), dus de actieve `user.config` staat nu weer in `%LOCALAPPDATA%\SimHub\SimHubWPF.exe_Url_…\1.0.0.0\`. De vorige release gebruikte de map `퉡`. Wijzigingen in een oude map worden genegeerd. Voor handmatige tests: kies de map via de CompanyName van het exe-bestand, niet via de laatste wijzigingstijd.

- **2026-10-21:** F1 gebouwd. Er zijn nu 158 tests, en drie runs na elkaar slaagden allemaal. De tests met `Current behaviour (Bx)` zijn omgezet naar het gerepareerde gedrag.
  - **B1:** de client wordt binnen de `try` aangemaakt, in zowel `ConnectMQTT` als `TestConnectionAsync`. Een DNS-fout geeft nu een Warn en een mislukte verbinding, in plaats van een exceptie. `Init` koppelt eerst de properties en start de watchdog, en verbindt pas daarna (in een `try`). De reconnect-taak van de watchdog vangt en logt fouten.
  - **B2:** `SaveSettingsAsync` zet staged tekst om naar het type van de instelling, zodat `"True"` een bool wordt. Bij een fout wordt niets geschreven, wordt de staging gewist, volgt een Error-log en wordt de exceptie opnieuw gegooid. De Save-knop toont dan "Could not save the settings".
  - **B7:** "instellingen aanwezig" betekent nu "broker ingevuld" (`SettingsRules.HasBrokerConfigured`, in plaats van de telling). `ConnectAndSubscribe` controleert dat vóór de poort.
  - **B8:** `AttachDelegate` is uit de setter gehaald. Het veld is `volatile`.
  - **B9:** `UnprotectString` gooit nooit meer. Een lege waarde geeft zonder log `""`, en ongeldige of onontsleutelbare waarden geven `""` met een Error. Mislukt versleutelen slaat `""` op.
  - **B10 en L6:** ontvangen en gepubliceerde berichten worden alleen nog op Debug gelogd. Een publish zonder verbinding geeft een Warn. Fouten bij verbinden staan op Warn en dataverlies op Error. Het oude `SettingsControl` is niet aangepast (D3).
  - **B11:** payloads worden geparset met `DateParseHandling.None` en `FloatParseHandling.Decimal`. Een getal buiten het bereik van `decimal` (bijvoorbeeld `1e400`) wordt nu als tekst doorgegeven.
  - **A4 (deels) en D2:** het veld `timestampIso` is toegevoegd. `MessageFormatter.Format` krijgt een `DateTimeOffset`.
  - **Testhulp:** `LogCapture` (een log4net `MemoryAppender`) controleert logniveaus zonder T4.

- **2026-10-21:** De gebruiker heeft F0b in SimHub gecontroleerd (Debug-build, instellingenpagina): alles werkt nog. B11 (JSON-payloads worden herschreven) is met akkoord van de gebruiker aan PLAN.md toegevoegd en ingedeeld in F1.

- **2026-10-21:** F0b afgerond (op verzoek van de gebruiker vóór F1 ingevoegd, zie PLAN.md). Er zijn nu 139 tests, en drie runs na elkaar slaagden allemaal. Alleen de Release-build is gemaakt.
  - **Nieuwe pure helpers:**
    - `MessageFormatter`: berichtverwerking, met het tijdstip als parameter.
    - `ActionsSerializer`: de acties-(de)serialisatie. Het legacy `SettingsControl` houdt zijn eigen kopie tot F5.
    - `SettingsRules`: de "settings found"-telling, de TLS-check en het parsen van de poort.
    - `SettingsViewLogic`: statusteksten, de poortwissel bij TLS, de toetsfilter voor de poort, de veldtekst van acties, de volledigheidscheck en het opslagbericht.
    - `MQTTClient.GetBackoffDelaySeconds`.
  - **Seams:** `ISettingsStore`/`PropertiesSettingsStore` als `MQTTSettings.Store` (T3), `MQTTSettings.Reconnect` en `MQTTClient.SleepSeconds`. Alle directe toegang tot `Properties.Settings.Default` in `MQTTClient`, `MQTTSettings` en `SettingsControlV2` loopt nu via `Store`.
  - **Nieuw vastgelegd:** de volledige backoff-reeks 2, 5, 10, 30, 60, 60, … (een mislukte poging zet de teller niet terug), dat B7 een verse installatie "Connection failed" laat tonen, en dat een opgeslagen wijziging bij elke volgende Save opnieuw wordt weggeschreven (de staging-lijst wordt niet gewist). Verder dat de poortcontrole geen bereik heeft (`-1` en `70000` worden geaccepteerd), dat `"true"` in kleine letters niet als TLS telt, en dat Backspace en Delete niet als poorttoets gelden.
  - **Timestamp:** heeft geen voorloopnullen (`4-3-2026 05:06:07`). De test uit de vorige stap rekende op `dd-MM-yyyy` en zou op dagen 1–9 zijn mislukt; die is aangepast.
  - **Nog niet testbaar:** verbinden en abonneren bij een live verbinding (B3, wacht op T2 in F2), `Init`/`End`/`AddAction` (hebben de `PluginManager` van SimHub nodig), en de WPF-bediening zelf.

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
