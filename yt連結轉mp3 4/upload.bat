@echo off
:: 🎯 1. 自動讀取你 Program.cs 裡面的最新版號（例如 1.0.4）
for /f "tokens=3" %%i in ('findstr "CurrentAppVersion" Program.cs') do set VERSION=%%i
set VERSION=%VERSION:~1,-2%

echo 偵測到當前版號為: v%VERSION%

:: 🎯 2. 自動執行 Git 認可與打標籤
git add .
git commit -m "VS自動發布 v%VERSION%"
git tag -a v%VERSION% -m "Release v%VERSION%"

:: 🎯 3. 一鍵推上 GitHub（包含程式碼與 Tag 火種）
git push origin master
git push origin v%VERSION%

echo ====== GitHub 發布指令已發送，機器人開始背景編譯！ ======