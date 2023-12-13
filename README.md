# MQTT Plugin

MQTT Plugin is a SimHub plugin that allows you to show MQTT data on your dashboard and also send data to your MQTT broker via SimHub Actions.

## Installation

Place the dll file in your SimHub installation folder.

## Usage

Provide connection settings in the Plugin configuration. Topic setting refers to the topic you will be subscribing to.

### Publishing
By clicking the Add button you will be asked to provide a name for the Action, a topic to publish to and a message to publish. 
Afterwards click CREATE ACTION and bind the Action to the input of your choice. Click Save.

### Subscribing
You can subscribe to a single topic (for now...) which can contain a single value (i.e. 'on' or 'off') or a JSON formatted string (key/value pairs).
This plugin returns a JSON object with the payload (the value in the topic), the topic and a timestamp.
Use JavaScript in your Dashboard to extract the data from this object.

```
const message = JSON.parse($prop('MQTTPlugin.MQTTMessage'));
if(message.topic == 'my/mqtt/topic') return message.payload.key;
```


## Contributing

Pull requests are welcome. For major changes, please open an issue first
to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License

[MIT](https://choosealicense.com/licenses/mit/)