# PalServerManager

PalServerManager is a small .NET 10 console application for managing a Palworld dedicated server. It can start, stop, and report the status of a configured server executable. It also runs a password-protected Telnet service so you can issue those commands remotely.

> **Security notice:** Telnet does not encrypt traffic. The password and commands are sent in plain text. Use this only on a trusted network, or protect access with a VPN/firewall. Do not expose the Telnet port directly to the public internet. Change the example password before running the program.

## Requirements

## Installation
- download the ZIP file.
- Copy the contents to the same directory where palserver.exe is located.
- Edit the config file to add a strong password and I recommend also changing the listening port.
- route the port on your router to the computer that is running the server.
- Start the PalServerManager.exe
- You need a Telnet client to connect remotely; on Windows, the built-in Telnet Client may need to be enabled first. also you can use the commonly used Putty client.
- example for logging into the servermanager with windows cmd: telnet ServerIP:23  then type your password and hit enter.
- after logging in, you can use the following commands: `help`, `startserver`, `stopserver`, `status`, `exit` or `bye`

## Configuration

Edit `appsettings.json` and set the listening port, a strong admin password, and the full path to the server executable:

```json
{
  "ServerSettings": {
	"Port": 23,
	"AdminPassword": "replace-this-with-a-strong-password",
	"ServerExePath": "PalServer.exe"
  }
}
```

`appsettings.json` must be located next to the built `PalServerManager.exe` (or in the application's base directory when running from source). The server executable is launched without additional command-line arguments, so configure an executable that can start correctly that way.

## Build and run

From the repository directory:

```powershell
dotnet build
```

To run the Telnet service from source:

```powershell
dotnet run
```

The service listens on the configured port on all network interfaces and continues running until the process is stopped. To run the published application, publish it and place the configured `appsettings.json` beside the resulting executable:

```powershell
dotnet publish -c Release
```

## Connect and use commands

Connect to the machine running PalServerManager using a Telnet client and the configured port. For example:

```text
telnet server-address:port-number
```

Replace `23` with the configured `Port` if you changed it. Enter the configured `AdminPassword` when prompted. Authentication allows up to three attempts; the password comparison is case-insensitive.

After connecting, enter one command per line:

| Command | Description |
| --- | --- |
| `help` | Lists the available commands |
| `startserver` | Starts the configured server executable if it is not already running |
| `stopserver` | Terminates the configured server process |
| `status` | Reports whether the server process is running, including process details when available |
| `exit` or `bye` | Closes the Telnet connection |

You can also run the management commands locally from a terminal, using the same configuration:

```powershell
.\PalServerManager.exe startserver
.\PalServerManager.exe status
.\PalServerManager.exe stopserver
```

When started without a command argument, PalServerManager runs the Telnet service. When invoked with one of the management commands, it performs that command and exits.

## Notes

- The service binds to all network interfaces, so restrict inbound access to the configured port with your firewall.
- `stopserver` forcibly terminates the server process; it does not request a graceful shutdown.
- The status and duplicate-process checks use the executable's filename, so they may also match another running process with the same name.
