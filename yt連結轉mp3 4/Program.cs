using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace yt連結轉mp3_4
{
    internal static class Program
    {
        // 🎯 1. 軟體目前版本（故意寫 1.0.0 舊版，等一下才能測試自動更新）
        private static readonly string CurrentAppVersion = "1.0.0";

        // 🎯 2. 你的 GitHub 帳號
        private static readonly string GitHubUser = "hsinn1sa";

        // 🎯 3. 你的專案倉庫名稱（必須跟網址一樣大寫）
        private static readonly string GitHubRepo = "YOUTUBE_TO_MP3_MP4";

        [STAThread]
        static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();

            // 1. 檢查是否為「更新覆蓋模式」執行
            if (args.Contains("/update-apply"))
            {
                HandleUpdateOverwrite();
                return;
            }

            // 2. 正常啟動流程：先卡住檢查是否有更新
            bool passed = CheckAppUpdateAsync().GetAwaiter().GetResult();

            if (passed)
            {
                // 沒有更新或是網路失敗，順暢進入主本體
                Application.Run(new Form1());
            }
            else
            {
                // 有更新正在背景下載或取消更新，直接退出
                Application.Exit();
            }
        }

        private static async Task<bool> CheckAppUpdateAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                    string url = $"https://api.github.com/repos/{GitHubUser}/{GitHubRepo}/releases/latest";

                    var json = await client.GetStringAsync(url);
                    var match = Regex.Match(json, @"""tag_name"":\s*""v?([\d\.]+)""");

                    if (match.Success)
                    {
                        string latestAppVersion = match.Groups[1].Value;

                        // 🔍 發現新版本！
                        if (CurrentAppVersion != latestAppVersion)
                        {
                            var result = MessageBox.Show(
                                $"偵測到軟體有新版本 ({latestAppVersion})！\n\n是否需要更新？\n(按「是」將自動完成更新並重啟軟體，按「否」退出程式)",
                                "軟體更新提示",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information);

                            if (result == DialogResult.Yes)
                            {
                                // 尋找 GitHub 上的 exe 下載連結 (在 Assets 裡面)
                                var browserDownloadUrlMatch = Regex.Match(json, @"""browser_download_url"":\s*""([^""]+\.exe)""");
                                if (!browserDownloadUrlMatch.Success)
                                {
                                    // 如果找不到直接的 exe，就退一步找 Release 網頁網址
                                    var htmlUrlMatch = Regex.Match(json, @"""html_url"":\s*""([^""]+)""");
                                    if (htmlUrlMatch.Success)
                                    {
                                        Process.Start(new ProcessStartInfo { FileName = htmlUrlMatch.Groups[1].Value, UseShellExecute = true });
                                    }
                                    return false;
                                }

                                string downloadUrl = browserDownloadUrlMatch.Groups[1].Value;
                                string currentExePath = Application.ExecutablePath;
                                string newExePath = Path.Combine(Application.StartupPath, "new_version.tmp");

                                // 提示使用者正在背景下載
                                // 注意：此時 Form1 還沒開，所以畫一個簡單的提示
                                Task.Run(() => MessageBox.Show("正在背景下載更新檔，請稍候...", "下載中", MessageBoxButtons.OK, MessageBoxIcon.Information));

                                // 下載新版的 exe
                                byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                                File.WriteAllBytes(newExePath, fileBytes);

                                // 啟動「新下載的檔案」並傳入覆蓋參數，同時把當前舊版的路徑傳過去給它改名
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = newExePath,
                                    Arguments = $"/update-apply \"{currentExePath}\"",
                                    UseShellExecute = true
                                });

                                // 回傳 false，不要啟動目前舊版本的 Form1，讓主程式立刻退場解鎖
                                return false;
                            }
                            else
                            {
                                // 使用者選否，不給用舊版，直接退出
                                return false;
                            }
                        }
                    }
                }
            }
            catch
            {
                // 如果斷網或 GitHub 掛掉，為了讓使用者能盲用，直接放行進入主程式
                return true;
            }

            // 沒有新版本，放行
            return true;
        }

        /// <summary>
        /// 更新覆蓋助理核心邏輯
        /// </summary>
        private static void HandleUpdateOverwrite()
        {
            try
            {
                // 從參數中抓到原本舊主程式的路徑
                string[] args = Environment.GetCommandLineArgs();
                if (args.Length < 3) return;
                string targetExePath = args[2];

                // 休息 1.5 秒，確保原本的舊版主程式已經完全閃退並釋放檔案鎖定
                Thread.Sleep(1500);

                string tempExePath = Application.ExecutablePath; // 當前正在執行的 new_version.tmp
                string backupExePath = targetExePath + ".bak";

                // 刪除上一次遺留的備份檔
                if (File.Exists(backupExePath)) File.Delete(backupExePath);

                // 把原本的舊版主程式改名為 .bak 備份（騰出空位）
                if (File.Exists(targetExePath)) File.Move(targetExePath, backupExePath);

                // 把自己複製過去取代原本的舊主程式路徑
                File.Copy(tempExePath, targetExePath, true);

                // 重新啟動那台已經變成全新版本的主程式！
                Process.Start(new ProcessStartInfo { FileName = targetExePath, UseShellExecute = true });

                // 用殘留的指令碼在背景等自己關閉後，把這個暫存的 new_version.tmp 砍掉
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c choice /t 1 /d y /n & del \"{tempExePath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("全自動覆蓋更新失敗，請手動至 GitHub 下載最新版。錯誤訊息：" + ex.Message);
            }
            finally
            {
                Application.Exit();
            }
        }
    }
}