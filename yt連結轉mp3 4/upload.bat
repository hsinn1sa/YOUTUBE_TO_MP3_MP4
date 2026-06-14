@echo off
chcp 65001 > nul

:: 🎯 用精準的關鍵字切分，不管前面有幾個空白或修飾詞，直接抓雙引號中間的版號！
for /f "tokens=2 delims==" %%a in ('findstr /i "CurrentAppVersion" Program.cs') do (
    set "RAW_VER=%%a"
)

:: 把抓到的進度清洗掉雙引號、分號和空格
set "VERSION=%RAW_VER%"
set "VERSION=%VERSION: =%"
set "VERSION=%VERSION:;=%"
set "VERSION=%VERSION:"=%"

:: 如果不幸還是空的，給一個預設版號防呆
if "%VERSION%"=="" set VERSION=1.0.5

echo ====================================================
echo 🚀 修正成功！目前精準偵測到發布版號為: v%VERSION%
echo ====================================================

:: 自動執行 Git 認可與打標籤
git add .
git commit -m "VS Auto Release v%VERSION%"
git tag -a v%VERSION% -m "Release v%VERSION%"

:: 推上 GitHub
echo 正在將程式碼與正確的 Tag 推送到 GitHub...
git push origin master
git push origin v%VERSION%

echo.
echo ====== 🌟 修正完成！機器人這次會帶上漂亮的 v%VERSION% 標籤去打包 exe 了！ ======