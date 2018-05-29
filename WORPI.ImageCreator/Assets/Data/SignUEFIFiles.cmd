@echo off
bcdedit.exe /store P:\EFI\Microsoft\Boot\bcd /set {default} testsigning on
bcdedit.exe /store P:\EFI\Microsoft\Boot\bcd /set {default} nointegritychecks on
pause
exit
