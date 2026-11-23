namespace GuideStrap
{
    internal static class Program
    {
        /// <summary>
        ///  Entry point.
        ///  --uninstall         : remove GuideStrap (used by Windows' Installed apps list)
        ///  already installed   : hand off to the installed copy (no setup screen)
        ///  brand-new user      : setup screen (handled inside GuideStrapContext)
        ///  otherwise           : launcher, or straight to Roblox for roblox:// links
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            if (args.Contains("--uninstall"))
            {
                Setup.Uninstall();
                return;
            }
            if (Setup.HandOffIfInstalled(args)) return;
            Application.Run(new GuideStrapContext(args));
        }
    }
}
