<div align="center">

# 🎌 Phillips Anime

**A personal Windows anime streaming app powered by Google Drive**

[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11-0078D4?style=flat-square&logo=windows)](https://microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![WinUI](https://img.shields.io/badge/WinUI-3-0078D4?style=flat-square&logo=microsoft)](https://learn.microsoft.com/en-us/windows/apps/winui/winui3/)
[![LibVLC](https://img.shields.io/badge/LibVLC-3.9.6-FF8800?style=flat-square&logo=vlcmediaplayer)](https://code.videolan.org/videolan/LibVLCSharp)
[![License](https://img.shields.io/badge/License-Private-red?style=flat-square)]()

Stream your personal anime library stored on Google Drive — directly on your Windows desktop. No monthly subscriptions, no compression, no limits.

</div>

---

## ✨ Features

- 🗂️ **Google Drive Integration** — Reads your anime folder structure directly from Drive using a service account (no manual sharing of each file required)
- 🖼️ **Automatic Cover Art** — Fetches high-quality poster art from [MyAnimeList](https://jikan.moe/) via the Jikan API, with intelligent title matching and local disk caching
- 🎬 **Hardware-Accelerated Playback** — Uses VLC with **D3D11VA** GPU decode for smooth 1080p HEVC / H.264 10-bit playback
- 📝 **Styled Subtitle Support** — Full ASS/SSA subtitle rendering; embedded fonts, `\blur`, `\move`, `\clip` tags rendered correctly
- 🎧 **Multi-Track Selection** — Switch between audio tracks and subtitle tracks at runtime via dropdown menus
- 📑 **Chapter Navigation** — MKV chapter markers displayed and selectable in a dropdown
- ⏭️ **Auto Next Episode** — Linked episode chain so "Next" always knows where to go, with keyboard shortcut support
- ⌨️ **Full Keyboard Control** — All player actions available via keyboard shortcuts (see table below)
- 🔲 **Fullscreen Mode** — True OS-level fullscreen with auto-hiding controls and cursor
- 🏠 **Subfolder / Season Support** — Recursively navigates Season 1 / Season 2 / OVA folder structures
- ⚡ **Local Proxy Streaming** — A built-in HTTP proxy transparently handles byte-range requests so VLC can seek freely without downloading the whole file

---

## 🖥️ Requirements

| Requirement | Minimum |
|---|---|
| OS | Windows 10 version 1903 (build 17763) |
| Recommended OS | Windows 11 |
| Runtime | [Windows App SDK 1.x Runtime](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads) |
| Architecture | x64 (ARM64 also supported) |
| Network | Internet connection to Google Drive |

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                        Phillips Anime                        │
│                     WinUI 3 / Windows App SDK               │
├──────────────┬──────────────────────┬───────────────────────┤
│  MainPage    │     FolderPage       │     PlayerPage        │
│  Anime Grid  │  Episodes / Seasons  │  VLC Media Player     │
│  Cover Art   │  Navigation          │  Subtitle Tracks      │
└──────┬───────┴──────────┬───────────┴──────────┬────────────┘
       │                  │                       │
       ▼                  ▼                       ▼
┌─────────────┐  ┌─────────────────┐   ┌──────────────────────┐
│ GoogleDrive │  │  JikanService   │   │   LocalProxyServer   │
│   Service   │  │  (Cover Art)    │   │  (HTTP Range Proxy)  │
│  (Drive v3) │  │  + ImageCache   │   │  + Google Drive API  │
└──────┬──────┘  └─────────────────┘   └──────────┬───────────┘
       │                                            │
       └────────────── Google Drive ────────────────┘
```

### How Streaming Works

1. The **player** starts a `LocalProxyServer` on a random localhost port
2. VLC points to `http://localhost:{port}/video.mkv?id={fileId}&token={accessToken}`
3. VLC sends HTTP range requests (for seeking) to the proxy
4. The proxy **forwards the range request** upstream to Google Drive's API with the service account token
5. Google Drive responds with the requested byte range, which the proxy streams back to VLC
6. VLC decodes with **D3D11VA** (GPU) and renders to a WinUI SwapChain panel

This avoids downloading the entire file, enables instant seeking, and keeps the Google OAuth token server-side.

---

## ⌨️ Keyboard Shortcuts

| Key | Action |
|---|---|
| `Space` | Play / Pause |
| `F` | Toggle Fullscreen |
| `Escape` | Exit Fullscreen |
| `→` Right Arrow | Skip Forward 10 seconds |
| `←` Left Arrow | Skip Backward 10 seconds |
| `↑` Up Arrow | Volume Up |
| `↓` Down Arrow | Volume Down |
| `M` | Toggle Mute |
| `N` | Next Episode |

---

## 🚀 Setup

### Prerequisites

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourname/phillips-anime.git
   cd phillips-anime
   ```

2. **Install the Windows App SDK runtime** if you haven't already:
   [Download from Microsoft](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/downloads)

3. **Install NuGet packages**
   ```bash
   dotnet restore
   ```

### Google Drive Configuration

This app authenticates with Google Drive using a **service account** — no user login required.

1. Go to [Google Cloud Console](https://console.cloud.google.com/)
2. Create a new project (or use an existing one)
3. Enable the **Google Drive API**
4. Go to **IAM & Admin → Service Accounts** and create a new service account
5. Download the JSON key file
6. Rename the file to `service-account.json` and place it in the project root (next to `AnimeStreamer.csproj`)

   > ⚠️ **Important:** `service-account.json` is listed in `.gitignore`. Never commit it to version control — it provides full read access to any Drive files shared with the service account email.

7. **Share your anime folder** with the service account's email address (found in the JSON file as `client_email`) — grant **Viewer** access

8. **Update the Root Folder ID** in `Services/GoogleDriveService.cs`:
   ```csharp
   private const string RootAnimeFolderId = "YOUR_GOOGLE_DRIVE_FOLDER_ID_HERE";
   ```
   To find the folder ID: open the folder in Google Drive — it's the last part of the URL:
   `https://drive.google.com/drive/folders/`**`THIS_IS_THE_ID`**

### Build & Run

Open `AnimeStreamer.slnx` in Visual Studio 2022 (17.9+) and press **F5**, or:

```bash
dotnet build AnimeStreamer.csproj --configuration Debug
```

> The project targets `net8.0-windows10.0.19041.0` and requires Visual Studio with the **Windows App SDK workload** installed.

---

## 📁 Project Structure

```
phillips-anime/
├── App.xaml / App.xaml.cs          # Application entry point, singleton services, dark title bar
├── Views/
│   ├── MainPage.xaml(.cs)          # Anime grid browser — fetches Drive folders & cover art
│   ├── FolderPage.xaml(.cs)        # Episode list — handles seasons, subfolders, OVAs
│   └── PlayerPage.xaml(.cs)        # Full-featured VLC media player page
├── Services/
│   ├── GoogleDriveService.cs       # Google Drive API wrapper with 1-hour token caching
│   ├── LocalProxyServer.cs         # Localhost HTTP proxy for range-request video streaming
│   ├── JikanService.cs             # MyAnimeList cover art via Jikan API (rate-limited, cached)
│   ├── ImageCacheService.cs        # Disk cache for downloaded cover art images
│   └── EpisodeNameParser.cs        # Episode naming utility
├── ViewModels/
│   ├── AnimeItemViewModel.cs       # Anime folder binding model (title, cover, drive ID)
│   └── EpisodeItemViewModel.cs     # Episode binding model (title, file ID, next episode link)
├── Helpers/
│   └── HoverEffect.cs             # Pointer hover animation helper
└── service-account.json            # ⚠️ NOT committed — your Google service account key
```

---

## 🔧 Key Technical Details

### Video Playback
- **Decoder:** LibVLC 3.9.6 with `--avcodec-hw=d3d11va` for GPU-accelerated decode
- **Renderer:** WinUI 3 SwapChain panel (zero-copy D3D11 path when GPU decode is active)
- **Network buffer:** 3 seconds pre-buffer with automatic reconnect (`http-reconnect`)
- **Subtitles:** `--no-sub-autodetect-file` ensures only embedded MKV subtitle tracks are used

### Subtitle Quality
The player is tuned for **styled ASS/SSA fansub tracks** — the kind embedded in high-quality 1080p MKV rips. VLC's D3D11 render pipeline combined with no-autodetect settings preserves embedded fonts, `\blur`, `\move`, `\clip`, and `\an` positioning tags correctly.

### Cover Art Pipeline
```
Folder Name → Memory Cache → Disk Cache → Jikan API (with smart fallbacks)
```
The Jikan API lookup uses an intelligent title-cleaning pipeline that handles:
- Titles with arc subtitles: `Anime Title - Arc Name`
- Season tags: `Season 2`, `2nd Season`, `S2`, `Part 2`
- Long light novel titles (progressive word truncation)
- Quoted sub-titles in LN-style names

Covers are cached to disk permanently — the API is only hit once per unique title.

### Google Drive Token Caching
Service account OAuth tokens are valid for **exactly 1 hour**. The `GoogleDriveService` caches the token in memory with a 2-minute safety margin, so rapid episode navigation doesn't trigger repeated auth round-trips (which added 300–1000ms cold-start latency per episode).

---

## 📦 Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Microsoft.WindowsAppSDK` | 1.x | WinUI 3 framework |
| `LibVLCSharp` | 3.9.6 | VLC .NET bindings |
| `VideoLAN.LibVLC.Windows` | 3.0.23 | Native VLC libraries for Windows |
| `Google.Apis.Drive.v3` | 1.73.x | Google Drive API client |
| `Microsoft.Web.WebView2` | 1.x | WebView2 runtime |

---

## 📝 Google Drive Folder Structure

The app expects your Drive to follow this structure:

```
📁 Root Anime Folder  ← (set RootAnimeFolderId to this ID)
├── 📁 Attack on Titan
│   ├── 📁 Season 1
│   │   ├── 🎬 Episode 01.mkv
│   │   └── 🎬 Episode 02.mkv
│   ├── 📁 Season 2
│   └── 📁 OVA
├── 📁 Demon Slayer
│   ├── 🎬 Episode 01.mkv
│   └── 🎬 Episode 02.5.mkv   ← Decimal episodes supported
└── 📁 One Piece
    └── ...
```

**Supported episode naming:** any filename containing the video — the app auto-detects OVA files (by filename containing "ova") and handles decimal episode numbers like `12.5`.

**Hidden folders** (automatically skipped): `specials`, `special`, `fanart`, `fanarts`, `extras`, `extra`, `extrafanart`

---

## ⚙️ Development Notes

- The app uses `NavigationCacheMode.Required` on `MainPage` so the anime grid is not reloaded when navigating back from a folder
- `PlayerPage` removes itself from the back stack after each navigation to prevent stale player instances accumulating
- The local proxy server binds to `127.0.0.1` only — it never exposes your Drive token over the network
- All VLC events (`TimeChanged`, `ESAdded`, etc.) dispatch back to the UI thread via `DispatcherQueue.TryEnqueue`

---

<div align="center">

Made with ❤️ by **John Phillip Lacorte**

</div>