@echo off
::打包 Release 单文件 exe
::Costura 会把所有依赖 dll 和更新程序一起塞进 lol_speaker.exe，产出就一个文件
cd /d "%~dp0"

dotnet build LolSpeaker.csproj --configuration Release

echo.
echo 产物: %~dp0bin\Release\net472\lol_speaker.exe
echo.
pause
