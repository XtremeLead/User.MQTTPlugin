using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Xml;

namespace User.MQTTPlugin
{
    /// <summary>
    /// Interaction logic for SettingsControl.xaml
    /// </summary>
    public partial class SettingsControl : UserControl
    {
        public MQTTPlugin Plugin { get; }
        public MQTTClient Client { get; }
        private IDictionary<string, string> settings = new Dictionary<string, string>();
        private static readonly string CONNECTION_FAILED_MESSAGE = "Connection failed. Please check your settings or broker availability.";
        private static readonly string CONNECTION_SUCCESS_MESSAGE = "Successfully connected. Happy simracing!";
        private static readonly string CONNECTION_TEST_SUCCESS_MESSAGE = "Successfully connected. Click Save to Save your settings and connect.";
        private Dictionary<string, ValueTuple<string, string>> ActionsToStore = new Dictionary<string, ValueTuple<string, string>>();

        public SettingsControl()
        {
            InitializeComponent();

            settings = MQTTSettings.LoadSettings();
            txtBroker.Text = settings["mqttserver"];
            txtPort.Text = settings["mqttport"];
            txtUser.Text = settings["mqttuser"];
            txtPassword.Text = settings["mqttpass"];
            txtTopic.Text = settings["mqtttopic"];
            if (settings["mqttactions"] != "")
            {
                try
                {
                    ActionsToStore = (Dictionary<string, (string, string)>)DeserializeDict(settings["mqttactions"], ActionsToStore.GetType());
                    SimHub.Logging.Current.Info($"Loading MQTT Actions...");
                    int index = 0;
                    foreach (var action in ActionsToStore)
                    {
                        string name = action.Key;
                        string topic = action.Value.Item1;
                        string message = action.Value.Item2;
                        StackPanel newRow = NewRow(name, (topic, message));
                        newRow.Name = "NewRow";
                        stackPanelActions.Children.Add(newRow);
                        if (ActionsScrollViewer.Height < 200)
                        {
                            ActionsScrollViewer.Height += 200;
                        }
                        SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor();

                        controlsEditor.Name = $"ControlsEditor{index}";
                        controlsEditor.FriendlyName = action.Key;
                        controlsEditor.ActionName = $"MQTTPlugin.{action.Key}";
                        controlsEditor.Width = (3 * 95) + (2 * 10);
                        controlsEditor.HorizontalAlignment = HorizontalAlignment.Left;

                        stackPanelActions.Children.Add(controlsEditor);
                        btnSaveActions.Visibility = Visibility.Visible;

                        MQTTPlugin plugin = new MQTTPlugin();
                        plugin.AddAction(name, topic, message);

                        index++;
                    }


                }
                catch (Exception)
                {
                    SimHub.Logging.Current.Info($"Could not parse MQTT Actions!");
                }
            }


            Dictionary<string, string> MqttSettingsFound = settings
                .Where(kvp => kvp.Key.IndexOf("mqtt") > -1 && kvp.Value != "")
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

            SimHub.Logging.Current.Info($"Found {MqttSettingsFound.Count} MQTT settings!");

            if (MqttSettingsFound.Count == 0)
            {
                ConnectionStatus.Content = "Please provide MQTT settings.";
            }

            if (MQTTClient.CLIENT != null || MqttSettingsFound.Count > 0)
            {
                ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
                 CONNECTION_SUCCESS_MESSAGE :
                 CONNECTION_FAILED_MESSAGE;
            }
        }
        public SettingsControl(MQTTPlugin plugin) : this()
        {
            Plugin = plugin;
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttserver", sender);

        }

        private void TextBox_TextChanged_1(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttport", sender);
        }

        private void TextBox_TextChanged_2(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttuser", sender);
        }

        private void TextBox_TextChanged_3(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqttpass", sender);
        }

        private void TextBox_TextChanged_4(object sender, TextChangedEventArgs e)
        {
            StoreInSettings("mqtttopic", sender);
        }

        private void StoreInSettings(string key, object sender)
        {
            if (btnSave != null)
            {
                btnSave.IsEnabled = false;
            }
            TextBox textBox = sender as TextBox;
            if (textBox != null)
            {
                string value = textBox.Text;
                MQTTSettings.RememberSetting(key, value);
            }
        }

        private void SHButtonPrimary_Click(object sender, RoutedEventArgs e)
        {
            MQTTSettings.SaveSettings();
            ConnectionStatus.Content = MQTTClient.CLIENT.IsConnected ?
               CONNECTION_SUCCESS_MESSAGE :
               CONNECTION_FAILED_MESSAGE;
        }

        private void SHButtonPrimary_Click_1(object sender, RoutedEventArgs e)
        {
            try
            {
                bool connectionSuccessful = MQTTClient.TestConnection(txtBroker.Text, int.Parse(txtPort.Text), txtUser.Text, txtPassword.Text);
                ConnectionStatus.Content = connectionSuccessful ?
                    CONNECTION_TEST_SUCCESS_MESSAGE :
                    CONNECTION_FAILED_MESSAGE;
                Properties.Settings.Default["lastConnectionSuccessful"] = connectionSuccessful;
                btnSave.IsEnabled = connectionSuccessful;
            }
            catch (Exception)
            {
                ConnectionStatus.Content = CONNECTION_FAILED_MESSAGE;
            }
        }


