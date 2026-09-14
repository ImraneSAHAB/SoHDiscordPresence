# SoH Discord Presence

A lightweight, standalone Discord Rich Presence integration for **Ship of Harkinian** (*The Legend of Zelda: Ocarina of Time* PC Port).

![Ocarina of Time](Zelda%20OOT.png)

---

## 🌟 Features

- **Game Over Detection**:
  Displays `💀 Game Over (0/X)` as soon as Link runs out of health.
- **Age State Detection**:
  Dynamically shows whether you are playing as **Adult Link**, **Child Link**, or navigating the **Main Menu / Title Screen**.
- **System Tray Integration**:
  Runs silently in the Windows system tray with a context menu to exit cleanly.
- **Fully Configurable**:
- **Zero Heavy Dependencies**:
  Built natively in C# for Windows using Discord's Native IPC pipe interface without external package dependencies.

---

## 📸 Discord Rich Presence Preview

| State | Presence Example |
| :--- | :--- |
| **In-Game (Adult)** | **Details:** Playing as Adult Link<br>**State:** ❤️ 14.5/20 |
| **Low Health (Child)** | **Details:** Playing as Child Link<br>**State:** ❤️ 0.25/3 |
| **Game Over** | **Details:** Playing as Adult Link<br>**State:** 💀 Game Over (0/20) |
| **Main Menu** | **Details:** Main Menu<br>**State:** Title Screen |

---

## 🚀 Getting Started

### Prerequisites
- **Windows OS**
- **Ship of Harkinian** (`soh.exe`)
- **Discord Desktop App** running

### Quick Start
1. Download `SOHDiscordPresence.exe`.
2. Place `config.json` and `app.ico` in the same directory as `SOHDiscordPresence.exe`.
3. Launch `SOHDiscordPresence.exe`.
4. Start **Ship of Harkinian** and enjoy live Discord Rich Presence status!

---

## 🛠️ Building from Source

To compile the application manually on Windows using the included `build.bat`:

```cmd
build.bat
```

Or execute the C# compiler directly:

```cmd
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:SOHDiscordPresence.exe Program.cs
```

---

## 📄 License

Created by **Imashiro**. Open-source project for the Ship of Harkinian community.
