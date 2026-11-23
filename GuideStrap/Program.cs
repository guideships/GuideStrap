namespace GuideStrap
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        ///  Opens the small launcher window first. If Roblox's website started us
        ///  (roblox-player: link), the launcher launches straight away.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            string? launchUri = args.FirstOrDefault(a => a.StartsWith("roblox", StringComparison.OrdinalIgnoreCase));
            Application.Run(new GuideStrapContext(launchUri));
        }
    }
}