        private void txtPort_KeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Key >= Key.D0 && e.Key <= Key.D9) ||
                (e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9))
            {
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        private void btnAddAction_Click(object sender, RoutedEventArgs e)
        {
            if (ActionsScrollViewer.Height < 200)
            {
                ActionsScrollViewer.Height += 45;
            }
            btnSaveActions.Visibility = Visibility.Visible;

            this.MinHeight += 35;
            StackPanel newRow = NewRow();
            newRow.Name = "NewRow";
            stackPanelActions.Children.Add(newRow);
        }

        private StackPanel NewRow(string name = "", ValueTuple<string, string>? values = null)
        {
            ValueTuple<string, string> ActionValues = new ValueTuple<string, string>("", "");
            if (values != null)
            {
                ActionValues = ((string, string))values;
            }
            StackPanel sp = new StackPanel();
            sp.Name = "StackPanelActionRow";
            TextBox txtActionName = new TextBox();
            TextBox txtTopic = new TextBox();
            TextBox txtMessage = new TextBox();
            Button btn = new Button();
            Button del = new Button();

            sp.Orientation = Orientation.Horizontal;

            // left top right bottom
            Thickness inputMargin = new Thickness(0, 10, 10, 0);
            Thickness buttonMargin = new Thickness(0, 10, 10, 0);
            Thickness lastButtonMargin = new Thickness(0, 10, 0, 0);

            txtActionName.Width = 95;
            txtActionName.HorizontalAlignment = HorizontalAlignment.Left;
            txtActionName.Margin = inputMargin;
            txtActionName.Name = "Name";
            txtActionName.Text = name;

            txtTopic.Width = 95;
            txtTopic.HorizontalAlignment = HorizontalAlignment.Left;
            txtTopic.Margin = inputMargin;
            txtTopic.Name = "Topic";
            txtTopic.Text = ActionValues.Item1;

            txtMessage.Width = 95;
            txtMessage.HorizontalAlignment = HorizontalAlignment.Left;
            txtMessage.Margin = inputMargin;
            txtMessage.Name = "Message";
            txtMessage.Text = ActionValues.Item2;

            btn.Height = 12;
            btn.Margin = buttonMargin;
            btn.Content = "Create Action";
            btn.Click += (sender, EventArgs) => { btnCreateAction_Click(sender, EventArgs, txtActionName.Text, txtTopic.Text, txtMessage.Text); };

            del.Height = 12;
            del.Margin = lastButtonMargin;
            del.Content = "X";
            del.Click += (sender, EventArgs) => { btnDeleteAction_Click(sender, EventArgs, sp, name); };

            sp.Children.Add(txtActionName);
            sp.Children.Add(txtTopic);
            sp.Children.Add(txtMessage);
            sp.Children.Add(btn);
            sp.Children.Add(del);

            return sp;
        }
        private void btnCreateAction_Click(object sender, RoutedEventArgs e, string action, string topic, string message)
        {
            Button btn = sender as Button;
            StackPanel spButtonParent = btn.Parent as StackPanel;
            StackPanel spButtonGrandParent = spButtonParent.Parent as StackPanel;
            SimHub.Plugins.UI.ControlsEditor controlsEditor = new SimHub.Plugins.UI.ControlsEditor();
            int index = spButtonGrandParent.Children.IndexOf(spButtonParent);

            controlsEditor.Name = $"ControlsEditor{index}";
            controlsEditor.FriendlyName = action;
            controlsEditor.ActionName = $"MQTTPlugin.{action}";
            controlsEditor.Width = (3 * 95) + (2 * 10);
            controlsEditor.HorizontalAlignment = HorizontalAlignment.Left;

            spButtonGrandParent.Children.Insert(index + 1, controlsEditor);

            MQTTPlugin plugin = new MQTTPlugin();
            plugin.AddAction(action, topic, message);
        }

        private void btnDeleteAction_Click(object sender, RoutedEventArgs e, StackPanel sp, string name)
        {
            StackPanel parent = sp.Parent as StackPanel;
            parent.Children.Remove(sp);
            MessageBox.Show($"dont forget to delete action {name}");
        }

        private void btnSaveActions_Click(object sender, RoutedEventArgs e)
        {

            foreach (var ActionRow in stackPanelActions.Children)
            {
                try
                {
                    StackPanel actionRow = (StackPanel)ActionRow;
                    TextBox name = actionRow.Children.OfType<TextBox>().Single(child => child.Name == "Name");
                    TextBox topic = actionRow.Children.OfType<TextBox>().Single(child => child.Name == "Topic");
                    TextBox message = actionRow.Children.OfType<TextBox>().Single(child => child.Name == "Message");

                    ValueTuple<string, string> ActionData = (topic.Text, message.Text);

                    if (ActionData.Item1 != "" && ActionData.Item2 != "")
                    {
                        ActionsToStore[name.Text] = ActionData;
                    }

                }
                catch (Exception) { }

            }


            Properties.Settings.Default["mqttActions"] = SerializeDict(ActionsToStore);
            Properties.Settings.Default.Save();
        }


        private string SerializeDict(Dictionary<string, ValueTuple<string, string>> tuple)
        {
            // serialize the dictionary
            DataContractSerializer serializer = new DataContractSerializer(tuple.GetType());

            using (StringWriter sw = new StringWriter())
            {
                using (XmlTextWriter writer = new XmlTextWriter(sw))
                {
                    // add formatting so the XML is easy to read in the log
                    writer.Formatting = Formatting.Indented;

                    serializer.WriteObject(writer, tuple);

                    writer.Flush();

                    return sw.ToString();
                }
            }
        }
        public static object DeserializeDict(string xml, Type toType)
        {
            using (Stream stream = new MemoryStream())
            {
                byte[] data = System.Text.Encoding.UTF8.GetBytes(xml);
                stream.Write(data, 0, data.Length);
                stream.Position = 0;
                DataContractSerializer deserializer = new DataContractSerializer(toType);
                return deserializer.ReadObject(stream);
            }
        }
    }
}
