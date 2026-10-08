// file: Program.cs
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

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
        string configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ServerConfig>(configJson);

        if (config is null || config.ServerSettings is null)
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

        // 5. Check server management commands (startserver, stopserver, status)
        if (args.Length > 0)
        {
            ProcessAdminCommand(args[0]);
            return;
        }

        Console.WriteLine($"[{GetTimestamp()}] Server starting on port {port}...");
        Console.WriteLine("Note: Only clients with the correct password can log in.");
        Console.WriteLine();

        // 3. Create TCP listener
        TcpListener listener = new(System.Net.IPAddress.Any, port);
        listener.Start();

        Console.WriteLine($"[{GetTimestamp()}] Server listening on: http://localhost:{port}");
        Console.WriteLine();

        // 4. Main loop - wait for connections
        while (true)
        {
            try
            {
                TcpClient client = await listener.AcceptTcpClientAsync();

                Console.WriteLine($"[{GetTimestamp()}] New connection from {client.Client.RemoteEndPoint}");
                Console.WriteLine();

                // Start processing task
                _ = Task.Run(async () => await HandleConnection(client, adminPassword));
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
    private static void ProcessAdminCommand(string command)
    {
        ArgumentNullException.ThrowIfNull(command);

        switch (command.ToLower(System.Globalization.CultureInfo.CurrentCulture))
        {
            case "startserver":
                StartServer();
                break;

            case "stopserver":
                StopServer();
                break;

            case "status":
                ShowStatus();
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
    static void StartServer()
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Starting Server ===");
        Console.WriteLine();

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: Config file not found: {configPath}");
            return;
        }

        string configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ServerConfig>(configJson);

        if (config?.ServerSettings == null || string.IsNullOrEmpty(config.ServerSettings.ServerExePath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: ServerExePath not defined in config!");
            Console.WriteLine("Please set 'ServerExePath' in appsettings.json.");
            return;
        }

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
    static void StopServer()
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Stopping Server ===");
        Console.WriteLine();

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: Config file not found: {configPath}");
            return;
        }

        string configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ServerConfig>(configJson);

        if (config?.ServerSettings == null || string.IsNullOrEmpty(config.ServerSettings.ServerExePath))
        {
            Console.WriteLine($"[{GetTimestamp()}] No server configured. Set ServerExePath in appsettings.json.");
            return;
        }

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
    static void ShowStatus()
    {
        Console.WriteLine($"[{GetTimestamp()}] === PalServerManager - Server Status ===");
        Console.WriteLine();

        string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

        if (!File.Exists(configPath))
        {
            Console.WriteLine($"[{GetTimestamp()}] Error: Config file not found: {configPath}");
            return;
        }

        string configJson = File.ReadAllText(configPath);
        var config = JsonSerializer.Deserialize<ServerConfig>(configJson);

        if (config?.ServerSettings == null || string.IsNullOrEmpty(config.ServerSettings.ServerExePath))
        {
            Console.WriteLine($"[{GetTimestamp()}] No server configured. Set ServerExePath in appsettings.json.");
            return;
        }

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
    /// Checks if a .exe is currently running.
    /// </summary>
    static Process[] GetServerProcesses(string exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return [];

        try
        {
            string fullExePath = Path.GetFullPath(exePath);
            Process[] candidates = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(fullExePath));
            var matchingProcesses = new System.Collections.Generic.List<Process>();

            foreach (Process process in candidates)
            {
                try
                {
                    string? processPath = process.MainModule?.FileName;
                    if (!string.IsNullOrEmpty(processPath) &&
                        string.Equals(Path.GetFullPath(processPath), fullExePath, StringComparison.OrdinalIgnoreCase))
                    {
                        matchingProcesses.Add(process);
                    }
                    else
                    {
                        process.Dispose();
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    process.Dispose();
                }
                catch (InvalidOperationException)
                {
                    process.Dispose();
                }
            }

            return matchingProcesses.ToArray();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    Error checking server status: {ex.Message}");
            return [];
        }
    }

    /// <summary>
    /// Handles a client connection and authenticates the user.
    /// </summary>
    /// <param name="client">The TCP client</param>
    /// <param name="adminPassword">The allowed admin password</param>
    static async Task HandleConnection(TcpClient client, string adminPassword)
    {
        using (client)
        using (NetworkStream stream = client.GetStream())
        {
            // Configure stream
            stream.ReadTimeout = 10000;
            stream.WriteTimeout = 10000;

            // --- IMPROVED: Line-based authentication with timeout ---
            Console.WriteLine($"[{GetTimestamp()}] Authentication mode enabled.");
            Console.WriteLine($"[{GetTimestamp()}] Waiting for password input...");
            Console.WriteLine();

            // Send welcome message (with authentication notice)
            string authMessage = "PalServerManager Telnet Server v1.0\r\n" +
                                "=========================================\r\n" +
                                "SECURE ACCESS ENABLED\r\n" +
                                "Please enter your password:\r\n" +
                                "----------------------------------------\r\n";

            byte[] authBytes = Encoding.UTF8.GetBytes(authMessage);
            await stream.WriteAsync(authBytes, 0, authBytes.Length);
            await stream.FlushAsync();

            Console.WriteLine($"[{GetTimestamp()}] Welcome message sent.");

            // IMPROVED: Wait for complete password entry (line-based)
            Console.WriteLine($"[{GetTimestamp()}] Waiting for your password input...");

            bool isAuthenticated = false;
            int failedAttempts = 0;

            while (!isAuthenticated && failedAttempts < 3)
            {
                try
                {
                    string line = await ReadLineAsync(stream);
                    if (line == null)
                    {
                        // Client has disconnected
                        Console.WriteLine($"[{GetTimestamp()}] Client disconnected during authentication.");
                        break;
                    }

                    line = line.Trim();
                    if (!string.IsNullOrEmpty(line))
                    {
                        Console.WriteLine($"[{GetTimestamp()}] Line received: '{line}'");

                        // Password check for complete lines only
                        if (line.Equals(adminPassword, StringComparison.OrdinalIgnoreCase))
                        {
                            // PASSWORD CORRECT!
                            Console.WriteLine($"[{GetTimestamp()}] Authentication successful!");
                            isAuthenticated = true;

                            // Send success message
                            string successMessage = "=========================================\r\n" +
                                                   "ACCESS GRANTED\r\n" +
                                                   "Welcome to PalServerManager!\r\n" +
                                                   "----------------------------------------\r\n";

                            byte[] successBytes = Encoding.UTF8.GetBytes(successMessage);
                            await stream.WriteAsync(successBytes, 0, successBytes.Length);
                            await stream.FlushAsync();

                            Console.WriteLine($"[{GetTimestamp()}] Success message sent.");

                            // Now process normal commands
                            await ProcessCommands(stream, client);
                        }
                        else
                        {
                            // WRONG PASSWORD!
                            failedAttempts++;
                            Console.WriteLine($"[{GetTimestamp()}] Wrong password attempted. Attempts remaining: {3 - failedAttempts}");

                            // Send error message (without showing the real password!)
                            string errorMessage = "=========================================\r\n" +
                                               "WRONG PASSWORD!\r\n" +
                                               "Access denied.\r\n" +
                                               "Please try again.\r\n" +
                                               "----------------------------------------\r\n";

                            byte[] errorBytes = Encoding.UTF8.GetBytes(errorMessage);
                            await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
                            await stream.FlushAsync();

                            Console.WriteLine($"[{GetTimestamp()}] Error message sent.");
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
                // Client was not authenticated - terminate connection
                Console.WriteLine($"[{GetTimestamp()}] Connection without authentication terminated.");
            }
        }
    }

    /// <summary>
    /// Processes commands after successful authentication.
    /// </summary>
    static async Task ProcessCommands(NetworkStream stream, TcpClient client)
    {
        // Process the first command after authentication (do not ignore)
        bool isFirstCommand = true;

        while (true)
        {
            try
            {
                string commandText = await ReadLineAsync(stream);
                if (commandText == null)
                {
                    Console.WriteLine($"[{GetTimestamp()}] Client disconnected.");
                    break;
                }

                commandText = commandText.Trim();

                if (string.IsNullOrEmpty(commandText))
                    continue;

                // Process the first command after authentication
                Console.WriteLine($"[{GetTimestamp()}] Command received: {commandText}");

                // Trim and execute
                commandText = commandText.Trim();

                if (commandText == "exit" || commandText == "bye")
                {
                    Console.WriteLine($"[{GetTimestamp()}] Closing connection to {client.Client.RemoteEndPoint}...");

                    // Send disconnect message before closing
                    string disconnectMessage = "=========================================\r\n" +
                                              "CONNECTION IS BEING CLOSED\r\n" +
                                              "Thank you for visiting!\r\n" +
                                              "=========================================\r\n";

                    byte[] disconnectBytes = Encoding.UTF8.GetBytes(disconnectMessage);
                    try
                    {
                        await stream.WriteAsync(disconnectBytes, 0, disconnectBytes.Length);
                        await stream.FlushAsync();
                        Console.WriteLine($"[{GetTimestamp()}] Disconnect message sent.");
                    }
                    catch (IOException)
                    {
                        // Client has already disconnected - that's okay
                        Console.WriteLine($"[{GetTimestamp()}] Client has already disconnected.");
                    }

                    // Cleanly close socket (both directions)
                    try
                    {
                        client.Client.Shutdown(SocketShutdown.Both);
                        Console.WriteLine($"[{GetTimestamp()}] Socket closed.");
                    }
                    catch (IOException)
                    {
                        // Socket is already closed - that's okay
                    }

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
                    Console.WriteLine($"[{GetTimestamp()}] Command 'startserver' recognized.");

                    byte[] responseBytes = Encoding.UTF8.GetBytes("Command: startserver\r\nServer is starting...\r\n");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await stream.FlushAsync();

                    StartServer();
                }
                else if (commandText == "stopserver")
                {
                    Console.WriteLine($"[{GetTimestamp()}] Command 'stopserver' recognized.");

                    byte[] responseBytes = Encoding.UTF8.GetBytes("Command: stopserver\r\nServer is stopping...\r\n");
                    await stream.WriteAsync(responseBytes, 0, responseBytes.Length);
                    await stream.FlushAsync();

                    StopServer();
                }
                else if (commandText == "status")
                {
                    Console.WriteLine($"[{GetTimestamp()}] Command 'status' recognized.");

                    // Generate status information
                    string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");

                    if (!File.Exists(configPath))
                    {
                        string errorMsg = "[X] Config file not found!\r\n";
                        byte[] errorBytes = Encoding.UTF8.GetBytes(errorMsg);
                        await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
                        await stream.FlushAsync();
                        return;
                    }

                    var config = JsonSerializer.Deserialize<ServerConfig>(File.ReadAllText(configPath));

                    if (string.IsNullOrEmpty(config.ServerSettings.ServerExePath))
                    {
                        string errorMsg = "[X] No server configured!\r\n";
                        byte[] errorBytes = Encoding.UTF8.GetBytes(errorMsg);
                        await stream.WriteAsync(errorBytes, 0, errorBytes.Length);
                        await stream.FlushAsync();
                        Console.WriteLine("No server configured. Set ServerExePath in appsettings.json.");
                        return;
                    }

                    string exePath = config.ServerSettings.ServerExePath;

                    // Generate status text
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

                    // Send status to client
                    byte[] statusBytes = Encoding.UTF8.GetBytes(statusText);
                    await stream.WriteAsync(statusBytes, 0, statusBytes.Length);
                    await stream.FlushAsync();

                    // Also display in console
                    Console.WriteLine(statusText);
                }
                else
                {
                    // Ignore all other commands (expandable later)
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
                    telnetState = value == 240 ? 0 : value == 255 ? 4 : 4;
                    break;
            }
        }
    }
}

/// <summary>
/// Configuration model for the server.
/// </summary>
public class ServerConfig
{
    public ServerSettings ServerSettings { get; set; } = new ServerSettings();
}

/// <summary>
/// Server settings from the config file.
/// </summary>
public class ServerSettings
{
    /// <summary>
    /// The port on which the server listens (default: 23 for Telnet).
    /// </summary>
    public int Port { get; set; } = 23;

    /// <summary>
    /// The password with which admins can log in.
    /// IMPORTANT: Do NOT store this in plain text in a file!
    /// In production, you should use hashing (e.g., bcrypt).
    /// </summary>
    public string AdminPassword { get; set; } = "";

    /// <summary>
    /// Path to the external .exe that should be started/stopped.
    /// Leave empty if no external .exe should be managed.
    /// </summary>
    public string ServerExePath { get; set; } = "";
}