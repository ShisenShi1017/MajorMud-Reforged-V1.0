using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace RealmGate.Client.Security;

public sealed record SecureCredential(string UserName, string Secret);

public interface ISecureCredentialStore
{
    string BackendName { get; }

    bool IsAvailable { get; }

    Task<bool> ContainsAsync(string target, CancellationToken cancellationToken = default);

    Task SaveAsync(
        string target,
        SecureCredential credential,
        CancellationToken cancellationToken = default);

    Task<SecureCredential?> ReadAsync(
        string target,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string target, CancellationToken cancellationToken = default);
}

public static class PlatformSecureCredentialStore
{
    public static ISecureCredentialStore CreateForCurrentPlatform()
    {
        if (OperatingSystem.IsWindows())
            return new WindowsCredentialManagerStore();
        if (OperatingSystem.IsLinux())
        {
            var executable = LinuxSecretServiceCredentialStore.FindSecretTool();
            return executable is null
                ? new UnavailableSecureCredentialStore(
                    "Linux Secret Service",
                    "Linux secure credential storage requires Secret Service and the secret-tool command.")
                : new LinuxSecretServiceCredentialStore(executable);
        }

        return new UnavailableSecureCredentialStore(
            "Unsupported platform",
            "RealmGate secure credential storage currently supports Windows and Linux.");
    }
}

public sealed class WindowsCredentialManagerStore : ISecureCredentialStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 5 * 512;

    public string BackendName => "Windows Credential Manager";

    public bool IsAvailable => OperatingSystem.IsWindows();

    public Task<bool> ContainsAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        target = NormalizeTarget(target);
        if (CredRead(target, CredentialTypeGeneric, 0, out var pointer))
        {
            CredFree(pointer);
            return Task.FromResult(true);
        }

        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return Task.FromResult(false);
        throw new Win32Exception(error, "Windows Credential Manager could not check the credential.");
    }

    public Task SaveAsync(
        string target,
        SecureCredential credential,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(credential);
        target = NormalizeTarget(target);
        if (string.IsNullOrEmpty(credential.Secret))
            throw new ArgumentException("A non-empty credential secret is required.", nameof(credential));

        var bytes = Encoding.Unicode.GetBytes(credential.Secret);
        if (bytes.Length > MaximumCredentialBlobBytes)
            throw new ArgumentException("The credential is too long for Windows Credential Manager.", nameof(credential));

        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var native = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = string.IsNullOrWhiteSpace(credential.UserName)
                    ? "RealmGate"
                    : credential.UserName.Trim(),
                Comment = "RealmGate secure credential"
            };
            if (!CredWrite(ref native, 0))
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(),
                    "Windows Credential Manager could not save the credential.");
        }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
            Marshal.FreeCoTaskMem(blob);
        }

        return Task.CompletedTask;
    }

    public Task<SecureCredential?> ReadAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        target = NormalizeTarget(target);
        if (!CredRead(target, CredentialTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return Task.FromResult<SecureCredential?>(null);
            throw new Win32Exception(error, "Windows Credential Manager could not read the credential.");
        }

        try
        {
            var native = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (native.CredentialBlob == IntPtr.Zero || native.CredentialBlobSize == 0)
                return Task.FromResult<SecureCredential?>(null);

            var bytes = new byte[checked((int)native.CredentialBlobSize)];
            Marshal.Copy(native.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                return Task.FromResult<SecureCredential?>(new SecureCredential(
                    string.IsNullOrWhiteSpace(native.UserName) ? "RealmGate" : native.UserName,
                    Encoding.Unicode.GetString(bytes)));
            }
            finally
            {
                Array.Clear(bytes, 0, bytes.Length);
            }
        }
        finally
        {
            CredFree(pointer);
        }
    }

    public Task DeleteAsync(string target, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureAvailable();
        target = NormalizeTarget(target);
        if (CredDelete(target, CredentialTypeGeneric, 0))
            return Task.CompletedTask;

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(error, "Windows Credential Manager could not clear the credential.");
        return Task.CompletedTask;
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
            throw new PlatformNotSupportedException("Windows Credential Manager is available only on Windows.");
    }

    internal static string NormalizeTarget(string target)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        var normalized = target.Trim();
        if (normalized.Length > 256 || normalized.Any(char.IsControl))
            throw new ArgumentException("Credential target names must be 1-256 printable characters.", nameof(target));
        return normalized;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite([In] ref NativeCredential credential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = false)]
    private static extern void CredFree([In] IntPtr buffer);
}

