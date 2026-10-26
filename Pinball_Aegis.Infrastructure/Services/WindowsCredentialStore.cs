using System.Runtime.InteropServices;
using System.Text;
using Pinball_Aegis.Application.Interfaces;

namespace Pinball_Aegis.Infrastructure.Services;

// Interacts directly with the native Windows Credential Manager (advapi32.dll)
public class WindowsCredentialStore : ICredentialStore
{
    private const int CredTypeGeneric = 1;
    private const int CredPersistLocalMachine = 2;

    public void Save(string targetName, string username, string secret)
    {
        var secretBytes = Encoding.Unicode.GetBytes(secret);
        var blobPtr = Marshal.AllocHGlobal(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, blobPtr, secretBytes.Length);

            var cred = new NativeCredential
            {
                Type = CredTypeGeneric,
                TargetName = targetName,
                UserName = username,
                CredentialBlob = blobPtr,
                CredentialBlobSize = (uint)secretBytes.Length,
                Persist = CredPersistLocalMachine
            };

            CredWrite(ref cred, 0);
        }
        finally
        {
            Marshal.FreeHGlobal(blobPtr);
        }
    }

    public (string? Username, string? Secret) Get(string targetName)
    {
        if (!CredRead(targetName, CredTypeGeneric, 0, out var ptr))
            return (null, null);

        try
        {
            var cred = Marshal.PtrToStructure<NativeCredential>(ptr);
            string? secret = null;

            if (cred.CredentialBlob != IntPtr.Zero && cred.CredentialBlobSize > 0)
            {
                var secretBytes = new byte[cred.CredentialBlobSize];
                Marshal.Copy(cred.CredentialBlob, secretBytes, 0, (int)cred.CredentialBlobSize);
                secret = Encoding.Unicode.GetString(secretBytes);
            }

            return (cred.UserName, secret);
        }
        finally
        {
            CredFree(ptr);
        }
    }

    public void Delete(string targetName)
    {
        CredDelete(targetName, CredTypeGeneric, 0);
    }

    #region Windows Native API

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite([In] ref NativeCredential userCredential, [In] uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int reservedFlag, out IntPtr credentialPtr);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll", EntryPoint = "CredFree", SetLastError = true)]
    private static extern void CredFree([In] IntPtr cred);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    #endregion
}