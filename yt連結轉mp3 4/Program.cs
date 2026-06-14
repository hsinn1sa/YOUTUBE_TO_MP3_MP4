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
        private static readonly string CurrentAppVersion = "1.0.5";

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

                    string json = await client.GetStringAsync(url);

                    var tagMatch = Regex.Match(json, @"""tag_name"":\s*""v?([\d\.]+)""");
                    var downloadMatch = Regex.Match(json, @"""browser_download_url"":\s*""([^""]+\.exe)""");

                    if (tagMatch.Success)
                    {
                        string latestAppVersion = tagMatch.Groups[1].Value;

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
                                if (!downloadMatch.Success)
                                {
                                    MessageBox.Show("抓取更新檔下載連結失敗！", "更新提示");
                                    return false;
                                }

                                string downloadUrl = downloadMatch.Groups[1].Value;
                                string currentExePath = Application.ExecutablePath; // 當前正在執行的舊 exe 路徑
                                string startupPath = Application.StartupPath;

                                // 定義路徑
                                string tempDownloadPath = Path.Combine(startupPath, "new_version.tmp");
                                string updateExePath = Path.Combine(startupPath, "update_assistant.exe");

                                // 提示正在下載
                                Task.Run(() => MessageBox.Show("正在背景下載更新檔，請稍候...", "下載中", MessageBoxButtons.OK, MessageBoxIcon.Information));

                                // 1. 下載新版檔案到暫存檔
                                byte[] fileBytes = await client.GetByteArrayAsync(downloadUrl);
                                File.WriteAllBytes(tempDownloadPath, fileBytes);

                                // 2. 清除上次殘留的助理，並把剛剛下載好的新靈魂複製一份當作更新助理
                                if (File.Exists(updateExePath)) { try { File.Delete(updateExePath); } catch { } }
                                File.Copy(tempDownloadPath, updateExePath, true);

                                // 3. 啟動更新助理，並把「當前被鎖定的舊 exe 路徑」傳給它
                                Process.Start(new ProcessStartInfo
                                {
                                    FileName = updateExePath,
                                    Arguments = $"/update-apply \"{currentExePath}\"",
                                    UseShellExecute = true
                                });

                                // 4. 舊程式立刻閃退，放開檔案鎖定！
                                Environment.Exit(0);
                                return false;
                            }
                            else
                            {
                                return false;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"檢查更新時發生連線錯誤：{ex.Message}", "偵錯提示");
                return true;
            }

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

                // 🎯 強力保險：等足 2 秒，確保舊版主程式已經完全閃退、釋放檔案鎖定
                Thread.Sleep(2000);

                string tempExePath = Application.ExecutablePath; // 當前正在執行的 update_assistant.exe
                string backupExePath = targetExePath + ".bak";

                // 1. 刪除上一次遺留的備份檔
                if (File.Exists(backupExePath)) { try { File.Delete(backupExePath); } catch { } }

                // 2. 把原本的舊版主程式改名為 .bak 備份（騰出空位）
                if (File.Exists(targetExePath))
                {
                    File.Move(targetExePath, backupExePath);
                }

                // 3. 把自己（新版程式）複製過去，取代原本舊主程式的路徑！
                File.Copy(tempExePath, targetExePath, true);

                // 4. 重新啟動那台已經變成全新版本的主程式！
                Process.Start(new ProcessStartInfo { FileName = targetExePath, UseShellExecute = true });

                // 5. 用 CMD 延遲 1 秒（等自己安全退出後），把這個暫存的 update_assistant.exe 砍掉
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