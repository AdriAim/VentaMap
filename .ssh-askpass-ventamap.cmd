@echo off
powershell.exe -NoProfile -Command "$line = (Select-String -Path 'HOSTINGER_CREDENCIALES.local.md' -Pattern '^\s*- Password:\s*(.+?)\s*$').Matches[0].Groups[1].Value; [Console]::Write(($line -replace '[` ]', ''))"
