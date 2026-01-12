using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // 必須: Marshal.Copy用
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using FFMpegCore;
using NAudio.Wave;
using OpenCvSharp;
using Path = System.IO.Path;

namespace Lip_Sync_Generator_2
{
    public class LipSyncProcessor
    {
        private ConfigManager _configManager;
        private int _frameCount = 0;
        private int _blinkFrameCount = 0;
        private int _nextBlinkFrame = 0;
        public bool AlphaVideo { get; set; } = false;
        private WaveOutEvent _outputDevice = new WaveOutEvent();

        public LipSyncProcessor(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        #region ListBox / DragDrop Operations
        public void UpItem(ListBox listBox, Config.FileList list)
        {
            var selectedItem = listBox.SelectedItem as Config.FileName;
            if (selectedItem == null) return;
            int index = list.IndexOf(selectedItem);
            if (index - 1 == -1) return;
            var buff = list[index - 1];
            list[index - 1] = list[index];
            list[index] = buff;
            listBox.SelectedItem = list[index - 1];
        }

        public void DownItem(ListBox listBox, Config.FileList list)
        {
            var selectedItem = listBox.SelectedItem as Config.FileName;
            if (selectedItem == null) return;
            int index = list.IndexOf(selectedItem);
            if (index + 1 == list.Count) return;
            var buff = list[index + 1];
            list[index + 1] = list[index];
            listBox.SelectedItem = list[index + 1];
        }

        public void DropFile(object sender, DragEventArgs e, Config.FileCollection fileCollection)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);
                foreach (var name in fileNames)
                {
                    var itemlist = (Config.FileList)((ListBox)sender).ItemsSource;
                    itemlist.Add(new Config.FileName(Path.GetFileName(name), name));
                }
                ((ListBox)sender).SelectedIndex = 0;
            }
        }

        public void DeleteItem(ListBox listBox, Config.FileList list)
        {
            if (listBox.SelectedItem is Config.FileName selectedItem)
            {
                list.Remove(selectedItem);
            }
            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = listBox.Items.Count - 1;
        }
        #endregion

        /// <summary>
        /// 音声解析（省メモリ・ストリーミング版）
        /// </summary>
        public List<float> AnalyzeAudio(string audio_path)
        {
            List<float> averageList = new List<float>();
            try
            {
                using (var audio_reader = new AudioFileReader(audio_path))
                {
                    float frameDuration = 1.0f / _configManager.Config.framerate;
                    int samplesPerFrame = (int)(audio_reader.WaveFormat.SampleRate * audio_reader.WaveFormat.Channels * frameDuration);

                    _configManager.Config.average_samples = samplesPerFrame;

                    float[] buffer = new float[samplesPerFrame];
                    int samplesRead;

                    while ((samplesRead = audio_reader.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        double sum = 0;
                        for (int i = 0; i < samplesRead; i++) sum += Math.Abs(buffer[i]);

                        float avg = 0;
                        if (samplesRead > 0)
                        {
                            avg = (float)(sum / samplesRead) * _configManager.Config.sample_scale;
                        }
                        averageList.Add(avg);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"AnalyzeAudio Error: {ex.Message}");
                // エラー時は空リストを返す
                return new List<float>();
            }
            return averageList;
        }

        /// <summary>
        /// メイン実行処理（直列実行・エラーハンドリング強化版）
        /// </summary>
        public void Run(List<Config.FileName> selectedAudioItems, Config.FileCollection fileCollection, Action<string> onProgress, Action<string, string> onError)
        {
            if (fileCollection.Body.Count == 0)
            {
                onError("画像ファイルが設定されていません。", "FileCollection.Body is empty.");
                return;
            }
            if (selectedAudioItems.Count == 0)
            {
                onError("オーディオファイルが選択されていません。", "selectedAudioItems is empty.");
                return;
            }

            int current = 0;
            int total = selectedAudioItems.Count;

            // Parallel.ForEachは廃止。直列実行で安定性を確保。
            foreach (var item in selectedAudioItems)
            {
                current++;
                try
                {
                    onProgress?.Invoke($"開始 ({current}/{total}): {item.Name}");

                    CreateMovie(item.Path, fileCollection, (prog) =>
                    {
                        onProgress?.Invoke($"({current}/{total}) {item.Name}: {prog}");
                    });
                }
                catch (Exception ex)
                {
                    string userMsg = $"ファイル「{item.Name}」の処理中にエラーが発生しました。\nこのファイルはスキップされます。";
                    string devMsg = $"[ERROR] Target: {item.Path}\nException: {ex.GetType().Name}\nMessage: {ex.Message}\nStackTrace:\n{ex.StackTrace}";

                    onError?.Invoke(userMsg, devMsg);
                }
            }
        }

        /// <summary>
        /// 動画生成（パイプライン・デッドロック対策版）
        /// </summary>
        private void CreateMovie(string audioPath, Config.FileCollection fileCollection, Action<string> progressCallback)
        {
            // パスチェック
            string ffmpegExePath = Path.Combine(ConfigManager.CurrentDir, "ffmpeg", "ffmpeg.exe");
            if (!File.Exists(ffmpegExePath)) throw new FileNotFoundException("ffmpeg.exeが見つかりません。", ffmpegExePath);
            if (!File.Exists(audioPath)) throw new FileNotFoundException("音声ファイルが見つかりません。", audioPath);

            // 一時ファイルパス
            string tempVideoPath = Path.Combine(ConfigManager.CurrentDir, $"temp_{Guid.NewGuid().ToString("N").Substring(0, 8)}.mp4");
            string outDir = Path.Combine(ConfigManager.CurrentDir, "outputs");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);
            string finalOutputPath = Path.Combine(outDir, Path.GetFileNameWithoutExtension(audioPath) + ".mp4");

            // 音声解析
            progressCallback("音声解析中...");
            List<float> averageList = AnalyzeAudio(audioPath);
            if (averageList.Count == 0) throw new Exception("音声データの解析結果が0件です。ファイルが破損している可能性があります。");

            // 画像読み込み
            List<Mat> inputsMatBody = new List<Mat>();
            List<Mat> inputsMatEyes = new List<Mat>();
            OpenCvSharp.Size size = new OpenCvSharp.Size();
            bool checkSize = false;

            try
            {
                foreach (var item in fileCollection.Body)
                {
                    if (!File.Exists(item.Path)) throw new FileNotFoundException($"Body画像なし: {item.Name}");
                    var mat = Cv2.ImRead(item.Path, ImreadModes.Unchanged);
                    if (mat.Empty()) throw new Exception($"Body画像読み込み失敗: {item.Name}");
                    inputsMatBody.Add(mat);
                    if (!checkSize) { size = new OpenCvSharp.Size(mat.Width, mat.Height); checkSize = true; }
                }
                foreach (var item in fileCollection.Eyes)
                {
                    if (!File.Exists(item.Path)) throw new FileNotFoundException($"Eyes画像なし: {item.Name}");
                    var mat = Cv2.ImRead(item.Path, ImreadModes.Unchanged);
                    if (mat.Empty()) throw new Exception($"Eyes画像読み込み失敗: {item.Name}");
                    inputsMatEyes.Add(mat);
                }

                // サイズ偶数化補正 (動画コーデック要件)
                if (size.Width % 2 != 0) size.Width++;
                if (size.Height % 2 != 0) size.Height++;

                for (int i = 0; i < inputsMatBody.Count; i++)
                {
                    var old = inputsMatBody[i]; inputsMatBody[i] = old.Resize(size); old.Dispose();
                }
                for (int i = 0; i < inputsMatEyes.Count; i++)
                {
                    var old = inputsMatEyes[i]; inputsMatEyes[i] = old.Resize(size); old.Dispose();
                }

                // --- パイプ処理開始 ---
                // -loglevel error: ログ出力を減らしてバッファ溢れを防ぐ
                // -preset ultrafast: 画質より速度優先（中間ファイルのため）
                string args = $"-y -loglevel error -f rawvideo -vcodec rawvideo -s {size.Width}x{size.Height} -r {_configManager.Config.framerate} -pix_fmt bgra -i - -c:v libx264 -preset ultrafast -pix_fmt yuv420p \"{tempVideoPath}\"";

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ffmpegExePath;
                    process.StartInfo.Arguments = args;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardError = true; // デッドロック対策

                    // 標準エラー出力を非同期で読み捨てる
                    process.ErrorDataReceived += (s, e) => { /* 必要ならログ出力 */ };

                    process.Start();
                    process.BeginErrorReadLine(); // 読み取り開始

                    var bgColor = new Scalar(_configManager.Config.background.B, _configManager.Config.background.G, _configManager.Config.background.R, 255);
                    byte[] buffer = new byte[size.Width * size.Height * 4]; // BGRA

                    int divide = inputsMatBody.Count;
                    float maxVol = averageList.Max();
                    if (maxVol == 0) maxVol = 1.0f;
                    float step = maxVol / divide / _configManager.Config.lipSync_threshold + 0.0001f;
                    int lastPercent = -1;

                    using (var baseMat = new Mat(size, MatType.CV_8UC4, bgColor))
                    using (var stdin = process.StandardInput.BaseStream)
                    {
                        for (int frame = 0; frame < averageList.Count; frame++)
                        {
                            if (process.HasExited) throw new Exception($"FFmpegプロセスが予期せず終了しました。ExitCode: {process.ExitCode}");

                            int dispNum = (int)(averageList[frame] / step);
                            if (dispNum >= divide) dispNum = divide - 1;

                            using (var outputMat = baseMat.Clone())
                            {
                                // Body合成
                                TransparentComposition(outputMat, inputsMatBody[dispNum]);

                                // 目パチ
                                UpdateBlinkState();
                                bool eye_exist = fileCollection.Eyes.Count > 0;

                                if (eye_exist)
                                {
                                    int eyeIndex = 0;
                                    if (_blinkFrameCount > 0) eyeIndex = CalculateEyeIndex(inputsMatEyes.Count);
                                    TransparentComposition(outputMat, inputsMatEyes[eyeIndex]);
                                }

                                try
                                {
                                    // メモリコピー & パイプ書き込み & フラッシュ
                                    Marshal.Copy(outputMat.Data, buffer, 0, buffer.Length);
                                    stdin.Write(buffer, 0, buffer.Length);
                                    stdin.Flush(); // 重要: バッファ詰まり防止
                                }
                                catch (IOException ex)
                                {
                                    Debug.WriteLine($"Pipe broken: {ex.Message}");
                                    break;
                                }
                            }

                            // 進捗通知（1%刻み）
                            int percent = (int)((float)frame / averageList.Count * 100);
                            if (percent > lastPercent)
                            {
                                lastPercent = percent;
                                progressCallback($"{percent}%");
                            }

                            if (frame % 2000 == 0) GC.Collect();
                        }
                    } // stdin.Close() -> FFmpeg終了処理へ

                    process.WaitForExit();
                }

                // 音声結合
                progressCallback("音声結合中...");
                ReplaceAudio(tempVideoPath, audioPath, finalOutputPath);

                // 透過動画
                if (AlphaVideo)
                {
                    progressCallback("透過動画変換中...");
                    convert2Transparent(finalOutputPath);
                }
            }
            finally
            {
                // リソース解放
                foreach (var m in inputsMatBody) m?.Dispose();
                foreach (var m in inputsMatEyes) m?.Dispose();
                inputsMatBody.Clear();
                inputsMatEyes.Clear();

                if (File.Exists(tempVideoPath)) try { File.Delete(tempVideoPath); } catch { }
                GC.Collect();
            }
        }

        private void UpdateBlinkState()
        {
            _frameCount++;
            if (_blinkFrameCount == 0)
            {
                int interval = (int)(_configManager.Config.framerate * (1.0f / _configManager.Config.blink_frequency));
                if (interval > 0 && _frameCount % interval == 0)
                {
                    _blinkFrameCount = 1;
                    _nextBlinkFrame = 0;
                }
            }
        }

        private int CalculateEyeIndex(int totalFrames)
        {
            int phaseLength = totalFrames;
            int normalized = _nextBlinkFrame % (phaseLength * 2 - 2);
            int idx = (normalized < phaseLength) ? normalized : phaseLength - (normalized - phaseLength) - 2;

            if (_nextBlinkFrame >= (totalFrames * 2 - 2)) { _blinkFrameCount = 0; _nextBlinkFrame = 0; }
            else { _nextBlinkFrame++; }
            return idx;
        }

        private void ReplaceAudio(string inputVideo, string inputAudio, string outputVideo)
        {
            string ffmpeg = Path.Combine(ConfigManager.CurrentDir, "ffmpeg", "ffmpeg.exe");
            string args = $"-y -i \"{inputVideo}\" -i \"{inputAudio}\" -c:v copy -c:a aac -map 0:v -map 1:a -shortest \"{outputVideo}\"";

            using (var p = new Process())
            {
                p.StartInfo.FileName = ffmpeg;
                p.StartInfo.Arguments = args;
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }
        }

        private void convert2Transparent(string inputMoviePath)
        {
            try
            {
                string outPath = Path.Combine(ConfigManager.CurrentDir, "outputs", Path.GetFileNameWithoutExtension(inputMoviePath) + ".mov");
                string ffmpeg = Path.Combine(ConfigManager.CurrentDir, "ffmpeg", "ffmpeg.exe");
                string bgColorHex = $"{_configManager.Config.background.R:X2}{_configManager.Config.background.G:X2}{_configManager.Config.background.B:X2}";
                string args = $"-y -i \"{inputMoviePath}\" -vf colorkey={bgColorHex}:{_configManager.Config.similarity}:{_configManager.Config.blend} -c:v qtrle \"{outPath}\"";

                using (var p = new Process())
                {
                    p.StartInfo.FileName = ffmpeg;
                    p.StartInfo.Arguments = args;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    p.WaitForExit();
                }
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }

        private void TransparentComposition(Mat src, Mat add)
        {
            if (src.Size() != add.Size()) return;
            unsafe
            {
                int len = src.Height * src.Width;
                byte* s = src.DataPointer;
                byte* a = add.DataPointer;
                for (int i = 0; i < len; i++)
                {
                    int idx = i * 4;
                    float alpha = a[idx + 3] / 255.0f;
                    if (alpha > 0)
                    {
                        s[idx] = (byte)((a[idx] * alpha) + (s[idx] * (1 - alpha)));     // B
                        s[idx + 1] = (byte)((a[idx + 1] * alpha) + (s[idx + 1] * (1 - alpha))); // G
                        s[idx + 2] = (byte)((a[idx + 2] * alpha) + (s[idx + 2] * (1 - alpha))); // R
                        s[idx + 3] = 255;
                    }
                }
            }
        }

        public void PlayAudio(string audio_path)
        {
            if (_outputDevice.PlaybackState == PlaybackState.Playing)
            {
                _outputDevice.Stop();
                return;
            }
            try
            {
                var afr = new AudioFileReader(audio_path);
                _outputDevice.Init(afr);
                _outputDevice.Play();
            }
            catch (Exception ex) { Debug.WriteLine(ex.Message); }
        }
    }
}