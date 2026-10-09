; Windows installer for the desktop app. Built by scripts/package-desktop.sh with:
;   makensis -DVERSION=x.y.z -DSOURCE=<published win-x64 folder> -DOUTFILE=<setup.exe> yass.nsi
; Installs for the current user only (no administrator prompt) into %LOCALAPPDATA%\Programs\YASS,
; adds a Start menu entry and an uninstaller, and registers with "Apps & features".

Unicode true
SetCompressor /SOLID lzma
RequestExecutionLevel user

!define APP "YASS"
!define EXE "Yass.Desktop.exe"
!define UNINSTALL_KEY "Software\Microsoft\Windows\CurrentVersion\Uninstall\YASS"

Name "${APP}"
OutFile "${OUTFILE}"
; Always this folder, never one the user picks: install and uninstall clear it whole, which is
; only safe for a folder that holds nothing but the app.
InstallDir "$LOCALAPPDATA\Programs\${APP}"
Icon "${__FILEDIR__}\..\..\assets\logo\yass.ico"
UninstallIcon "${__FILEDIR__}\..\..\assets\logo\yass.ico"
BrandingText "${APP} ${VERSION}"

VIProductVersion "0.0.0.0"
VIAddVersionKey "ProductName" "${APP}"
VIAddVersionKey "ProductVersion" "${VERSION}"
VIAddVersionKey "FileDescription" "${APP} installer"
VIAddVersionKey "FileVersion" "${VERSION}"

Page instfiles
UninstPage uninstConfirm
UninstPage instfiles

Section "Install"
  SetOutPath "$INSTDIR"
  ; An upgrade replaces the app whole, so no file of the old version is left behind.
  RMDir /r "$INSTDIR"
  SetOutPath "$INSTDIR"
  File /r "${SOURCE}\*.*"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  CreateShortcut "$SMPROGRAMS\${APP}.lnk" "$INSTDIR\${EXE}" "" "$INSTDIR\${EXE}" 0

  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayName" "${APP}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayVersion" "${VERSION}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "DisplayIcon" "$INSTDIR\${EXE}"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "Publisher" "YASS"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr HKCU "${UNINSTALL_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoModify" 1
  WriteRegDWORD HKCU "${UNINSTALL_KEY}" "NoRepair" 1
SectionEnd

Section "Uninstall"
  ; The app's files only: scans and settings live in the user's own folders and stay.
  Delete "$SMPROGRAMS\${APP}.lnk"
  RMDir /r "$INSTDIR"
  DeleteRegKey HKCU "${UNINSTALL_KEY}"
SectionEnd
