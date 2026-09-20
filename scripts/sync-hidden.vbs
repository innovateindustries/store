' Lanzador invisible del sync (sin ventana de consola).
Dim sh, root
Set sh = CreateObject("Wscript.Shell")
root = Left(WScript.ScriptFullName, InStrRev(WScript.ScriptFullName, "\"))
sh.Run "powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File """ & root & "sync-pages.ps1""", 0, False
