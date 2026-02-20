@echo off
REM Pokrece Google Chrome u remote-debugging rezimu na portu 9222
REM Proverite da li je putanja do chrome.exe tacna!

set CHROME_PATH="C:\Program Files\Google\Chrome\Application\chrome.exe"
set PROFILE_DIR="%~dp0chrome_profile"

start "" %CHROME_PATH% --remote-debugging-port=9222 --user-data-dir=%PROFILE_DIR%

echo Chrome je pokrenut u remote-debugging rezimu na portu 9222.
pause
