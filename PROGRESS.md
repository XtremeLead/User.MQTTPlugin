# Voortgang

Voortgang op het [verbeterplan](PLAN.md). Werk dit bestand bij na elke afgeronde stap. Zet de nieuwste logregel bovenaan.

**Status:** ⬜ open · 🔄 bezig · ✅ klaar (gebouwd en getest) · ⏸️ wacht op beslissing

## Fasen

| Fase | Status | Opmerking |
|---|---|---|
| F0. Testproject | ✅ | Klassiek net48-project met MSTest 3.1.1 en `vstest.console` uit het pakket `Microsoft.TestPlatform`; commando's in CLAUDE.md |
| F0b. Seams zonder gedragswijziging | ✅ | Toegevoegd op verzoek van de gebruiker, zie PLAN.md. 139 tests groen en handmatig gecontroleerd in SimHub |
| F1. Snelle, veilige bugfixes | ✅ | 158 tests groen; B1, B2 en acties handmatig bevestigd in SimHub |
| F2. Verbindingsbeheerder | ✅ | 186 tests groen; handmatig bevestigd in SimHub (opstarten, Test, Save met nieuw topic, gamewissel, acties, afsluiten) |
| F3. Acties naar de plugin | ✅ | Tests groen; B4, B12 en acties (opstarten, koppelingen, dubbele namen, hernoemen, verwijderen) handmatig bevestigd in SimHub |
| F4. Instellingenmodel en opslag | ⬜ | |
| F5. Opruimen en leesbaarheid | ⬜ | |
| F6. Documentatie | ⬜ | |

## Punten

| Code | Omschrijving | Fase | Status |
|---|---|---|---|
| B1 | DNS-fout bij opstarten zet de plugin uit | F1 | ✅ (ook handmatig bevestigd in SimHub) |
| B2 | TLS-vinkje blokkeert het opslaan | F1 | ✅ (ook handmatig bevestigd in SimHub) |
| B3 | Test verbreekt de live verbinding, Save past niet toe | F2 | ✅ (ook handmatig bevestigd in SimHub) |
| B4 | Acties pas actief na openen van de instellingen | F3 | ✅ (ook handmatig bevestigd in SimHub) |
| B5 | Instellingen weg na SimHub-update | F4 | ⬜ |
| B6 | Race conditions rond de verbinding | F2 | ✅ |
| B7 | "Please provide MQTT settings" wordt nooit getoond | F1 | ✅ |
| B8 | `AttachDelegate` bij elk bericht | F1 | ✅ |
| B9 | Foutpaden rond het wachtwoord | F1 | ✅ |
| B10 | Ruis in het log, stille publish | F1 | ✅ |
| B11 | JSON-payloads worden herschreven (tijdzone, decimalen) | F1 | ✅ |
| B12 | Instellingenscherm: meldingen afgekapt, niet scrollbaar | F3 | ✅ (ook handmatig bevestigd in SimHub) |
| A1 | Verbindingsbeheerder in plaats van statische toestand | F2 | ✅ |
| A2 | Eén getypeerd instellingenmodel | F4 | ⬜ |
| A3 | Acties bij de plugin | F3 | ✅ |
| A4 | Berichtverwerking los van bijwerkingen | F1/F2 | ✅ |
| A5 | Oude code en template-resten weg | F5 | ⬜ |
| L1–L6 | Leesbaarheid | F1/F4/F5 | 🔄 L6 ✅ (F1, behalve het oude `SettingsControl`, dat in F5 verdwijnt) |
| T1–T5 | Testbaarheid | F0b/F2 | ✅ T1 en T3 (F0b), T2, T4 en T5 (F2) |

## Beslissingen

