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
    // FASTFLAGS (Roblox's allowlist only)
    static class FF
    {
        public const string Quality = "DFIntDebugFRMQualityLevelOverride";
        public const string TexEnabled = "DFFlagTextureQualityOverrideEnabled";
        public const string Texture = "DFIntTextureQualityOverride";
        public const string Msaa = "FIntDebugForceMSAASamples";
        public const string D3D11 = "FFlagDebugGraphicsPreferD3D11";
        public const string Vulkan = "FFlagDebugGraphicsPreferVulkan";
        public const string OpenGL = "FFlagDebugGraphicsPreferOpenGL";
        public const string GrassMin = "FIntFRMMinGrassDistance";
        public const string GrassMax = "FIntFRMMaxGrassDistance";
        public const string GraySky = "FFlagDebugSkyGray";
        public const string PauseVoxelizer = "DFFlagDebugPauseVoxelizer";
        public const string GrassMotion = "FIntGrassMovementReducedMotionFactor";
        public const string DisableDpi = "DFFlagDisableDPIScale";
        public const string AltEnter = "FFlagHandleAltEnterFullscreenManually";
        public static readonly string[] Lod =
        {
            "DFIntCSGLevelOfDetailSwitchingDistance",
            "DFIntCSGLevelOfDetailSwitchingDistanceL12",
            "DFIntCSGLevelOfDetailSwitchingDistanceL23",
            "DFIntCSGLevelOfDetailSwitchingDistanceL34",
        };

        // Roblox's official allowlist. If Roblox adds more later, add them here.
        public static readonly (string Name, string Desc)[] Allowed =
        {
            (Quality, "Graphics quality level, 1 (lowest) to 21 (highest)"),
            (TexEnabled, "True = use the texture quality flag"),
            (Texture, "Texture quality: 0 lowest, 1 low, 2 medium, 3 high"),
            (Msaa, "Anti-aliasing samples: 1, 2, 4 or 8"),
            (D3D11, "True = render with Direct3D 11"),
            (Vulkan, "True = render with Vulkan"),
            (OpenGL, "True = render with OpenGL"),
            (GrassMin, "Grass draw distance (min). 0 with max = no grass"),
            (GrassMax, "Grass draw distance (max). 0 = no grass"),
            (GraySky, "True = gray sky instead of the skybox"),
            (PauseVoxelizer, "True = freeze lighting updates (faster)"),
            (GrassMotion, "Grass movement. 0 = still grass"),
            (DisableDpi, "True = turn off DPI scaling"),
            (AltEnter, "False = Alt+Enter uses exclusive fullscreen"),
            (Lod[0], "Mesh detail switching distance"),
            (Lod[1], "Mesh detail distance, level 1 to 2"),
            (Lod[2], "Mesh detail distance, level 2 to 3"),
            (Lod[3], "Mesh detail distance, level 3 to 4"),
        };

        public static bool IsAllowed(string name) => Allowed.Any(a => a.Name == name);
        public static string Describe(string? name) => L.T(Allowed.FirstOrDefault(a => a.Name == name).Desc ?? "");
    }
}