public sealed class LinuxSecretServiceCredentialStore(string secretToolPath) : ISecureCredentialStore
{
    private const int MaximumSecretLength = 16384;
    private readonly string _secretToolPath = Path.GetFullPath(secretToolPath);

    public string BackendName => "Linux Secret Service";

    public bool IsAvailable => OperatingSystem.IsLinux() && File.Exists(_secretToolPath);

    public async Task<bool> ContainsAsync(string target, CancellationToken cancellationToken = default) =>
        await ReadAsync(target, cancellationToken).ConfigureAwait(false) is not null;

    public async Task SaveAsync(
        string target,
        SecureCredential credential,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        ArgumentNullException.ThrowIfNull(credential);
        target = WindowsCredentialManagerStore.NormalizeTarget(target);
        if (string.IsNullOrEmpty(credential.Secret))
            throw new ArgumentException("A non-empty credential secret is required.", nameof(credential));

        var payload = JsonSerializer.Serialize(credential);
        if (payload.Length > MaximumSecretLength)
            throw new ArgumentException("The credential is too long for Linux Secret Service.", nameof(credential));

        var result = await RunAsync(
            ["store", "--label=RealmGate credential", "service", "RealmGate", "target", target],
            payload,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Linux Secret Service could not save the credential.");
    }

    public async Task<SecureCredential?> ReadAsync(
        string target,
        CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        target = WindowsCredentialManagerStore.NormalizeTarget(target);
        var result = await RunAsync(
            ["lookup", "service", "RealmGate", "target", target],
            null,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode == 1 || string.IsNullOrWhiteSpace(result.StandardOutput))
            return null;
        if (result.ExitCode != 0)
            throw new InvalidOperationException("Linux Secret Service could not read the credential.");

        try
        {
            return JsonSerializer.Deserialize<SecureCredential>(result.StandardOutput.Trim());
        }
        catch (JsonException)
        {
            throw new InvalidDataException("Linux Secret Service returned an invalid RealmGate credential payload.");
        }
    }

    public async Task DeleteAsync(string target, CancellationToken cancellationToken = default)
    {
        EnsureAvailable();
        target = WindowsCredentialManagerStore.NormalizeTarget(target);
        var result = await RunAsync(
            ["clear", "service", "RealmGate", "target", target],
            null,
            cancellationToken).ConfigureAwait(false);
        if (result.ExitCode is not (0 or 1))
            throw new InvalidOperationException("Linux Secret Service could not clear the credential.");
    }

    internal static string? FindSecretTool()
    {
        if (!OperatingSystem.IsLinux())
            return null;
        var path = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var folder in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(folder, "secret-tool");
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    private void EnsureAvailable()
    {
        if (!IsAvailable)
        {
            throw new PlatformNotSupportedException(
                "Linux secure credential storage requires Secret Service and the secret-tool command.");
        }
    }

    private async Task<ProcessResult> RunAsync(
        IReadOnlyList<string> arguments,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _secretToolPath,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var argument in arguments)
            startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
                            ?? throw new InvalidOperationException("Linux Secret Service could not start secret-tool.");
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken)
                .ConfigureAwait(false);
            process.StandardInput.Close();
        }

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        _ = await errorTask.ConfigureAwait(false);
        return new ProcessResult(process.ExitCode, await outputTask.ConfigureAwait(false));
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput);
}

public sealed class UnavailableSecureCredentialStore(
    string backendName,
    string unavailableReason) : ISecureCredentialStore
{
    public string BackendName { get; } = backendName;

    public bool IsAvailable => false;

    public Task<bool> ContainsAsync(string target, CancellationToken cancellationToken = default) =>
        Task.FromException<bool>(CreateException(cancellationToken));

    public Task SaveAsync(
        string target,
        SecureCredential credential,
        CancellationToken cancellationToken = default) =>
        Task.FromException(CreateException(cancellationToken));

    public Task<SecureCredential?> ReadAsync(
        string target,
        CancellationToken cancellationToken = default) =>
        Task.FromException<SecureCredential?>(CreateException(cancellationToken));

    public Task DeleteAsync(string target, CancellationToken cancellationToken = default) =>
        Task.FromException(CreateException(cancellationToken));

    private Exception CreateException(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new PlatformNotSupportedException(unavailableReason);
    }
}