| Code | Vraag | Besluit | Datum |
|---|---|---|---|
| D1 | Overstappen naar SimHub-opslag met migratie? | Ja: getypeerd model in `ReadCommonSettings`, eenmalige migratie uit de huidige `user.config` als de nieuwe opslag leeg is | 2026-10-21 |
| D2 | Timestamp: extra ISO 8601-veld? | Ja: `timestamp` blijft `nl-NL`, extra veld `timestampIso` (`DateTimeOffset.ToString("o")`) | 2026-10-21 |
| D4 | Gedeelde verbindingsbeheerder of één per plugininstantie? | Gedeeld (`MqttConnectionManager.Shared`), op advies | 2026-10-22 |
| D5 | Moet `Init` wachten op de eerste verbinding? | Nee, de eerste verbinding gebeurt op de achtergrond | 2026-10-22 |
| D6 | Dubbele actienamen? | Save weigeren met een melding; hoofd- en kleine letters tellen als dezelfde naam | 2026-10-23 |
| D3 | Oude `SettingsControl` verwijderen? | Ja, in F5; `DeserializeDict` gaat in F3 naar de serializer-helper | 2026-10-21 |

## Log

- **2026-10-24:** Driedubbelklikken in alle velden door de gebruiker bevestigd in SimHub.

- **2026-10-24:** Driedubbelklikken werkte in SimHub nog niet. De eerste versie gebruikte `MouseButtonEventArgs.ClickCount`, dat daar kennelijk niet tot 3 komt. Nu telt `MultiClickCounter` zelf: kliks op hetzelfde veld binnen de Windows-dubbelkliktijd (`GetDoubleClickTime`) vormen een reeks.
  - Op verzoek van de gebruiker werkt het nu in alle velden. De handler zit op `PreviewMouseLeftButtonDown` van de hele pagina en zoekt de `TextBox` of `PasswordBox` onder de muis, dus actierijen die later worden toegevoegd doen ook mee.
  - Er zijn nu 215 tests en ze slagen allemaal. Dit moet nog handmatig worden gecontroleerd.

- **2026-10-23:** F3 en B12 door de gebruiker bevestigd in SimHub: scrollen werkt, de melding staat er volledig en de rest van de pagina is in orde.

  Extra wens van de gebruiker, meteen gebouwd: driedubbelklikken in het veld MQTT Sub Topic selecteert alle tekst (`SelectAllOnTripleClick` op `PreviewMouseLeftButtonDown`, met `SettingsViewLogic.SelectsAllText`). Er zijn nu 208 tests en ze slagen allemaal. Dit moet nog handmatig worden gecontroleerd.

- **2026-10-23:** Handmatige controle van F3 door de gebruiker:
  - Acties werken zonder de instellingen te openen (B4).
  - Bestaande koppelingen blijven werken na "Save actions" (`ClearActions` en opnieuw registreren).
  - De melding over dubbele namen verschijnt.
  - Verwijderen werkt.
  - Hernoemen geeft een nieuwe actie. De knopkoppeling moet opnieuw worden gezet, en SimHub biedt dan zelf aan de koppeling aan de oude naam te verwijderen. Dat is verwacht gedrag (staat in CLAUDE.md).

  Gemeld: de melding over dubbele namen is afgekapt, en de pagina is niet te scrollen. Dat is opgenomen als B12 (PLAN.md) en meteen opgelost in `SettingsControlV2.xaml`: één `ScrollViewer` om de pagina, geen vaste hoogte, geen aparte scrollbox voor de acties, en statusregels als `TextBlock` die teruglopen. De 204 tests slagen.

- **2026-10-23:** F3 gebouwd. Er zijn nu 204 tests, en drie runs na elkaar slaagden allemaal.
  - **Registratie bij de plugin (A3, B4):** `Init` laadt de opgeslagen acties en registreert ze via `RegisterActions`. Die wist eerst alle acties van de plugin met `PluginManager.ClearActions(typeof(MQTTPlugin))` (publiek in SimHub, gevonden met reflectie) en voegt dan de huidige set toe. Hernoemde en verwijderde acties verdwijnen daardoor.
  - **Instellingenscherm:** bij het openen worden de acties alleen nog getoond. "Save actions" valideert met `ActionRules.Validate`, slaat op en roept `MQTTPlugin.Instance.RegisterActions` aan.
  - **Dubbele namen (D6):** Save wordt geweigerd met de melding "Duplicate action name 'X' — action names must be unique. Nothing was saved.". Hoofd- en kleine letters tellen als dezelfde naam.
  - **Nieuwe code:** `ActionRules`, `ActionRegistration` (met `IActionRegistry`/`SimHubActionRegistry`) en `SettingsViewLogic.DuplicateActionsMessage`. Het oude `SettingsControl` registreert niets meer.
  - **Nog handmatig te controleren:** of bestaande koppelingen onder Controls and events blijven werken na `ClearActions` en opnieuw registreren.

