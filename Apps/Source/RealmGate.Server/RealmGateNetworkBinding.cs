using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace RealmGate.Server;

public sealed class RealmGateNetworkBindException : IOException
{
    public RealmGateNetworkBindException(string bindAddress, int port, SocketException innerException)
        : base(BuildMessage(bindAddress, port, innerException), innerException)
    {
        RequestedBindAddress = bindAddress;
        RequestedPort = port;
        SocketErrorCode = innerException.SocketErrorCode;
    }

    public string RequestedBindAddress { get; }
    public int RequestedPort { get; }
    public SocketError SocketErrorCode { get; }

    private static string BuildMessage(string bindAddress, int port, SocketException exception)
    {
        var friendly = exception.SocketErrorCode switch
        {
            SocketError.AddressAlreadyInUse => "The port is already in use.",
            SocketError.AddressNotAvailable => "The requested address is not available on this machine.",
            SocketError.AccessDenied => "Permission to bind this address or port was denied.",
            _ => "The listener could not be started."
        };

        return $"Unable to start RealmGate server.{Environment.NewLine}{Environment.NewLine}" +
               $"Address: {bindAddress}{Environment.NewLine}" +
               $"Port: {port}{Environment.NewLine}{Environment.NewLine}" +
               $"{friendly}{Environment.NewLine}" +
               $"Socket error: {exception.SocketErrorCode} ({exception.Message})";
    }
}

/// <summary>
/// Cross-platform IPv4 listener configuration used by the server runtime and desktop hosts.
/// UI layers only select/update settings; they do not own sockets.
/// </summary>
public static class RealmGateNetworkBinding
{
    public const string LocalOnlyAddress = "127.0.0.1";
    public const string AllIpv4InterfacesAddress = "0.0.0.0";
    public const int DefaultTelnetPort = 2323;
    public const int DefaultClientDataPort = 2324;

    public static IPAddress ResolveIpv4Address(string bindAddress)
    {
        bindAddress = (bindAddress ?? string.Empty).Trim();
        if (!IPAddress.TryParse(bindAddress, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
        {
            throw new InvalidOperationException(
                $"Invalid RealmGate bind address '{bindAddress}'. Enter an IPv4 address such as " +
                $"{LocalOnlyAddress}, a specific LAN address, or {AllIpv4InterfacesAddress}.");
        }

        return address;
    }

    public static TcpListener StartListener(string bindAddress, int port)
    {
        ValidatePort(port);
        var address = ResolveIpv4Address(bindAddress);
        var listener = new TcpListener(new IPEndPoint(address, port));
        try
        {
            listener.Start();
            return listener;
        }
        catch (SocketException exception)
        {
            listener.Stop();
            throw new RealmGateNetworkBindException(bindAddress, port, exception);
        }
    }

    public static IReadOnlyList<IPAddress> DiscoverLanIpv4Addresses()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(adapter => adapter.OperationalStatus == OperationalStatus.Up)
                .Where(adapter => adapter.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(adapter => adapter.GetIPProperties().UnicastAddresses)
                .Select(entry => entry.Address)
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .Where(address => !IPAddress.IsLoopback(address))
                .Distinct()
                .OrderBy(address => address.ToString(), StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception exception) when (exception is NetworkInformationException or SocketException)
        {
            return Array.Empty<IPAddress>();
        }
    }

    public static IReadOnlyList<string> DiscoverLanEndpoints(int port) =>
        DiscoverLanIpv4Addresses().Select(address => $"{address}:{port}").ToArray();

    public static int PairedClientDataPort(int telnetPort)
    {
        ValidatePort(telnetPort);
        return telnetPort >= 65535 ? 65534 : telnetPort + 1;
    }

    public static void ValidatePort(int port)
    {
        if (port is < 1 or > 65535)
            throw new InvalidOperationException($"Invalid RealmGate port '{port}'. Enter a port from 1 through 65535.");
    }
}
