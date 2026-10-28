@echo off
rem TabDeck Agent'i Windows'ta hazir gelen .NET Framework derleyicisiyle derler.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%CSC%" /nologo /optimize /out:TabDeckAgent.exe /r:"%REF%\System.Web.Extensions.dll" /r:"%REF%\System.Drawing.dll" /r:"%REF%\Microsoft.CSharp.dll" /r:"%REF%\System.Core.dll" TabDeckAgent.cs
if errorlevel 1 (
  echo Derleme basarisiz.
  pause
  exit /b 1
)
echo TabDeckAgent.exe olusturuldu.
