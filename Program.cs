// file: Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;

/// <summary>
/// PalServerManager - Telnet Server with Password Protection and Server Management
/// 
/// This program creates a secure Telnet server that only allows clients
/// who successfully authenticate with a password.
/// The password is stored in a separate config file.
/// </summary>
class Program
{
    /// <summary>
    /// Gets the current timestamp for logging purposes.
    /// </summary>
    private static string GetTimestamp() => DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

    static async Task Main(string[] args)
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager Telnet Server ===");
        Console.WriteLine();

        // 1. Load configuration file
        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: Config file not found: {configPath}");
            Console.WriteLine("Please create an appsettings.json file with the admin password.");
            return;
        }

        // 2. Read JSON configuration
        ServerConfig? config;
        try
        {
            string configJson = File.ReadAllText(configPath);
            config = JsonSerializer.Deserialize<ServerConfig>(configJson);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetTimestamp()}] Error reading config: {ex.Message}");
            return;
        }

        if (config?.ServerSettings is null)
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: Invalid config or ServerSettings is missing!");
            return;
        }

        if (string.IsNullOrEmpty(config.ServerSettings.ServerExePath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: ServerExePath not defined in config!");
            Console.WriteLine("Please set 'ServerExePath' in appsettings.json.");
            return;
        }

        string adminPassword = config.ServerSettings.AdminPassword;
        int port = config.ServerSettings.Port;

        // 3. Check server management commands (startserver, stopserver, status)
        if (args.Length > 0)
        {
            ProcessAdminCommand(args[0], config);
            return;
        }

        Console.WriteLine($"[{GetTimestamp()}] Server starting on port {port}...");
        Console.WriteLine("Note: Only clients with the correct password can log in.");
        Console.WriteLine();

        // 4. Create TCP listener
        TcpListener listener = new(IPAddress.Any, port);
        listener.Start();

        Console.WriteLine($"[{GetTimestamp()}] Server listening on: localhost:{port}");
        Console.WriteLine();

        // 5. Main loop - wait for connections
        while (true)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync();

                Console.WriteLine($"[{GetTimestamp()}] New connection from {client.Client.RemoteEndPoint}");
                Console.WriteLine();

                // Start processing task
                _ = Task.Run(async () => await HandleConnection(client, adminPassword, config));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{GetTimestamp()}] Error: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Processes admin commands (startserver, stopserver, status).
    /// </summary>
    private static void ProcessAdminCommand(string command, ServerConfig config)
    {
        ArgumentNullException.ThrowIfNull(command);

        switch (command.ToLowerInvariant())
        {
            case "startserver":
                StartServer(config);
                break;

            case "stopserver":
                StopServer(config);
                break;

            case "status":
                ShowStatus(config);
                break;

            default:
                Console.WriteLine($"[{GetTimestamp()}] Unknown command: {command}");
                Console.WriteLine("Available commands: startserver, stopserver, status");
                break;
        }
    }

    /// <summary>
    /// Starts an external .exe from the config.
    /// </summary>
    static void StartServer(ServerConfig config)
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Starting Server ===");
        Console.WriteLine();

        string exePath = Path.GetFullPath(config.ServerSettings.ServerExePath);

        // Check if the .exe is already running
        if (IsServerRunning(exePath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Server is already started: {exePath}");
            return;
        }

        // Start the .exe
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = Path.GetDirectoryName(exePath)!,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            Console.WriteLine($"[{GetTimestamp()}] Server started: {exePath}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetTimestamp()}] Error starting server: {ex.Message}");
        }
    }

    static bool IsServerRunning(string exePath)
    {
        Process[] processes = GetServerProcesses(exePath);
        bool isRunning = processes.Length > 0;

        foreach (Process process in processes)
        {
            process.Dispose();
        }

        return isRunning;
    }

    /// <summary>
    /// Stops an external .exe.
    /// </summary>
    static void StopServer(ServerConfig config)
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Stopping Server ===");
        Console.WriteLine();

        string exePath = Path.GetFullPath(config.ServerSettings.ServerExePath);
        Process[] processes = GetServerProcesses(exePath);

        if (processes.Length == 0)
        {
            Console.WriteLine($"[{GetTimestamp()}] Server is not started.");
            return;
        }

        // Stop the .exe
        try
        {
            foreach (Process process in processes)
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit();
                }
            }

            Console.WriteLine($"[{GetTimestamp()}] Server stopped: {exePath}");
            Console.WriteLine();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{GetTimestamp()}] Error stopping server: {ex.Message}");
        }
    }

    /// <summary>
    /// Shows the status of the .exe (running or not).
    /// </summary>
    static void ShowStatus(ServerConfig config)
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Server Status ===");
        Console.WriteLine();

        string exePath = config.ServerSettings.ServerExePath;

        // Show status
        if (IsServerRunning(exePath))
        {
            Console.WriteLine("[OK] Server is running!");
            Console.WriteLine($"    Path: {exePath}");

            try
            {
                Process[] processes = GetServerProcesses(exePath);
                if (processes.Length > 0)
                {
                    Process process = processes[0];
                    Console.WriteLine($"    PID: {process.Id}");
                    Console.WriteLine($"    Start time: {process.StartTime}");
                    Console.WriteLine($"    Uptime: {(DateTime.Now - process.StartTime).TotalMinutes:F2} minutes");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    Error retrieving process details: {ex.Message}");
            }
        }
        else
        {
            Console.WriteLine("[X] Server is NOT running!");
            Console.WriteLine($"    Path: {exePath}");
        }

        Console.WriteLine();
    }

    /// <summary>
    /// Checks if an .exe is currently running.
    /// </summary>
    static Process[] GetServerProcesses(string exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return Array.Empty<Process>();
        }

        try
        {
            string fullExePath = Path.GetFullPath(exePath);
            Process[] candidates = Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(fullExePath));
            var matchingProcesses = new List<Process>();

            foreach (Process process in candidates)
            {
                bool keepProcess = false;

                try
                {
                    string? processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(processPath) &&
                        string.Equals(
                            Path.GetFullPath(processPath),
                            fullExePath,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        matchingProcesses.Add(process);
                        keepProcess = true;
                    }
                }
                catch (Exception ex) when (
                    ex is ArgumentException or
                    InvalidOperationException or
                    NotSupportedException or
                    System.ComponentModel.Win32Exception or
                    System.Security.SecurityException)
                {
                    // The process may have exited or access may be denied.
                }
                finally
                {
                    if (!keepProcess)
                    {
                        process.Dispose();
                    }
                }
            }

            return matchingProcesses.ToArray();
        }
        catch (Exception ex) when (
            ex is ArgumentException or
            InvalidOperationException or
            NotSupportedException or
            IOException or
            System.ComponentModel.Win32Exception or
            System.Security.SecurityException)
        {
            Console.WriteLine($"    Error checking server status: {ex.Message}");
            return Array.Empty<Process>();
        }
    }

    /// <summary>
    /// Handles a client connection and authenticates the user.
    /// </summary>
    static async Task HandleConnection(TcpClient client, string adminPassword, ServerConfig config)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;

            Console.WriteLine($"[{GetTimestamp()}] Authentication mode enabled.");
            Console.WriteLine($"[{GetTimestamp()}] Waiting for password input...");
            Console.WriteLine();

            string authMessage = "PalServerManager Telnet Server v1.0\r\n" +
                                "=========================================\r\n" +
                                "SECURE ACCESS ENABLED\r\n" +
                                "Please enter your password:\r\n" +
                                "----------------------------------------\r\n";

            byte[] authBytes = Encoding.UTF8.GetBytes(authMessage);
            await stream.WriteAsync(authBytes, 0, authBytes.Length);
            await stream.FlushAsync();

            bool isAuthenticated = false;
            int failedAttempts = 0;

            while (!isAuthenticated && failedAttempts < 3)
            {
                try
                {
                    string? line = await ReadLineAsync(stream);
                    if (line == null)
                    {
                        Console.WriteLine($"[{GetTimestamp()}] Client disconnected during authentication.");
                        break;
                    }

                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        Console.WriteLine($"[{GetTimestamp()}] Line received: '{line}'");

                        if (line.Equals(adminPassword, StringComparison.OrdinalIgnoreCase))
                        {
                            Console.WriteLine($"[{GetTimestamp()}] Authentication successful!");
                            isAuthenticated = true;

                            string successMessage = "=========================================\r\n" +
                                                   "ACCESS GRANTED\r\n" +
                                                   "Welcome to PalServerManager!\r\n" +
                                                   "----------------------------------------\r\n";

                            byte[] successBytes = Encoding.UTF8.GetBytes(successMessage);
                            await stream.WriteAsync(successBytes, 0, successBytes.Length);
                            await stream.FlushAsync();

                            await ProcessCommands(stream, client, config);
                        }
                        else
                        {
                            failedAttempts++;
                            Console.WriteLine($"[{GetTimestamp()}] Wrong password attempted. Attempts remaining: {3 - failedAttempts}");

                            string errorMessage = "=========================================\r\n" +
                                               "WRONG PASSWORD!\r\n" +
                                               "Access denied.\r\n" +
                                               "Please try again.\r\n" +
                                               "----------------------------------------\r\n";

                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                            await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
                            await stream.FlushAsync();
                        }
                    }
                }
                catch (IOException)
                {
                    Console.WriteLine($"[{GetTimestamp()}] Client disconnected.");
                    break;
                }
            }

            if (!isAuthenticated)
            {
                Console.WriteLine($"[{GetTimestamp()}] Connection without authentication terminated.");
            }
        }
    }

    /// <summary>
    /// Processes commands after successful authentication.
    /// </summary>
    static async Task ProcessCommands(NetworkStream stream, TcpClient client, ServerConfig config)
    {
        while (true)
        {
            try
            {
                string? commandText = await ReadLineAsync(stream);
                if (commandText == null)
                {
                    Console.WriteLine($"[{GetTimestamp()}] Client disconnected.");
                    break;
                }

                commandText = commandText.Trim();
                if (string.IsNullOrEmpty(commandText))
                    continue;

                Console.WriteLine($"[{GetTimestamp()}] Command received: {commandText}");

                if (commandText == "exit" || commandText == "bye")
                {
                    Console.WriteLine($"[{GetTimestamp()}] Closing connection to {client.Client.RemoteEndPoint}...");

                    string disconnectMessage = "=========================================\r\n" +
                                              "CONNECTION IS BEING CLOSED\r\n" +
                                              "Thank you for visiting!\r\n" +
                                              "=========================================\r\n";

                    byte[] disconnectBytes = Encoding.UTF8.GetBytes(disconnectMessage);
                    try
                    {
                        await stream.WriteAsync(disconnectBytes, 0, disconnectBytes.Length);
                        await stream.FlushAsync();
                    }
                    catch (IOException) { /* Client disconnected */ }

                    try
                    {
                        client.Client.Shutdown(SocketShutdown.Both);
                    }
                    catch (IOException) { /* Socket closed */ }

                    break;
                }
                else if (commandText == "help")
                {
                    string helpMessage = "Available commands:\r\n" +
                                        "  help - Shows this help\r\n" +
                                        "  exit - Ends the connection\r\n" +
                                        "  startserver - Starts the server\r\n" +
                                        "  stopserver - Stops the server\r\n" +
                                        "  status - Shows server status\r\n";

                    byte[] helpBytes = Encoding.UTF8.GetBytes(helpMessage);
                    await stream.WriteAsync(helpBytes, 0, helpBytes.Length);
                    await stream.FlushAsync();
                }
                else if (commandText == "startserver")
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes("Command: startserver\r\nServer is starting...\r\n");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await stream.FlushAsync();

                    StartServer(config);
                }
                else if (commandText == "stopserver")
                {
                    byte[] responseBytes = Encoding.UTF8.GetBytes("Command: stopserver\r\nServer is stopping...\r\n");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await stream.FlushAsync();

                    StopServer(config);
                }
                else if (commandText == "status")
                {
                    string exePath = config.ServerSettings.ServerExePath;
                    bool isRunning = IsServerRunning(exePath);
                    string statusText = "";

                    if (isRunning)
                    {
                        try
                        {
                            Process[] processes = GetServerProcesses(exePath);
                            if (processes.Length > 0)
                            {
                                Process process = processes[0];
                                statusText = $"[OK] Server is running!\r\n" +
                                           $"    Path: {exePath}\r\n" +
                                           $"    PID: {process.Id}\r\n" +
                                           $"    Start time: {process.StartTime}\r\n" +
                                           $"    Uptime: {(DateTime.Now - process.StartTime).TotalMinutes:F2} minutes\r\n";
                            }
                        }
                        catch (Exception ex)
                        {
                            statusText = $"[OK] Server is running!\r\n" +
                                       $"    Path: {exePath}\r\n" +
                                       $"    Error in details: {ex.Message}\r\n";
                        }
                    }
                    else
                    {
                        statusText = "[X] Server is NOT running!\r\n" +
                                    $"    Path: {exePath}\r\n";
                    }

                    byte[] statusBytes = Encoding.UTF8.GetBytes(statusText);
                    await stream.WriteAsync(statusBytes, 0, statusBytes.Length);
                    await stream.FlushAsync();
                    Console.WriteLine(statusText);
                }
                else
                {
                    Console.WriteLine($"[{GetTimestamp()}] Unknown command: {commandText}");
                    byte[] unknownMessage = Encoding.UTF8.GetBytes("Unknown command. Enter 'help' to see available commands.\r\n");
                    await stream.WriteAsync(unknownMessage, 0, unknownMessage.Length);
                    await stream.FlushAsync();
                }
            }
            catch (IOException)
            {
                Console.WriteLine($"[{GetTimestamp()}] Client disconnected.");
                break;
            }
        }
    }

    static async Task<string?> ReadLineAsync(NetworkStream stream)
    {
        using var line = new MemoryStream();
        byte[] buffer = new byte[1];
        int telnetState = 0;

        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer.AsMemory(0, 1));
            if (bytesRead == 0)
                return line.Length == 0 ? null : Encoding.UTF8.GetString(line.ToArray());

            byte value = buffer[0];

            switch (telnetState)
            {
                case 0:
                    if (value == 255)
                        telnetState = 1;
                    else if (value == '\r' || value == '\n')
                        return Encoding.UTF8.GetString(line.ToArray());
                    else if (value != 0)
                        line.WriteByte(value);
                    break;

                case 1:
                    if (value == 251 || value == 253)
                        telnetState = value == 251 ? 2 : 3;
                    else if (value == 250)
                        telnetState = 4;
                    else
                        telnetState = 0;
                    break;

                case 2:
                case 3:
                    byte responseCommand = telnetState == 2 ? (byte)254 : (byte)252;
                    await stream.WriteAsync(new byte[] { 255, responseCommand, value }, 0, 3);
                    telnetState = 0;
                    break;

                case 4:
                    if (value == 255)
                        telnetState = 5;
                    break;

                case 5:
                    telnetState = value == 240 ? 0 : 4;
                    break;
            }
        }
    }
}

public class ServerConfig
{
    public ServerSettings ServerSettings { get; set; } = new ServerSettings();
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public class ServerSettings
{
    private string serverExePath = "";

    public int Port { get; set; } = 23;
    public string AdminPassword { get; set; } = "";
    public string ServerExePath { get => serverExePath; set => serverExePath = value; }
    private string GetDebuggerDisplay() => ToString() ?? string.Empty;
}