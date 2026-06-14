using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace yt連結轉mp3_4
{
    public partial class Form1 : Form
    {
        private CancellationTokenSource _titleCts;
        private bool _isDownloading = false;

        public Form1()
        {
            // 註冊 Big5 等編碼提供者，確保繁體中文路徑與解析正常
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            InitializeComponent();
            this.MaximumSize = this.Size;
            this.MinimumSize = this.Size;
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private async void Form1_Load(object sender, EventArgs e)
        {
            await Task.Delay(1000);
            await CheckYtDlpUpdate();
        }

        #region 更新檢查
        async Task CheckYtDlpUpdate()
        {
            try
            {
                string currentVersion = await GetYtDlpVersion();
                guna2HtmlLabel2.Text = $"目前版本：{currentVersion}";

                string latestVersion = await GetLatestYtDlpVersionFromGitHub();

                if (currentVersion != latestVersion)
                {
                    var result = MessageBox.Show(
                        $"偵測到 yt-dlp 有新版本 ({latestVersion})，要更新嗎？",
                        "yt-dlp 更新",
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);

                    if (result == DialogResult.Yes)
                        await ExecuteUpdate();
                    else
                        guna2HtmlLabel2.Text = $"已略過更新";
                }
                else
                {
                    guna2HtmlLabel2.Text = $"已是最新版本";
                }
            }
            catch
            {
                guna2HtmlLabel2.Text = "更新檢查失敗";
            }
        }

        async Task<string> GetLatestYtDlpVersionFromGitHub()
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.UserAgent.ParseAdd("request");
                string url = "https://api.github.com/repos/yt-dlp/yt-dlp/releases/latest";
                var json = await client.GetStringAsync(url);
                var match = Regex.Match(json, @"""tag_name"":\s*""v?([\d\.]+)""");
                if (match.Success) return match.Groups[1].Value;
                return "未知版本";
            }
        }

        async Task<string> GetYtDlpVersion()
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = "--version",
                    WorkingDirectory = Application.StartupPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (Process process = Process.Start(psi))
                {
                    string version = await process.StandardOutput.ReadToEndAsync();
                    process.WaitForExit();
                    return version.Trim();
                }
            }
            catch
            {
                return "未知版本";
            }
        }

        private async Task ExecuteUpdate()
        {
            try
            {
                guna2HtmlLabel2.Text = "正在更新 yt-dlp...";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = "-U",
                    WorkingDirectory = Application.StartupPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                await Task.Run(() =>
                {
                    using (Process process = Process.Start(psi))
                        process.WaitForExit();
                });

                string version = await GetYtDlpVersion();
                guna2HtmlLabel2.Text = $"更新完成 ({version})";
            }
            catch (Exception ex)
            {
                MessageBox.Show("更新失敗：" + ex.Message);
                guna2HtmlLabel2.Text = "更新失敗";
            }
        }
        #endregion

        #region 共用方法
        private bool IsYoutubeUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            url = url.ToLower();

            // 修正並優化網址判定，支援標準與短網址格式
            return url.Contains("youtube.com") || url.Contains("youtu.be");
        }

        private ProcessStartInfo CreateYtDlpProcess(string args)
        {
            return new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = args,
                WorkingDirectory = Application.StartupPath,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(950),
                StandardErrorEncoding = Encoding.GetEncoding(950)
            };
        }

        private async Task GetVideoTitle(string url, CancellationToken token)
        {
            try
            {
                guna2HtmlLabel3.Text = "讀取影片資訊中...";

                ProcessStartInfo psi = new ProcessStartInfo
                {
                    FileName = "yt-dlp.exe",
                    Arguments = $"--get-title --encoding utf-8 \"{url}\"",
                    WorkingDirectory = Application.StartupPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (Process process = new Process { StartInfo = psi })
                {
                    process.Start();

                    using (token.Register(() => { try { process.Kill(); } catch { } }))
                    {
                        string title = "";
                        string error = "";

                        await Task.Run(() =>
                        {
                            using (StreamReader reader = new StreamReader(process.StandardOutput.BaseStream, Encoding.UTF8))
                            {
                                title = reader.ReadToEnd();
                            }
                            using (StreamReader errReader = new StreamReader(process.StandardError.BaseStream, Encoding.UTF8))
                            {
                                error = errReader.ReadToEnd();
                            }
                        });

                        process.WaitForExit();

                        if (token.IsCancellationRequested) return;

                        if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(title))
                        {
                            title = title.Replace("\r", "").Replace("\n", "").Trim();
                            guna2HtmlLabel3.Text = title;
                        }
                        else
                        {
                            guna2HtmlLabel3.Text = "讀取失敗";
                        }
                    }
                }
            }
            catch
            {
                if (!token.IsCancellationRequested)
                {
                    guna2HtmlLabel3.Text = "程式執行出錯 請檢查元件";
                }
            }
        }

        private async Task Download(string url, string format)
        {
            labelStatus.Text = "準備下載...";

            string baseFolder = Path.Combine(Application.StartupPath, "下載的東西");
            string targetFolder = Path.Combine(baseFolder, format);
            Directory.CreateDirectory(targetFolder);

            string resolution = "1080";
            string args = "";

            // 定義通用 ffmpeg 音量平衡濾鏡參數 (EBU R128 標準，目標響度 -14 LUFS，真實峰值 -1 dBFS 確保絕不爆音)
            string ffmpegAudioFilter = "-af loudnorm=I=-11:TP=-1:LRA=11";

            if (format == "mp3")
            {
                // 【核心修改】轉檔 MP3 時注入音量平衡濾鏡，並強制最高音質編碼 (--audio-quality 0)
                args = $"--encoding utf-8 -x --audio-format mp3 --audio-quality 0 --newline " +
                       $"--postprocessor-args \"ffmpeg:{ffmpegAudioFilter}\" " +
                       $"-o \"下載的東西/mp3/%(title)s.%(ext)s\" \"{url}\"";
            }
            else
            {
                // 【核心修改】合併 MP4 影片與音軌時，同樣注入音量平衡濾鏡到封裝參數中
                args = $"--encoding utf-8 -f \"bestvideo[height<={resolution}][ext=mp4]+bestaudio[ext=m4a]/best[height<={resolution}][ext=mp4]/best\" " +
                       $"--merge-output-format mp4 " +
                       $"--postprocessor-args \"ffmpeg:-vcodec libx264 -pix_fmt yuv420p -acodec aac {ffmpegAudioFilter} -threads 4 -preset superfast\" " +
                       $"--newline -o \"下載的東西/mp4/%(title)s.%(ext)s\" \"{url}\"";
            }

            using (Process process = new Process { StartInfo = CreateYtDlpProcess(args) })
            {
                process.OutputDataReceived += (s, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data) || this.IsDisposed) return;

                    Match match = Regex.Match(e.Data, @"(\d+\.?\d*)%");
                    if (match.Success)
                        Invoke(new Action(() => labelStatus.Text = $"{(format == "mp3" ? "音樂" : "影片")}下載中 {match.Groups[1].Value}%"));

                    if (format == "mp3" && e.Data.Contains("[ExtractAudio]"))
                        Invoke(new Action(() => labelStatus.Text = "正在轉換為 MP3 格式並平衡音量..."));

                    if (format == "mp4")
                    {
                        if (e.Data.Contains("[Merger]"))
                            Invoke(new Action(() => labelStatus.Text = "影片音訊合併與音量平衡中..."));
                        if (e.Data.Contains("[VideoConvertor]"))
                            Invoke(new Action(() => labelStatus.Text = "正在執行轉檔..."));
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await Task.Run(() => process.WaitForExit());

                if (this.IsDisposed) return;

                Invoke(new Action(() =>
                {
                    if (process.ExitCode == 0)
                    {
                        labelStatus.Text = format == "mp3"
                            ? "音樂下載完成 (存至 mp3 資料夾，已平衡音量)"
                            : "影片下載完成 (存至 mp4 資料夾，已平衡音量)";
                    }
                    else
                    {
                        labelStatus.Text = "下載失敗，請檢查網路或連結";
                    }
                }));
            }
        }

        private async Task ProcessDownload(string url, string format)
        {
            if (!IsYoutubeUrl(url))
            {
                MessageBox.Show("請輸入有效的 YouTube 連結");
                return;
            }

            SetUIStatus(false);

            string actualUrl = url;

            if (url.Contains("list="))
            {
                var result = MessageBox.Show(
                    "這似乎是一個「合輯連結」。\n\n按【是】：下載整份合輯\n按【否】：只下載這一首",
                    "合輯詢問",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    actualUrl = url.Split('&')[0];
                    labelStatus.Text = "已切換為單首模式";
                    await GetVideoTitle(actualUrl, CancellationToken.None);
                }
                else if (result == DialogResult.Yes)
                {
                    guna2HtmlLabel3.Text = "合輯下載中...";
                }
                else
                {
                    SetUIStatus(true);
                    return;
                }
            }
            await Download(actualUrl, format);

            SetUIStatus(true);
        }

        private void SetUIStatus(bool enabled)
        {
            _isDownloading = !enabled;
            guna2Button1.Enabled = enabled;
            guna2Button2.Enabled = enabled;
            guna2TextBox1.Enabled = enabled;
        }

        private async void guna2Button1_Click(object sender, EventArgs e)
        {
            await ProcessDownload(guna2TextBox1.Text.Trim(), "mp3");
        }

        private async void guna2Button2_Click(object sender, EventArgs e)
        {
            await ProcessDownload(guna2TextBox1.Text.Trim(), "mp4");
        }

        private void guna2ControlBox1_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void guna2HtmlLabel2_Click(object sender, EventArgs e)
        {
        }
        #endregion

        #region 自動偵測連結
        private async void guna2TextBox1_TextChanged(object sender, EventArgs e)
        {
            if (_isDownloading) return;

            string url = guna2TextBox1.Text.Trim();

            _titleCts?.Cancel();
            _titleCts = new CancellationTokenSource();
            var token = _titleCts.Token;

            if (IsYoutubeUrl(url))
            {
                if (url.Contains("list="))
                {
                    guna2HtmlLabel3.Text = "偵測到合輯連結，下載時將詢問模式";
                    return;
                }

                try
                {
                    await GetVideoTitle(url, token);
                }
                catch (OperationCanceledException) { }
            }
            else if (string.IsNullOrWhiteSpace(url))
            {
                guna2HtmlLabel3.Text = "請輸入 YouTube 連結";
            }
            else
            {
                guna2HtmlLabel3.Text = "無效的網址格式";
            }
        }
        #endregion
    }
}
