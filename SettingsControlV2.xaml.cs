using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SimHub.Plugins.UI;

namespace User.MQTTPlugin
{
    public partial class SettingsControlV2 : UserControl
    {
        private static readonly Brush PlaceholderBrush = Brushes.Gray;

        // The width the page has always given the control-mapping editor below an action.
        private const double ControlsEditorWidth = (3 * 95) + (2 * 10);

        public SettingsControlV2()
        {
            InitializeComponent();

            MQTTPluginSettings settings = MQTTSettings.Current;
            txtBroker.Text = settings.Server;
            txtPort.Text = SettingsViewLogic.PortText(settings.Port);
            txtUser.Text = settings.Username;
            txtTopic.Text = settings.Topic;
            chkUseTls.IsChecked = settings.UseTls;
            txtPassword.Password = Crypt.UnprotectString(settings.EncryptedPassword);

            foreach (MqttActionSetting action in settings.Actions)
            {
                AddRowToUi(action.Name, action.Topic, action.Message, withControlsEditor: true);
            }

            ConnectionStatus.Text = SettingsViewLogic.InitialConnectionStatus(
                SettingsRules.HasBrokerConfigured(settings), MqttConnectionManager.Shared.IsConnected);
        }

        // --- Connection section ---

        // Any change to a connection field needs a new successful Test before Save. Used for the
        // TextChanged of the text fields and the PasswordChanged of the password field.
        private void ConnectionField_Changed(object sender, RoutedEventArgs e)
        {
            if (btnSave != null) btnSave.IsEnabled = false;
        }

        private void chkUseTls_Changed(object sender, RoutedEventArgs e)
        {
            string port = SettingsViewLogic.PortAfterTlsChange(chkUseTls.IsChecked, txtPort.Text);
            if (port != txtPort.Text) txtPort.Text = port;
        }

        /// <summary>The connection settings as entered, or null when the port isn't valid.</summary>
        private ConnectionSettings ReadConnectionFields()
        {
            if (!SettingsRules.TryParsePort(txtPort.Text, out int port)) return null;
            return new ConnectionSettings(txtBroker.Text, port, txtUser.Text, txtPassword.Password, txtTopic.Text, chkUseTls.IsChecked == true);
        }

        private async void btnSave_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettings connection = ReadConnectionFields();
            if (connection == null)
            {
                ConnectionStatus.Text = SettingsViewLogic.InvalidPortMessage;
                return;
            }

            btnSave.IsEnabled = false;
            ConnectionStatus.Text = "Connecting...";
            try
            {
                await MQTTSettings.SaveConnectionAsync(connection);
            }
            catch (Exception)
            {
                // MQTTSettings has logged the reason; the saved settings are unchanged.
                ConnectionStatus.Text = SettingsViewLogic.SaveFailedMessage;
                btnSave.IsEnabled = true;
                return;
            }
            ConnectionStatus.Text = MqttConnectionManager.Shared.IsConnected ? SettingsViewLogic.ConnectionSuccessMessage : SettingsViewLogic.ConnectionFailedMessage;
            btnSave.IsEnabled = true;
        }

        private async void btnTest_Click(object sender, RoutedEventArgs e)
        {
            ConnectionSettings testSettings = ReadConnectionFields();
            if (testSettings == null)
            {
                ConnectionStatus.Text = SettingsViewLogic.InvalidPortMessage;
                return;
            }

            ConnectionStatus.Text = "Testing connection...";
            try
            {
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

        // --- Actions section ---

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
                ControlsEditor controlsEditor = CreateControlsEditor(name, stackPanelActions.Children.Count);
                stackPanelActions.Children.Add(controlsEditor);
                row.Tag = controlsEditor;
            }
        }

        /// <summary>The control-mapping editor shown below a saved action; the row keeps it in its Tag.</summary>
        private static ControlsEditor CreateControlsEditor(string actionName, int index)
        {
            return new ControlsEditor
            {
                Name = $"ControlsEditor{index}",
                FriendlyName = actionName,
                ActionName = SettingsViewLogic.SimHubActionName(actionName),
                Width = ControlsEditorWidth,
                HorizontalAlignment = HorizontalAlignment.Left
            };
        }

        private void DeleteActionRow(Grid row)
        {
            int index = stackPanelActions.Children.IndexOf(row);
            if (index < 0) return;

            ControlsEditor editor = row.Tag as ControlsEditor;
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

            try
            {
                MQTTSettings.SaveActions(validation.Actions);
            }
            catch (Exception)
            {
                // MQTTSettings has logged the reason; the saved actions are unchanged.
                ActionsStatus.Text = SettingsViewLogic.SaveActionsFailedMessage;
                return;
            }

            // Register with the live plugin; this page may belong to an instance that has ended.
            MQTTPlugin.Instance?.RegisterActions(MQTTSettings.Current.Actions);

            foreach (int rowIndex in validation.SavedRows)
            {
                Grid row = rows[rowIndex];
                string name = texts[rowIndex].Item1;

                if (row.Tag is ControlsEditor existingEditor)
                {
                    existingEditor.FriendlyName = name;
                    existingEditor.ActionName = SettingsViewLogic.SimHubActionName(name);
                }
                else
                {
                    int index = stackPanelActions.Children.IndexOf(row);
                    ControlsEditor controlsEditor = CreateControlsEditor(name, index);
                    stackPanelActions.Children.Insert(index + 1, controlsEditor);
                    row.Tag = controlsEditor;
                }
            }

            ActionsStatus.Text = SettingsViewLogic.ActionsSavedMessage(validation.Actions.Count, validation.SkippedCount);
        }
    }
}
