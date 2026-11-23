using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Win32;
using DiscordRPC;

namespace GuideStrap
{
    class Account
    {
        public long UserId { get; set; }
        public string Username { get; set; } = "";
        public string DisplayName { get; set; } = "";
        public string Cookie { get; set; } = "";
    }

    static class AccountStore
    {
        static string FilePath => Path.Combine(Paths.Base, "accounts.dat");

        public static List<Account> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                string json = Encoding.UTF8.GetString(Dpapi.Unprotect(File.ReadAllBytes(FilePath)));
                return JsonSerializer.Deserialize<List<Account>>(json) ?? new();
            }
            catch { return new(); }
        }

        public static void Save(List<Account> list)
        {
            Directory.CreateDirectory(Paths.Base);
            byte[] json = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(list));
            File.WriteAllBytes(FilePath, Dpapi.Protect(json));
        }
    }

    // Windows DPAPI: only your Windows user on this PC can decrypt the accounts file
    static class Dpapi
    {
        [StructLayout(LayoutKind.Sequential)]
        struct DataBlob { public int cbData; public IntPtr pbData; }

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptProtectData(ref DataBlob dataIn, string? description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("crypt32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool CryptUnprotectData(ref DataBlob dataIn, IntPtr description, IntPtr entropy, IntPtr reserved, IntPtr prompt, int flags, ref DataBlob dataOut);

        [DllImport("kernel32.dll")]
        static extern IntPtr LocalFree(IntPtr mem);

        public static byte[] Protect(byte[] data) => Run(data, true);
        public static byte[] Unprotect(byte[] data) => Run(data, false);

        static byte[] Run(byte[] input, bool protect)
        {
            var inBlob = new DataBlob { cbData = input.Length, pbData = Marshal.AllocHGlobal(Math.Max(1, input.Length)) };
            var outBlob = new DataBlob();
            try
            {
                Marshal.Copy(input, 0, inBlob.pbData, input.Length);
                bool ok = protect
                    ? CryptProtectData(ref inBlob, "GuideStrap", IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref outBlob)
                    : CryptUnprotectData(ref inBlob, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 1, ref outBlob);
                if (!ok) throw new Win32Exception(Marshal.GetLastWin32Error());
                var result = new byte[outBlob.cbData];
                Marshal.Copy(outBlob.pbData, result, 0, outBlob.cbData);
                return result;
            }
            finally
            {
                Marshal.FreeHGlobal(inBlob.pbData);
                if (outBlob.pbData != IntPtr.Zero) LocalFree(outBlob.pbData);
            }
        }
    }
}
