# MuteDiscord

A lightweight background tool that fixes a specific audio bug with **SteelSeries GG** and **Discord screen sharing**.

---

## The Bug

When you share your screen on Discord while using **SteelSeries GG**, Discord captures two unintended audio sources through your stream:

- 🎙️ Your **microphone** (via the SteelSeries Sonar virtual device)
- 🎧 Your **headset audio** — including Discord itself, causing an echo/double audio effect

This happens because SteelSeries GG exposes virtual audio devices that Discord picks up the moment you go live, creating new audio sessions in the Windows Volume Mixer.

---

## The Fix

MuteDiscord runs silently in the background and watches those specific devices. The instant Discord opens an audio session on them (i.e. when you go live), it mutes it automatically in the Volume Mixer — within **500ms**.

No more mic bleed. No more double Discord audio. No manual intervention needed.

---

## Getting Started

### 1. Download
Grab `MuteDiscord.exe` and `Installer.exe` and place them in the same folder.

### 2. Run the Installer
Double-click `Installer.exe`. It will show you a list of your active audio devices.

You need to select **two devices**:
- **SteelSeries Sonar - Microphone** *(pre-selected by default)*
- **Your headset** — the one you use to hear audio (e.g. `Cuffie (3- Arctis Nova 7)`)

The installer will then ask if you want it to launch automatically with Windows.

### 3. Done
MuteDiscord runs in the background from now on. Start a stream — the problem devices get muted automatically.

---

## Configuration

Devices are stored in `config.json` next to the exe:

```json
{
  "devices": [
    "SteelSeries Sonar - Microphone (SteelSeries Sonar Virtual Audio Device)",
    "Cuffie (3- Arctis Nova 7)"
  ]
}
```

Edit this file anytime to add or remove devices. No reinstall needed.

---

## How It Works

- Polls your configured audio devices every **500ms**
- Detects new Discord audio sessions the moment they're created
- Mutes them instantly via the Windows Core Audio API
- No third-party dependencies — just a native Windows exe

**Resource usage:** near-zero CPU, ~10MB RAM.

---

## Requirements

- Windows 10 / 11
- .NET Framework 4.x *(pre-installed on all modern Windows)*
- Discord desktop app + SteelSeries GG

---

## Notes

- This only mutes Discord in the **Windows Volume Mixer** — it does not touch your mic or Discord's own mute button
- To disable startup launch, run `Installer.exe` again and toggle the option off
