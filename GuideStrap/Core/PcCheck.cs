using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace GuideStrap
{
    class PcInfo
    {
        public string Gpu { get; init; } = "";
        public double VramGb { get; init; }      // 0 = unknown
        public bool DedicatedGpu { get; init; }
        public double RamGb { get; init; }
        public int Threads { get; init; }
        public int RefreshHz { get; init; }      // 0 = unknown
    }

    // Looks at the PC (graphics card, memory, processor, screen) and suggests graphics settings for it.
    // Reads only what Windows already shows to every program: nothing is sent anywhere.
    static class PcCheck
    {
        const string GpuClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

        public static PcInfo Read()
        {
            var (gpu, vram) = ReadGpu();
            double ram = 0;
            try { ram = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0; } catch { }
            return new PcInfo
            {
                Gpu = gpu,
                VramGb = vram,
                DedicatedGpu = IsDedicated(gpu, vram),
                RamGb = ram,
                Threads = Environment.ProcessorCount,
                RefreshHz = ReadRefreshRate(),
            };
        }

        // Returns (graphics preset: 0, 1, 2, 3 or 5 = Smooth, FPS cap). Never suggests Max Graphics: smooth beats pretty for a default.
        public static (int Preset, int FpsCap) Recommend(PcInfo pc)
        {
            int preset;
            if (pc.RamGb > 0 && pc.RamGb < 6) preset = 0;                       // very little memory
            else if (!pc.DedicatedGpu) preset = pc.RamGb >= 8 ? 1 : 0;           // built-in graphics (most laptops)
            else if (pc.VramGb > 0 && pc.VramGb < 3) preset = 1;                 // older or entry graphics card
            else if (pc.VramGb > 0 && pc.VramGb < 4) preset = 5;                 // mid graphics card: Smooth
            else if (pc.VramGb >= 8 && pc.RamGb >= 16 && pc.Threads >= 8) preset = 3;
            else preset = 2;

            // matching the screen's refresh rate keeps it smooth without wasting power or heat
            int cap = pc.RefreshHz is >= 30 and <= 500 ? pc.RefreshHz : 0;
            return (preset, cap);
        }

        static bool IsDedicated(string gpu, double vram)
        {
            string g = gpu.ToLowerInvariant();
            if (g.Length == 0 || g.Contains("basic display") || g.Contains("basic render")) return false;
            if (g.Contains("nvidia") || g.Contains("geforce") || g.Contains("quadro") || g.Contains("rtx") ||
                g.Contains("radeon rx") || g.Contains("radeon pro") || g.Contains("intel(r) arc") || g.Contains("intel arc"))
                return true;
            // Intel UHD/Iris and AMD "Radeon(TM) Graphics"/Vega are built into the processor
            if (g.Contains("intel") || g.Contains("radeon(tm) graphics") || g.Contains("vega")) return false;
            return vram >= 2;
        }

        static (string Name, double VramGb) ReadGpu()
        {
            string best = "";
            double bestVram = -1;
            try
            {
                using var root = Registry.LocalMachine.OpenSubKey(GpuClass);
                if (root == null) return ("", 0);
                foreach (var sub in root.GetSubKeyNames())
                {
                    if (sub.Length != 4 || !sub.All(char.IsDigit)) continue;   // 0000, 0001...
                    try
                    {
                        using var key = root.OpenSubKey(sub);
                        string? name = key?.GetValue("DriverDesc") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (name.Contains("Basic Render", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Remote", StringComparison.OrdinalIgnoreCase) ||
                            name.Contains("Virtual", StringComparison.OrdinalIgnoreCase)) continue;
                        double vram = Bytes(key!.GetValue("HardwareInformation.qwMemorySize"))
                                      ?? Bytes(key.GetValue("HardwareInformation.MemorySize")) ?? 0;
                        vram /= 1073741824.0;
                        if (vram > bestVram) { best = name.Trim(); bestVram = vram; }
                    }
                    catch { }
                }
            }
            catch { }
            return (best, Math.Max(0, bestVram));
        }

        static double? Bytes(object? value) => value switch
        {
            long l => l,
            int i => (uint)i,
            byte[] b when b.Length >= 8 => BitConverter.ToInt64(b, 0),
            byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b, 0),
            _ => null,
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
            public int dmFields;
            public int dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
            public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
            public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool EnumDisplaySettings(string? deviceName, int modeNum, ref DEVMODE devMode);

        // The main screen's refresh rate (60, 144, 240...)
        static int ReadRefreshRate()
        {
            try
            {
                var mode = new DEVMODE { dmDeviceName = "", dmFormName = "" };
                mode.dmSize = (short)Marshal.SizeOf<DEVMODE>();
                return EnumDisplaySettings(null, -1, ref mode) && mode.dmDisplayFrequency > 1 ? mode.dmDisplayFrequency : 0;
            }
            catch { return 0; }
        }
    }
}
