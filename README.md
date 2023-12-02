# MQTT Plugin

MQTT Plugin is a SimHub plugin that allows you to show MQTT data on your dashboard.

## Installation

Place the dll file in your SimHub installation folder.

## Usage

Provide connection settings in the Plugin configuration.
MQTT wildcards are supported.
This plugin returns a JSON object with the payload, topic and timestamp.
Use JavaScript in your Dashboard to extract the data.

```
const message = JSON.parse($prop('MQTTPlugin.MQTTMessage'));
if(message.topic == 'my/mqtt/topic') return message.payload;
```


## Contributing

Pull requests are welcome. For major changes, please open an issue first
to discuss what you would like to change.

Please make sure to update tests as appropriate.

## License

[MIT](https://choosealicense.com/licenses/mit/)