using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace Reforged.App.Brains;

public interface IBorinSecureCredentialStore
{
    Task<bool> HasPasswordAsync(string sessionId, CancellationToken token = default);
    Task SavePasswordAsync(string sessionId, string loginName, string password, CancellationToken token = default);
    Task<string?> ReadPasswordAsync(string sessionId, CancellationToken token = default);
    Task ClearPasswordAsync(string sessionId, CancellationToken token = default);
}

/// <summary>
/// Stores Borin's external-server password in Windows Credential Manager.
/// The password never enters repository files, Borin memory, journals, logs, or model prompts.
/// </summary>
public sealed class WindowsBorinSecureCredentialStore : IBorinSecureCredentialStore
{
    private const uint CredentialTypeGeneric = 1;
    private const uint CredentialPersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobBytes = 5 * 512;

    public Task<bool> HasPasswordAsync(string sessionId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult(false);

        var target = GetTargetName(sessionId);
        if (CredRead(target, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            CredFree(credentialPointer);
            return Task.FromResult(true);
        }

        var error = Marshal.GetLastWin32Error();
        if (error == ErrorNotFound)
            return Task.FromResult(false);
        throw new Win32Exception(error, "Windows Credential Manager could not check Borin's saved password.");
    }

    public Task SavePasswordAsync(string sessionId, string loginName, string password, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Borin's secure password store requires Windows Credential Manager.");
        if (string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Enter Borin's password before saving it securely.");

        var passwordBytes = Encoding.Unicode.GetBytes(password);
        if (passwordBytes.Length > MaximumCredentialBlobBytes)
            throw new InvalidOperationException("The password is too long for Windows Credential Manager.");

        var blob = Marshal.AllocCoTaskMem(passwordBytes.Length);
        try
        {
            Marshal.Copy(passwordBytes, 0, blob, passwordBytes.Length);
            var credential = new NativeCredential
            {
                Type = CredentialTypeGeneric,
                TargetName = GetTargetName(sessionId),
                CredentialBlobSize = (uint)passwordBytes.Length,
                CredentialBlob = blob,
                Persist = CredentialPersistLocalMachine,
                UserName = string.IsNullOrWhiteSpace(loginName) ? "borin" : loginName.Trim(),
                Comment = "MajorMUD Reforged Borin external player login",
            };

            if (!CredWrite(ref credential, 0))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows Credential Manager could not save Borin's password.");
        }
        finally
        {
            if (passwordBytes.Length > 0)
                Array.Clear(passwordBytes, 0, passwordBytes.Length);
            Marshal.FreeCoTaskMem(blob);
        }

        return Task.CompletedTask;
    }

    public Task<string?> ReadPasswordAsync(string sessionId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.FromResult<string?>(null);

        var target = GetTargetName(sessionId);
        if (!CredRead(target, CredentialTypeGeneric, 0, out var credentialPointer))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == ErrorNotFound)
                return Task.FromResult<string?>(null);
            throw new Win32Exception(error, "Windows Credential Manager could not read Borin's saved password.");
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
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
            CredFree(credentialPointer);
        }
    }

    public Task ClearPasswordAsync(string sessionId, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsWindows())
            return Task.CompletedTask;

        if (CredDelete(GetTargetName(sessionId), CredentialTypeGeneric, 0))
            return Task.CompletedTask;

        var error = Marshal.GetLastWin32Error();
        if (error != ErrorNotFound)
            throw new Win32Exception(error, "Windows Credential Manager could not clear Borin's saved password.");
        return Task.CompletedTask;
    }

    private static string GetTargetName(string sessionId)
    {
        var safe = new string((sessionId ?? string.Empty)
            .Where(character => char.IsLetterOrDigit(character) || character is '.' or '_' or '-')
            .Take(96)
            .ToArray());
        if (safe.Length == 0)
            safe = "borin-training-01";
        return $"MajorMudReforged/BorinPlayer/{safe}";
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
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("Advapi32.dll", SetLastError = false)]
    private static extern void CredFree([In] IntPtr buffer);
}
