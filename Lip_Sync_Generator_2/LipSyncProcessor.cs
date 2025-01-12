using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private int _nextBlinkFrame = 0; // 次のまばたきまでのフレーム数
        public bool AlphaVideo { get; set; } = false;
        private WaveOutEvent _outputDevice = new WaveOutEvent();
        public LipSyncProcessor(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        /// <summary>
        /// リストボックスのアイテムを上へ移動
        /// </summary>
        public void UpItem(ListBox listBox, Config.FileList list)
        {
            //リストボックスで選択されているインデックス取得
            var selectedItem = listBox.SelectedItem as Config.FileName;

            if (selectedItem == null)
                return;

            int index = list.IndexOf(selectedItem);
            //一つ上のインデックスが存在しない（-1）場合何もしない
            if (index - 1 == -1)
                return;

            //交換先のアイテムをバッファ
            var buff = list[index - 1];

            //交換実行
            list[index - 1] = list[index];
            list[index] = buff;

            //交換後のアイテムを選択
            listBox.SelectedItem = list[index - 1];
        }
        /// <summary>
        /// リストボックスのアイテムを下へ移動
        /// </summary>
        public void DownItem(ListBox listBox, Config.FileList list)
        {
            var selectedItem = listBox.SelectedItem as Config.FileName;
            if (selectedItem == null)
                return;

            int index = list.IndexOf(selectedItem);
            if (index + 1 == list.Count)
                return;

            var buff = list[index + 1];
            list[index + 1] = list[index];
            listBox.SelectedItem = list[index + 1];
        }

        /// <summary>
        /// ファイルドロップ時の処理
        /// </summary>
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
        /// <summary>
        /// 音声のボリュームを解析
        /// </summary>
        public List<float> AnalyzeAudio(string audio_path)
        {
            List<float> averageList = new List<float>();

            AudioFileReader audio_reader;
            try
            {
                audio_reader = new AudioFileReader(audio_path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                return new List<float>();
            }

            float[] samples = new float[audio_reader.Length / audio_reader.BlockAlign * audio_reader.WaveFormat.Channels];
            audio_reader.Read(samples, 0, samples.Length);

            float time = (float)audio_reader.TotalTime.TotalSeconds;

            _configManager.Config.average_samples = (int)(samples.Length / time / _configManager.Config.framerate);


            //平均化処理
            for (int i = 0; i < samples.Length; i += _configManager.Config.average_samples)
            {

                float sum = 0;
                for (int j = 0; j < _configManager.Config.average_samples; j++)
                {
                    //samplesの範囲を超えないようにif
                    if (i + j >= samples.Length)
                        break;

                    //絶対値化
                    sum += Math.Abs(samples[i + j]);
                }
                averageList.Add(sum / (float)_configManager.Config.average_samples * _configManager.Config.sample_scale);
            }
            return averageList;
        }
        /// <summary>
        /// リストからアイテム削除
        /// </summary>
        public void DeleteItem(ListBox listBox, Config.FileList list)
        {
            if (listBox.SelectedItem is Config.FileName selectedItem)
            {
                list.Remove(selectedItem);
            }


            if (listBox.Items.Count > 0)
                listBox.SelectedIndex = listBox.Items.Count - 1;
        }
        /// <summary>
        /// 動画生成処理
        /// </summary>
        private void CreateMovie(string audioPath, Config.FileCollection fileCollection, Action<string> progressCallback)
        {
            //目存在フラグ
            bool eye_exist = true;
            if (fileCollection.Eyes.Count == 0)
                eye_exist = false;

            //tempファイル名を生成
            Guid g = System.Guid.NewGuid();
            string guid = g.ToString("N").Substring(0, 8);
            string tempMovPath = "temp_" + guid + ".mp4";
            string outPath = @"outputs/";

            List<float> averageListCopy = AnalyzeAudio(audioPath);

            List<Mat> inputsMatBody = new List<Mat>();
            List<Mat> inputsMatEyes = new List<Mat>();

            OpenCvSharp.Size size = new OpenCvSharp.Size();
            bool checkSize = false;

            //バックグラウンドカラー
            var rb = _configManager.Config.background.R;
            var gb = _configManager.Config.background.G;
            var bb = _configManager.Config.background.B;

            //body画像を取得
            foreach (var item in fileCollection.Body)
            {
                //透明度込みで読み込む
                var mat = Cv2.ImRead(item.Path, ImreadModes.Unchanged);
                inputsMatBody.Add(mat);

                //bodyの最初の1枚を基準サイズとする
                if (checkSize == false)
                {
                    size = new OpenCvSharp.Size(inputsMatBody[0].Width, inputsMatBody[0].Height);
                }
                checkSize = true;
            }

            //目画像を取得
            List<Config.FileName> eyeFiles = fileCollection.Eyes.ToList();
            for (int i = 0; i < eyeFiles.Count; i++)
            {
                //透明度込みで読み込む
                inputsMatEyes.Add(Cv2.ImRead(eyeFiles[i].Path, ImreadModes.Unchanged));
            }


            //縦横ピクセルが2の倍数でないとエラーになるので奇数なら1を足す
            if (size.Width % 2 != 0)
                size.Width += 1;
            if (size.Height % 2 != 0)
                size.Height += 1;

            //Resize
            for (var i = 0; i < inputsMatBody.Count; i++)
            {
                inputsMatBody[i] = inputsMatBody[i].Resize(size);
            }
            for (var i = 0; i < inputsMatEyes.Count; i++)
            {
                inputsMatEyes[i] = inputsMatEyes[i].Resize(size);
            }

            //出力パス
            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);
            string outputPath = outPath + Path.GetFileNameWithoutExtension(audioPath) + ".mp4";

            try
            {
                //口パク目パチ処理
                using (var vw = new VideoWriter(tempMovPath, FourCC.H264, _configManager.Config.framerate, size))
                using (var baseMat = new Mat(size, MatType.CV_8UC4, new Scalar(bb, gb, rb, 255)))
                {
                    //分割数
                    int divide = inputsMatBody.Count;
                    //基準ステップ
                    float step = averageListCopy.Max() / divide / _configManager.Config.lipSync_threshold + 0.0001f;

                    Debug.WriteLine(step);

                    for (int frame = 0; frame < averageListCopy.Count; frame++)
                    {

                        //音量によって表示画像を切り替える
                        int dispNum = ((int)(averageListCopy[frame] / step));
                        if (dispNum >= divide)
                            dispNum = divide - 1;

                        using (var outputMat = baseMat.Clone())
                        {
                            //アルファチャンネルをマスクとし、outputMatにコピーする
                            //メモリリーク原因となるのでExtractChannelもusing
                            using (var bodyMask = inputsMatBody[dispNum].ExtractChannel(3))
                            {
                                inputsMatBody[dispNum].CopyTo(outputMat, bodyMask);
                            }

                            //目パチ
                            _frameCount++;

                            if (_blinkFrameCount == 0)
                            {
                                if (_frameCount % (int)(_configManager.Config.framerate * (1 / _configManager.Config.blink_frequency)) == 0)
                                {
                                    _blinkFrameCount = 1; // まばたきを開始
                                    _nextBlinkFrame = 0;
                                }
                            }

                            if (eye_exist)
                            {
                                int eyeIndex = 0;
                                if (_blinkFrameCount > 0)
                                {
                                    int phaseLength = inputsMatEyes.Count; // フレーム数を取得
                                    int normalizedIndex = _nextBlinkFrame % (phaseLength * 2 - 2); // 往復運動にするため
                                    if (normalizedIndex < phaseLength)
                                        eyeIndex = normalizedIndex;
                                    else
                                        eyeIndex = phaseLength - (normalizedIndex - phaseLength) - 2;


                                    // まばたきが完了したら次のまばたきまでのフレームをカウントする
                                    if (_nextBlinkFrame >= (inputsMatEyes.Count * 2 - 2))
                                    {
                                        _blinkFrameCount = 0;
                                        _nextBlinkFrame = 0;
                                    }
                                    else
                                    {
                                        _nextBlinkFrame++;
                                    }
                                }
                                // 目の画像を表示
                                TransparentComposition(outputMat, inputsMatEyes[eyeIndex]);
                            }

                            //フレーム書き出し
                            vw.Write(outputMat);
                        }

                        if (frame % 10 == 0)
                            progressCallback(((float)frame / averageListCopy.Count * 100).ToString("f0") + "%");

                        if (frame % 1000 == 0)
                            GC.Collect();
                    }
                }
                //後処理
                foreach (Mat item in inputsMatBody)
                {
                    item.Release();
                    item.Dispose();
                }
                foreach (Mat item in inputsMatEyes)
                {
                    item.Release();
                    item.Dispose();
                }

                ReplaceAudio(tempMovPath, audioPath, outputPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);

                throw new Exception(ex.Message, ex.InnerException);
            }
            finally
            {
                File.Delete(tempMovPath);
                GC.Collect();
            }
            convert2Transparent(outputPath);
        }
        /// <summary>
        /// オーディオの入れ替え
        /// </summary>
        private void ReplaceAudio(string inputVideoPath, string inputAudioPath, string outputVideoPath)
        {
            FFMpeg.ReplaceAudio(inputVideoPath, inputAudioPath, outputVideoPath);
        }

        /// <summary>
        /// 透過動画に変換
        /// </summary>
        private void convert2Transparent(string inputMoviePath)
        {
            if (AlphaVideo)
                try
                {
                    using (Process process = new Process())
                    {
                        string outPath = ConfigManager.CurrentDir + @"\outputs\" + Path.GetFileNameWithoutExtension(inputMoviePath) + ".mov";
                        process.StartInfo.FileName = ConfigManager.CurrentDir + "\\ffmpeg\\ffmpeg.exe";
                        // string bgColor = _configManager.Config.background.R.ToString("x2") + _configManager.Config.background.G.ToString("x2") + _configManager.Config.background.B.ToString("x2");
                        string bgColor = $"{_configManager.Config.background.R:X2}{_configManager.Config.background.G:X2}{_configManager.Config.background.B:X2}";

                        //-y 上書き
                        process.StartInfo.Arguments = $@"-y -i {inputMoviePath} -vf colorkey={bgColor}:{_configManager.Config.similarity}:{_configManager.Config.blend} -pix_fmt argb -c:v qtrle {outPath}";
                        process.Start();

                        // コマンド終了まで待機
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
        }
        /// <summary>
        /// 透過画像を重ね合わせる（アルファブレンド）
        /// </summary>
        /// <param name="src">合成先のMatオブジェクト</param>
        /// <param name="add">合成するMatオブジェクト</param>
        private void TransparentComposition(Mat src, Mat add)
        {
            if (src.Size() != add.Size())
            {
                throw new ArgumentException("画像のサイズが異なります。");
            }

            unsafe
            {
                int numPixels = src.Height * src.Width;

                Parallel.For(0, numPixels, (index) =>
                {
                    byte* src_b = src.DataPointer + index * 4;
                    byte* add_b = add.DataPointer + index * 4;

                    float alpha = (float)add_b[3] / 255.0f; // 合成する画像のアルファ値を0-1の範囲に変換

                    if (alpha > 0)
                    {
                        // アルファブレンド処理
                        src_b[0] = (byte)((add_b[0] * alpha) + (src_b[0] * (1 - alpha))); // B
                        src_b[1] = (byte)((add_b[1] * alpha) + (src_b[1] * (1 - alpha))); // G
                        src_b[2] = (byte)((add_b[2] * alpha) + (src_b[2] * (1 - alpha))); // R

                        // 合成後のアルファ値を不透明に設定 (不透明合成)
                        src_b[3] = 255;
                    }
                });
            }
        }
        /// <summary>
        /// リップシンク処理の実行
        /// </summary>
        public void Run(List<Config.FileName> selectedAudioItems, Config.FileCollection fileCollection, Action<string> progressCallback)
        {
            if (fileCollection.Body.Count == 0)
            {
                MessageBox.Show("画像がありません", "Error");
                throw new Exception("Item == 0");
            }
            if (selectedAudioItems.Count == 0)
            {
                MessageBox.Show("オーディオファイルが選択されていません", "Error");
                throw new Exception("Item == 0");
            }
            //スレッド数を8に制限
            ParallelOptions option = new ParallelOptions();
            option.MaxDegreeOfParallelism = 8;

            Parallel.ForEach(selectedAudioItems, option, p =>
            {
                CreateMovie(p.Path, fileCollection, progressCallback);
            });
        }
        /// <summary>
        /// オーディオ再生
        /// </summary>
        public void PlayAudio(string audio_path)
        {
            if (_outputDevice.PlaybackState == PlaybackState.Playing)
            {
                _outputDevice.Stop();
                return;
            }
            try
            {
                //オーディオ再生
                AudioFileReader afr = new AudioFileReader(audio_path);
                _outputDevice.Init(afr);
                _outputDevice.Play();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }
    }
}