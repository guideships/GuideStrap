<p align="center">
  <img src="docs/logo.png" width="120" alt="GuideStrap logo">
</p>

<h1 align="center">GuideStrap</h1>

<p align="center">
  A Roblox launcher for Windows with graphics presets, FastFlags, mods, account switching and Discord status.
  <br><br>
  <a href="https://github.com/guideships/GuideStrap/releases/latest"><b>Download the latest version</b></a>
  ·
  <a href="https://guideships.github.io/GuideStrap/">Website</a>
</p>

---

## What it does

GuideStrap replaces Roblox's normal launcher. It downloads Roblox straight from Roblox's own servers, keeps it updated, and starts it with your settings applied.

**Graphics**
- A slider from *Max FPS* to *Max Graphics* for people who just want it to run well
- Every setting on its own if you want control: quality level, textures, anti-aliasing, rendering API, grass, sky and more

**FastFlags**
- An editor for flags on Roblox's official allowlist (anything else is skipped, since Roblox ignores it)
- Paste a whole JSON list at once; if a flag is already set, you choose whether to overwrite or keep it

**Mods**
- Custom death sound (`.ogg`)
- Mouse cursors: built-in GuideStrap arrow, a small dot for aiming, or your own image
- Custom sky packs
- A custom font for all in-game text
- A mods folder for anything else

**Accounts**
- Log in once with the real Roblox login page, then switch accounts in one click
- Join a game as any saved account by pasting its link

**While you play**
- Discord status showing the game you're in, with its icon and a link to it
- Server location when you join ("Connected to a server in Dallas, Texas, US")
- Recent games on the Play page for quick rejoining
- A tray menu to relaunch, switch accounts or turn the Discord status off

**Other**
- Settings profiles you can export and share
- Cache cleaner and a one-click Roblox repair
- Accent colors
- Updates itself when a new release comes out

## Is it safe?

- GuideStrap never touches the game while it's running. It only changes files and settings that Roblox reads on its own when it starts, the same way Bloxstrap and Fishstrap work. It isn't an exploit.
- **Your normal Roblox install is never changed.** Everything happens in GuideStrap's own copy of Roblox, so opening Roblox without GuideStrap always gives you the original game. You can also hand the website's Play buttons back to normal Roblox from Settings.
- Saved logins are encrypted with Windows and only work for your Windows user on your PC. Never share your GuideStrap folder.
- The code is all here, so anyone can check what it does.

## Install

1. Download `GuideStrap.exe` from [Releases](https://github.com/guideships/GuideStrap/releases/latest).
2. Run it. Windows may warn you because the app isn't signed yet; click **More info → Run anyway**.
3. Press **Launch Roblox**. The first launch downloads Roblox, which takes a minute.

Optional: in **Settings**, turn on **Open Roblox links with GuideStrap** so Play buttons on roblox.com use it too.

## Building it yourself

You need Visual Studio 2026 (or the .NET 10 SDK) on Windows.

```
git clone https://github.com/guideships/GuideStrap.git
cd GuideStrap
dotnet build
```

To make a single `.exe`: right-click the project in Visual Studio → **Publish → Folder**, then set *Self-contained*, *win-x64* and *Produce single file*.

## Credits

Made by **[guideships](https://github.com/guideships)**. Inspired by [Bloxstrap](https://github.com/bloxstraplabs/bloxstrap) and [Fishstrap](https://github.com/fishstrap/fishstrap).

GuideStrap is a fan project and isn't affiliated with or endorsed by Roblox Corporation. Released under the [MIT License](LICENSE.txt).
