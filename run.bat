@echo off
rem Build the C# code and launch the game.
cd /d "%~dp0"
"C:\Program Files\dotnet\dotnet.exe" build -v q || pause
start "" "%LOCALAPPDATA%\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path . %*
