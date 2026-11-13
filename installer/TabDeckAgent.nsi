; TabDeck Agent kurulum dosyasi (NSIS 3). Yonetici yetkisi istemez; kullanici klasorune kurar.
; Derleme: makensis -DVERSION=1.0.0 -DSRC=<exe ve LICENSE.txt klasoru> -DOUTFILE=<cikti.exe> TabDeckAgent.nsi
Unicode true

!ifndef VERSION
  !error "VERSION tanimli degil"
!endif
!ifndef SRC
  !error "SRC tanimli degil"
!endif
!ifndef OUTFILE
  !error "OUTFILE tanimli degil"
!endif

!define APP "TabDeck Agent"
!define UNINST_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\TabDeckAgent"

Name "${APP}"
OutFile "${OUTFILE}"
InstallDir "$LOCALAPPDATA\Programs\TabDeck"
InstallDirRegKey HKCU "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel user
SetCompressor /SOLID lzma
VIProductVersion "${VERSION}.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${APP} kurulumu"
VIAddVersionKey "LegalCopyright" "GPL-3.0"

!include "MUI2.nsh"

!define MUI_ABORTWARNING
!define MUI_ICON "${SRC}\tabdeck.ico"
!define MUI_UNICON "${SRC}\tabdeck.ico"
!define MUI_FINISHPAGE_RUN "$INSTDIR\TabDeckAgent.exe"
!define MUI_FINISHPAGE_RUN_TEXT "TabDeck Agent'ı şimdi başlat"

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_LICENSE "${SRC}\LICENSE.txt"
!insertmacro MUI_PAGE_COMPONENTS
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Turkish"
!insertmacro MUI_LANGUAGE "English"

Section "TabDeck Agent" SecMain
  SectionIn RO
  ; Guncellemede calisan surumu kapat (exe kilitli kalmasin)
  nsExec::Exec 'taskkill /IM TabDeckAgent.exe /F'
  Sleep 500

  SetOutPath "$INSTDIR"
  File "${SRC}\TabDeckAgent.exe"
  File "${SRC}\LICENSE.txt"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateDirectory "$SMPROGRAMS\TabDeck"
  CreateShortcut "$SMPROGRAMS\TabDeck\TabDeck Agent.lnk" "$INSTDIR\TabDeckAgent.exe"
  CreateShortcut "$SMPROGRAMS\TabDeck\TabDeck Agent'ı kaldır.lnk" "$INSTDIR\Uninstall.exe"
  ; 1.0.x'ten kalanlar: yonetici kisayolu (artik pencerede dugme var) ve Baslangic klasoru kisayolu
  ; (acilista baslatma artik HKCU\...\Run ile, agent ayarlarindan da acilip kapatilabilir)
  Delete "$SMPROGRAMS\TabDeck\TabDeck Agent (yönetici).lnk"
  Delete "$SMSTARTUP\TabDeck Agent.lnk"

  WriteRegStr HKCU "${UNINST_KEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINST_KEY}" "Publisher" "OKWorks"
  WriteRegStr HKCU "${UNINST_KEY}" "DisplayIcon" "$INSTDIR\TabDeckAgent.exe"
  WriteRegStr HKCU "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINST_KEY}" "NoRepair" 1
SectionEnd

; Agent penceresindeki "Windows acilisinda baslat" secenegiyle ayni kayit
Section "Windows açılışında başlat (sistem tepsisinde)" SecAutostart
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TabDeck Agent" '"$INSTDIR\TabDeckAgent.exe" --tray'
SectionEnd

Section "Uninstall"
  nsExec::Exec 'taskkill /IM TabDeckAgent.exe /F'
  Sleep 500

  Delete "$INSTDIR\TabDeckAgent.exe"
  Delete "$INSTDIR\LICENSE.txt"
  Delete "$INSTDIR\agent.json"
  Delete "$INSTDIR\Uninstall.exe"
  ; OBS ekran goruntuleri kullanicinin verisidir: screenshots klasoru silinmez
  RMDir "$INSTDIR"

  Delete "$SMPROGRAMS\TabDeck\TabDeck Agent.lnk"
  Delete "$SMPROGRAMS\TabDeck\TabDeck Agent (yönetici).lnk"
  Delete "$SMPROGRAMS\TabDeck\TabDeck Agent'ı kaldır.lnk"
  RMDir "$SMPROGRAMS\TabDeck"
  Delete "$SMSTARTUP\TabDeck Agent.lnk"
  DeleteRegValue HKCU "Software\Microsoft\Windows\CurrentVersion\Run" "TabDeck Agent"

  DeleteRegKey HKCU "${UNINST_KEY}"
SectionEnd