- **2026-10-23:** F2 afgerond. De gebruiker heeft Save met een nieuw topic bevestigd: het log toont direct "Connecting", "Connected" en "Subscribed to MQTT topic …" met het nieuwe topic.

- **2026-10-23:** F2 handmatig gecontroleerd in SimHub.
  - **In orde:** opstarten, Test met een onbereikbare broker (de live verbinding blijft staan), acties en afsluiten (geen Warn "closed unexpectedly").
  - **Gamewissel:** MQTT stopt en start weer automatisch. Daarmee is bevestigd dat SimHub `End` van de oude instantie aanroept vóór `Init` van de nieuwe (relevant voor B4).
  - **Save met een nieuw topic:** lastig te controleren. Daarom is een Info-logregel "Subscribed to MQTT topic …" toegevoegd, met tests; er zijn nog steeds 186 tests en drie runs na elkaar slaagden allemaal. Deze controle staat nog open.

- **2026-10-22:** F2 gebouwd. Er zijn nu 186 tests, en vijf runs na elkaar slaagden allemaal.
  - **De beheerder:** `MQTTClient.cs` is vervangen door `MqttConnectionManager`, met één gedeelde instantie (D4). `Start` en `Stop` krijgen de plugin als eigenaar mee, zodat de `End` van een oude instantie de verbinding van een nieuwe niet stopt. De eerste verbinding gebeurt op de achtergrond (D5).
  - **Nieuwe onderdelen:**
    - `IMqttClient`/`IMqttClientFactory` met een M2Mqtt-adapter (T2);
    - `IPluginLog` (T4) en `IClock` (T5);
    - een timer en een wachtfunctie die via de constructor binnenkomen (T5);
    - `ConnectionSettings` (de controles op broker en poort, en het ontsleutelde wachtwoord).
  - **B3:** Test gebruikt altijd een aparte client. Save (`ReconnectAsync`) verbreekt altijd en verbindt opnieuw met de opgeslagen instellingen, dus alleen het nieuwe topic blijft geabonneerd.
  - **B6:** er loopt maar één verbinding tegelijk (`connectGate`), en het blokkerende `Connect` draait buiten de lock. `Stop` annuleert de backoff-wachttijd, en een verbinding die na `Stop` klaar komt, wordt weggegooid. Dat laatste wordt ook getest voor de gamewissel, waar `Stop` van de oude en `Start` van de nieuwe instantie elkaar opvolgen.
  - **A4:** de plugin ontvangt berichten via het event `MessageReceived` en niet meer via `MQTTPlugin.Instance`.
  - **Opgeruimd:** het statische `IsConnected`, `ClientLock`, en het schrijven van `lastConnectionSuccessful` (werd nooit gelezen; de instelling blijft tot F4). Het oude `SettingsControl` is alleen aangepast zover nodig was om te bouwen.
  - **Nieuw gedrag:**
    - Bij een leeg topic wordt niet geabonneerd, met een Warn. Voorheen gooide M2Mqtt hier een exceptie.
    - Na een eigen disconnect (Save of Stop) komt er geen Warn "closed unexpectedly" meer.
    - De startstatus van het instellingenscherm hangt alleen af van "broker ingevuld" en "verbonden".
  - **Tests:** `MQTTClientTests` en `MessageReceivedTests` zijn vervangen door `MqttConnectionManagerTests` (met `ManagerHarness` en fakes in `ConnectionFakes.cs`), `ConnectionSettingsTests` en `M2MqttClientTests`.

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
