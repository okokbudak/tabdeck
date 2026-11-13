@echo off
rem TabDeck Agent'i Windows'ta hazir gelen .NET Framework derleyicisiyle derler.
cd /d "%~dp0"
set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set REF=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
"%CSC%" /nologo /optimize /codepage:65001 /target:winexe /out:TabDeckAgent.exe /win32icon:tabdeck.ico /resource:tabdeck.ico,TabDeck.tabdeck.ico /r:"%REF%\System.Web.Extensions.dll" /r:"%REF%\System.Drawing.dll" /r:"%REF%\System.Windows.Forms.dll" /r:"%REF%\Microsoft.CSharp.dll" /r:"%REF%\System.Core.dll" *.cs
if errorlevel 1 (
  echo Derleme basarisiz.
  pause
  exit /b 1
)
echo TabDeckAgent.exe olusturuldu.
