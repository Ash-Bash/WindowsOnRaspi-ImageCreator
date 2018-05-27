cd c:\ &&
bcdedit /store P:\EFI\Microsoft\Boot\bcd /set {default} testsigning on &&
bcdedit /store P:\EFI\Microsoft\Boot\bcd /set {default} nointegritychecks on