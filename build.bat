@echo off
rem ── batIDE 命令行构建脚本 ──────────────────────────────
rem 产物：release\ 目录，绿色免安装，直接可运行
rem   release\batIDE.exe       编辑器
rem   release\batlint.exe     命令行检查器
rem   release\BatLint.Core.dll / Scintilla.NET.dll / x86\*.dll
rem 需要：.NET SDK（dotnet build）+ NuGet 可联网还原 Scintilla.NET 5.3.2.9
rem ──────────────────────────────────────────────────────
setlocal
where dotnet >nul 2>nul || (echo [错误] 未找到 dotnet，请安装 .NET SDK & exit /b 1)

echo [1/3] 还原 + 构建（Release）……
dotnet build "%~dp0src\BatLint.Cli\BatLint.Cli.csproj" -c Release --nologo || (echo [错误] CLI 构建失败 & exit /b 1)
dotnet build "%~dp0src\batIDE48\batIDE48.csproj" -c Release --nologo || (echo [错误] 编辑器构建失败 & exit /b 1)

set OUT=%~dp0release
if exist "%OUT%" rd /s /q "%OUT%"
mkdir "%OUT%\x86"

echo [2/3] 收集产物……
copy /y "%~dp0src\BatLint.Cli\bin\Release\net48\batlint.exe" "%OUT%\" >nul
copy /y "%~dp0src\BatLint.Cli\bin\Release\net48\batlint.exe.config" "%OUT%\" >nul
copy /y "%~dp0src\BatLint.Core\bin\Release\net48\BatLint.Core.dll" "%OUT%\" >nul
copy /y "%~dp0src\batIDE48\bin\Release\net48\batIDE.exe" "%OUT%\" >nul
copy /y "%~dp0src\batIDE48\bin\Release\net48\batIDE.exe.config" "%OUT%\" >nul
copy /y "%~dp0src\batIDE48\bin\Release\net48\Scintilla.NET.dll" "%OUT%\" >nul
copy /y "%~dp0src\batIDE48\bin\Release\net48\x86\Scintilla.dll" "%OUT%\x86\" >nul
copy /y "%~dp0src\batIDE48\bin\Release\net48\x86\Lexilla.dll" "%OUT%\x86\" >nul

echo [3/3] 完成：%OUT%
echo 运行 %OUT%\batIDE.exe（可拖 .bat 到 exe 上直接打开）
endlocal
