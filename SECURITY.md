# Security policy

## Supported versions

Only the latest release gets security fixes.

| Version | Supported |
|---------|-----------|
| 3.0.x   | Yes       |
| < 3.0   | No        |

## Reporting a vulnerability

Please don't report security problems in a public issue. Use GitHub's private reporting instead: open the [Security tab](https://github.com/XtremeLead/SimHub-MQTT-Plugin/security), click **Report a vulnerability**, and describe the problem and how to reproduce it.

This is a hobby project maintained in my spare time, so I can't promise a response time, but I'll look at every report and let you know what I'll do with it.

## Known limitations

- **TLS doesn't verify the broker's certificate.** With **Use TLS** on, the connection is encrypted, but the plugin accepts any certificate the broker presents, including self-signed, expired or mismatched ones. Someone who can intercept your network traffic could pose as your broker and receive your user name and password. Only use the plugin with a broker on a network you trust, such as your local network.
- **The password is stored encrypted for your Windows user.** It's protected with Windows DPAPI, so anyone who can log in as your Windows user, or any program running as you, can decrypt it.
