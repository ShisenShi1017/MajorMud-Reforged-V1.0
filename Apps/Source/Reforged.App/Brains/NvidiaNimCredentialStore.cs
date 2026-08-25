using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Reforged.App.Brains;

public enum NvidiaNimKeySlot { Shared, Lightning, Ultra }

public interface INvidiaNimCredentialStore
{
    Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default);
    Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);
    Task<string?> ReadApiKeyAsync(CancellationToken cancellationToken = default);
    Task ClearApiKeyAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Stores the NVIDIA NIM API key in Windows Credential Manager. Callers can query only
/// whether a key exists; the UI must never bind the result of ReadApiKeyAsync.
/// </summary>
public sealed class WindowsNvidiaNimCredentialStore : INvidiaNimCredentialStore
{
    private readonly string _targetName;
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 5 * 512;

    public WindowsNvidiaNimCredentialStore(NvidiaNimKeySlot slot = NvidiaNimKeySlot.Shared)
    {
        Slot = slot;
        _targetName = slot == NvidiaNimKeySlot.Ultra
            ? "MajorMudReforged/Borin/NvidiaNim/Ultra"
            : "MajorMudReforged/Borin/NvidiaNim"; // Preserve the existing Lightning credential.
    }

    public NvidiaNimKeySlot Slot { get; }
    public string CredentialTargetName => _targetName;

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (CredRead(_targetName, CredentialTypeGeneric, 0, out var pointer))
        {
            CredFree(pointer);
            return Task.FromResult(true);
        }

        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return Task.FromResult(false);
        throw new Win32Exception(error, "Windows Credential Manager could not check the NVIDIA NIM credential.");
    }

    public Task SaveApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new ArgumentException("An NVIDIA NIM API key is required.", nameof(apiKey));

        var bytes = Encoding.Unicode.GetBytes(apiKey.Trim());
        if (bytes.Length > MaximumCredentialBlobBytes)
            throw new ArgumentException("The NVIDIA NIM API key is too long for Windows Credential Manager.", nameof(apiKey));

        var blob = Marshal.AllocCoTaskMem(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, blob, bytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = _targetName,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = "Borin",
                Comment = "MajorMUD Reforged Borin NVIDIA NIM API key"
            };
            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows Credential Manager could not save the NVIDIA NIM credential.");
        }
        finally
        {
            Array.Clear(bytes, 0, bytes.Length);
            Marshal.FreeCoTaskMem(blob);
        }

        return Task.CompletedTask;
    }

    public Task<string?> ReadApiKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (!CredRead(_targetName, CredentialTypeGeneric, 0, out var pointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return Task.FromResult<string?>(null);
            throw new Win32Exception(error, "Windows Credential Manager could not read the NVIDIA NIM credential.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                return Task.FromResult<string?>(null);
            var bytes = new byte[checked((int)credential.CredentialBlobSize)];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            try
            {
                return Task.FromResult<string?>(Encoding.Unicode.GetString(bytes));
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

    public Task ClearApiKeyAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureWindows();
        if (CredDelete(_targetName, CredentialTypeGeneric, 0))
            return Task.CompletedTask;
        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(error, "Windows Credential Manager could not clear the NVIDIA NIM credential.");
        return Task.CompletedTask;
    }

    private static void EnsureWindows()
    {
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("NVIDIA NIM credential storage requires Windows Credential Manager.");
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

public static class NvidiaNimCredentialMigration
{
    /// <summary>Copies a legacy tier credential into the shared slot only when the shared slot is empty.</summary>
    public static async Task<bool> EnsureSharedCredentialAsync(
        INvidiaNimCredentialStore shared,
        INvidiaNimCredentialStore legacyUltra,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(shared);
        ArgumentNullException.ThrowIfNull(legacyUltra);
        if (await shared.IsConfiguredAsync(cancellationToken).ConfigureAwait(false))
            return false;

        var legacyKey = await legacyUltra.ReadApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(legacyKey))
            return false;

        await shared.SaveApiKeyAsync(legacyKey, cancellationToken).ConfigureAwait(false);
        return true;
    }
}
