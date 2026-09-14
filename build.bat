@echo off
echo Building SOHDiscordPresence.exe...
"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" /nologo /target:winexe /win32icon:app.ico /r:System.Windows.Forms.dll /r:System.Drawing.dll /out:SOHDiscordPresence.exe Program.cs
if %errorlevel% equ 0 (
    echo Build succeeded: SOHDiscordPresence.exe generated successfully.
) else (
    echo Build failed: Error during compilation.
)
