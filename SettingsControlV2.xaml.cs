using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace User.MQTTPlugin
{
    public partial class SettingsControlV2 : UserControl
    {
        public MQTTPlugin Plugin { get; }
        private IDictionary<string, string> settings = new Dictionary<string, string>();
        private Dictionary<string, ValueTuple<string, string>> ActionsToStore = new Dictionary<string, ValueTuple<string, string>>();

        private static readonly Brush PlaceholderBrush = Brushes.Gray;

        public SettingsControlV2() : this(MQTTPlugin.Instance) { }

        public SettingsControlV2(MQTTPlugin plugin)
        {
            Plugin = plugin;
            InitializeComponent();

            settings = MQTTSettings.LoadSettings();
            txtBroker.Text = settings["mqttserver"];
            txtPort.Text = settings["mqttport"];
            txtUser.Text = settings["mqttuser"];
            txtTopic.Text = settings["mqtttopic"];
            chkUseTls.IsChecked = SettingsRules.IsTlsEnabled(settings);

            txtPassword.Password = Crypt.UnprotectString(settings["mqttpass"]);

            LoadExistingActions();

            ConnectionStatus.Text = SettingsViewLogic.InitialConnectionStatus(
                SettingsRules.HasBrokerConfigured(settings), MqttConnectionManager.Shared.IsConnected);
        }

        private void LoadExistingActions()
        {
            if (string.IsNullOrEmpty(settings["mqttactions"])) return;

            try
            {
                ActionsToStore = ActionsSerializer.Deserialize(settings["mqttactions"]);
                foreach (var action in ActionsToStore)
                {
                    AddRowToUi(action.Key, action.Value.Item1, action.Value.Item2, withControlsEditor: true);
                }
            }
            catch (Exception ex)
            {
                SimHub.Logging.Current.Error($"Could not parse MQTT Actions! {ex.Message}");
                ActionsStatus.Text = "Could not load previously saved actions — check the log.";
            }
        }

        // --- Connection section (unchanged behavior from SettingsControl) ---

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e) => StoreInSettings("mqttserver", sender);
        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e) => StoreInSettings("mqttport", sender);
        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e) => StoreInSettings("mqttuser", sender);
        private void PasswordBox_TextChanged_3(object sender, RoutedEventArgs e)
        {
            PasswordBox passwordBox = sender as PasswordBox;
            if (passwordBox != null)
            {
                if (btnSave != null) btnSave.IsEnabled = false;
                string encrypted = Crypt.ProtectString(passwordBox.Password);
                MQTTSettings.RememberSetting("mqttpass", encrypted);
            }
        }
        private void TextBox_TextChanged_4(object sender, TextChangedEventArgs e) => StoreInSettings("mqtttopic", sender);

        private void chkUseTls_Changed(object sender, RoutedEventArgs e)
        {
            MQTTSettings.RememberSetting("mqtttls", chkUseTls.IsChecked == true ? "True" : "False");
            string port = SettingsViewLogic.PortAfterTlsChange(chkUseTls.IsChecked, txtPort.Text);
            if (port != txtPort.Text) txtPort.Text = port;
        }

        private void StoreInSettings(string key, object sender)
        {
            if (btnSave != null) btnSave.IsEnabled = false;
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string value = textBox.Text;
                if (key == "mqttpass") value = Crypt.ProtectString(value);
                MQTTSettings.RememberSetting(key, value);
            }
        }

        private async void SHButtonPrimary_Click(object sender, RoutedEventArgs e)
        {
            btnSave.IsEnabled = false;
            ConnectionStatus.Text = "Connecting...";
            try
            {
                await MQTTSettings.SaveSettingsAsync();
            }
            catch (Exception)
            {
                // SaveSettingsAsync has logged the reason and discarded the staged values.
                ConnectionStatus.Text = SettingsViewLogic.SaveFailedMessage;
                btnSave.IsEnabled = true;
                return;
            }
            ConnectionStatus.Text = MqttConnectionManager.Shared.IsConnected ? SettingsViewLogic.ConnectionSuccessMessage : SettingsViewLogic.ConnectionFailedMessage;
            btnSave.IsEnabled = true;
        }

        private async void SHButtonPrimary_Click_1(object sender, RoutedEventArgs e)
        {
            if (!SettingsRules.TryParsePort(txtPort.Text, out int port))
            {
                ConnectionStatus.Text = "Invalid port.";
                return;
            }

            ConnectionStatus.Text = "Testing connection...";
            try
            {
                var testSettings = new ConnectionSettings(txtBroker.Text, port, txtUser.Text, txtPassword.Password, txtTopic.Text, chkUseTls.IsChecked == true);
                bool connectionSuccessful = await MqttConnectionManager.Shared.TestAsync(testSettings);
                ConnectionStatus.Text = connectionSuccessful ? SettingsViewLogic.ConnectionTestSuccessMessage : SettingsViewLogic.ConnectionFailedMessage;
                btnSave.IsEnabled = connectionSuccessful;
            }
            catch (Exception)
            {
                ConnectionStatus.Text = SettingsViewLogic.ConnectionFailedMessage;
            }
        }

        [DllImport("user32.dll")]
        private static extern uint GetDoubleClickTime();

        private readonly MultiClickCounter fieldClicks = new MultiClickCounter(GetDoubleClickTime());

        /// <summary>
        /// A triple click in any text or password field on this page selects all its text.
        /// Attached to the whole page in the preview (tunnelling) phase, so it also covers action
        /// rows added later and runs before the field turns the click into a word selection.
        /// </summary>
        private void SelectAllOnTripleClick(object sender, MouseButtonEventArgs e)
        {
            Control field = FindTextField(e.OriginalSource as DependencyObject);
            if (field == null) return;

            int clicks = fieldClicks.Register(field, e.Timestamp);
            if (!SettingsViewLogic.SelectsAllText(clicks)) return;

            field.Focus();
            if (field is TextBox textBox) textBox.SelectAll();
            else if (field is PasswordBox passwordBox) passwordBox.SelectAll();
            e.Handled = true;
        }

        private static Control FindTextField(DependencyObject element)
        {
            while (element != null && !(element is TextBox) && !(element is PasswordBox))
            {
                element = element is Visual ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
            }
            return element as Control;
        }

        private void txtPort_KeyDown(object sender, KeyEventArgs e)
        {
            e.Handled = !SettingsViewLogic.IsPortKey(e.Key);
        }

        // --- Actions section (redesigned) ---

        private void btnAddAction_Click(object sender, RoutedEventArgs e)
        {
            AddRowToUi("", "", "", withControlsEditor: false);
        }

        /// <summary>
        /// Adds an action row. A saved action also gets the control-mapping editor below it; the
        /// action itself is registered by the plugin (Init and RegisterActions), not here.
        /// </summary>
        private void AddRowToUi(string name, string topic, string message, bool withControlsEditor)
        {
            Grid row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });

            TextBox txtName = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = name };
            TextBox txtTopic = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = topic };
            TextBox txtMessage = new TextBox { Margin = new Thickness(0, 0, 10, 0), Text = message };
            Button btnDelete = new Button { Content = "✕", Width = 28, Height = 22, ToolTip = "Remove this row" };

            if (string.IsNullOrEmpty(name)) SetupPlaceholder(txtName, "e.g. ToggleClimate");
            if (string.IsNullOrEmpty(topic)) SetupPlaceholder(txtTopic, "e.g. simhub/commands/climate");
            if (string.IsNullOrEmpty(message)) SetupPlaceholder(txtMessage, "e.g. toggle");

            Grid.SetColumn(txtName, 0);
            Grid.SetColumn(txtTopic, 1);
            Grid.SetColumn(txtMessage, 2);
            Grid.SetColumn(btnDelete, 3);
            row.Children.Add(txtName);
            row.Children.Add(txtTopic);
            row.Children.Add(txtMessage);
            row.Children.Add(btnDelete);

            btnDelete.Click += (s, e) => DeleteActionRow(row);

            stackPanelActions.Children.Add(row);

            if (withControlsEditor && !string.IsNullOrEmpty(name))
            {
                SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor
                {
                    Name = $"ControlsEditor{stackPanelActions.Children.Count}",
                    FriendlyName = name,
                    ActionName = $"MQTTPlugin.{name}",
                    Width = (3 * 95) + (2 * 10),
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                stackPanelActions.Children.Add(controlsEditor);
                row.Tag = controlsEditor;
            }
        }

        private void DeleteActionRow(Grid row)
        {
            int index = stackPanelActions.Children.IndexOf(row);
            if (index < 0) return;

            SimHub.Plugins.UI.ControlsEditor editor = row.Tag as SimHub.Plugins.UI.ControlsEditor;
            stackPanelActions.Children.Remove(row);

            if (editor != null)
            {
                string actionName = editor.FriendlyName;
                stackPanelActions.Children.Remove(editor);
                MessageBox.Show(
                    $"'{actionName}' was removed from this list. SimHub may still keep its Control Mapping under Controls and events — " +
                    "you can delete that mapping there if you no longer need it. This plugin can't remove it automatically.",
                    "Action removed");
            }
        }

        private void SetupPlaceholder(TextBox box, string placeholder)
        {
            box.Tag = placeholder;
            box.Text = placeholder;
            box.Foreground = PlaceholderBrush;

            box.GotFocus += (s, e) =>
            {
                if (Equals(box.Foreground, PlaceholderBrush))
                {
                    box.Text = "";
                    box.ClearValue(TextBox.ForegroundProperty);
                }
            };
            box.LostFocus += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(box.Text))
                {
                    box.Text = (string)box.Tag;
                    box.Foreground = PlaceholderBrush;
                }
            };
        }

        private string GetRealText(TextBox box)
        {
            return SettingsViewLogic.RealText(box.Text, Equals(box.Foreground, PlaceholderBrush));
        }

        private void btnSaveActions_Click(object sender, RoutedEventArgs e)
        {
            var rows = stackPanelActions.Children.OfType<Grid>().ToList();
            var texts = rows
                .Select(row => (GetRealText((TextBox)row.Children[0]), GetRealText((TextBox)row.Children[1]), GetRealText((TextBox)row.Children[2])))
                .ToList();

            ActionsValidation validation = ActionRules.Validate(texts);
            if (validation.HasDuplicates)
            {
                ActionsStatus.Text = SettingsViewLogic.DuplicateActionsMessage(validation.DuplicateNames);
                return;
            }

            foreach (int rowIndex in validation.SavedRows)
            {
                Grid row = rows[rowIndex];
                string name = texts[rowIndex].Item1;

                if (row.Tag is SimHub.Plugins.UI.ControlsEditor existingEditor)
                {
                    existingEditor.FriendlyName = name;
                    existingEditor.ActionName = $"MQTTPlugin.{name}";
                }
                else
                {
                    int index = stackPanelActions.Children.IndexOf(row);
                    SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor
                    {
                        Name = $"ControlsEditor{index}",
                        FriendlyName = name,
                        ActionName = $"MQTTPlugin.{name}",
                        Width = (3 * 95) + (2 * 10),
                        HorizontalAlignment = HorizontalAlignment.Left
                    };
                    stackPanelActions.Children.Insert(index + 1, controlsEditor);
                    row.Tag = controlsEditor;
                }
            }

            ActionsToStore = validation.Actions;
            MQTTSettings.Store["mqttactions"] = ActionsSerializer.Serialize(ActionsToStore);
            MQTTSettings.Store.Save();

            // Register with the live plugin; this page may belong to an instance that has ended.
            MQTTPlugin.Instance?.RegisterActions(ActionsToStore);

            ActionsStatus.Text = SettingsViewLogic.ActionsSavedMessage(ActionsToStore.Count, validation.SkippedCount);
        }
    }
}