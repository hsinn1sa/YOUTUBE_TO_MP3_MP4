@echo off
chcp 65001 > nul

:: ?? 1. 專門針對「CurrentAppVersion = "x.x.x"」設計的精準抓取法
for /f "tokens=2 delims==" %%a in ('findstr "CurrentAppVersion" Program.cs') do (
    set "RAW_VER=%%a"
)

:: ?? 2. 把等號右邊的字串清洗掉空格、分號、雙引號
set "VERSION=%RAW_VER%"
set "VERSION=%VERSION: =%"
set "VERSION=%VERSION:;=%"
set "VERSION=%VERSION:"=%"

:: 防呆機制：如果真的抓空了，給一個預設版號
if "%VERSION%"=="" set VERSION=1.0.5

echo ====================================================
echo ?? 偵測成功！目前抓取到的發布版號為: v%VERSION%
echo ====================================================

:: 3. 自動執行 Git 認可與打標籤
git add .
git commit -m "VS Auto Release v%VERSION%"
git tag -a v%VERSION% -m "Release v%VERSION%"

:: 4. 強力推送上 GitHub 觸發機器人
echo 正在將程式碼與正確的 Tag 推送到 GitHub...
git push origin master
git push origin v%VERSION%

echo.
echo ====== ?? 完美接通！GitHub 機器人已開始打包 v%VERSION% 的 exe 檔！ ======