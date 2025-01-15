using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OpenCvSharp;

namespace Lip_Sync_Generator_2
{
    public class VideoProcessor
    {
        private ConfigManager _configManager;
        private int _frameCount = 0;
        private int _blinkFrameCount = 0;
        private int _nextBlinkFrame = 0;
        private Dictionary<string, Mat> _resizedImageCache = new Dictionary<string, Mat>();

        public VideoProcessor(ConfigManager configManager)
        {
            _configManager = configManager;
        }

        /// <summary>
        /// 音声のボリュームを解析
        /// </summary>
        public List<float> AnalyzeAudio(string audio_path)
        {
            List<float> averageList = new List<float>();

            NAudio.Wave.AudioFileReader audio_reader;
            try
            {
                audio_reader = new NAudio.Wave.AudioFileReader(audio_path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
                // 例外を上位に伝播させる
                throw;
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
        /// 動画生成処理 (パイプ処理)
        /// </summary>
        public void ConvertToTransparentWithPipe(string audioPath, Config.FileCollection fileCollection, Action<string> progressCallback, bool AlphaVideo)
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
            //出力パス
            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);
            string outputPath;

            if (AlphaVideo)
            {
                outputPath = outPath + Path.GetFileNameWithoutExtension(audioPath) + ".mov";
            }
            else
            {
                outputPath = outPath + Path.GetFileNameWithoutExtension(audioPath) + ".mp4";
            }

            List<float> averageListCopy = AnalyzeAudio(audioPath);

            List<Mat> inputsMatBody = new List<Mat>();
            List<Mat> inputsMatEyes = new List<Mat>();

            OpenCvSharp.Size size = new OpenCvSharp.Size();
            bool checkSize = false;

            //バックグラウンドカラー
            var rb = _configManager.Config.background.R;
            var gb = _configManager.Config.background.G;
            var bb = _configManager.Config.background.B;

            //body画像を読み込み、リサイズ
            foreach (var item in fileCollection.Body)
            {
                Mat resizedMat;
                if (_resizedImageCache.TryGetValue(item.Path, out resizedMat))
                {
                    inputsMatBody.Add(resizedMat);
                    continue;
                }
                using (var tempMat = Cv2.ImRead(item.Path, OpenCvSharp.ImreadModes.Unchanged))
                {

                    if (checkSize == false)
                    {
                        size = new OpenCvSharp.Size(tempMat.Width, tempMat.Height);
                        //縦横ピクセルが2の倍数でないとエラーになるので奇数なら1を足す
                        if (size.Width % 2 != 0)
                            size.Width += 1;
                        if (size.Height % 2 != 0)
                            size.Height += 1;

                        checkSize = true;
                    }
                    resizedMat = tempMat.Resize(size);
                    _resizedImageCache[item.Path] = resizedMat;
                    inputsMatBody.Add(resizedMat);
                }
            }

            //目画像を読み込み、リサイズ
            List<Config.FileName> eyeFiles = fileCollection.Eyes.ToList();
            for (int i = 0; i < eyeFiles.Count; i++)
            {
                Mat resizedMat;
                if (_resizedImageCache.TryGetValue(eyeFiles[i].Path, out resizedMat))
                {
                    inputsMatEyes.Add(resizedMat);
                    continue;
                }
                using (var tempMat = Cv2.ImRead(eyeFiles[i].Path, OpenCvSharp.ImreadModes.Unchanged))
                {
                    resizedMat = tempMat.Resize(size);
                    _resizedImageCache[eyeFiles[i].Path] = resizedMat;
                    inputsMatEyes.Add(resizedMat);
                }
            }

            try
            {
                using (var baseMat = new Mat(size, OpenCvSharp.MatType.CV_8UC4))
                {
                    if (AlphaVideo)
                        baseMat.SetTo(new OpenCvSharp.Scalar(0, 0, 0, 0));
                    else
                        baseMat.SetTo(new OpenCvSharp.Scalar(bb, gb, rb, 255));


                    string ffmpegArgs;
                    if (AlphaVideo)
                    {
                        // 透過動画 (MOV) を出力
                        ffmpegArgs = $"-y -f rawvideo -pix_fmt rgba -s {size.Width}x{size.Height} -r {_configManager.Config.framerate} -i - -c:v png -pix_fmt rgba \"{tempMovPath}\"";
                    }
                    else
                    {
                        // カラーキー処理 (MP4) を出力
                        string bgColor = $"{_configManager.Config.background.R:X2}{_configManager.Config.background.G:X2}{_configManager.Config.background.B:X2}";
                        ffmpegArgs = $"-y -f rawvideo -pix_fmt rgba -s {size.Width}x{size.Height} -r {_configManager.Config.framerate} -i - -vf colorkey=0x{bgColor}:{_configManager.Config.similarity}:{_configManager.Config.blend} -c:v libx264 -pix_fmt yuv420p \"{tempMovPath}\"";
                    }
                    using (Process process = new Process())
                    {
                        process.StartInfo.FileName = _configManager.Config.ffmpegPath; // コンフィグからffmpegパスを取得
                        process.StartInfo.UseShellExecute = false;
                        process.StartInfo.RedirectStandardError = true;
                        process.StartInfo.RedirectStandardInput = true;
                        process.StartInfo.CreateNoWindow = true;
                        process.StartInfo.Arguments = ffmpegArgs;
                        process.Start();
                        //分割数
                        int divide = inputsMatBody.Count;
                        //基準ステップ
                        float step = (float)(averageListCopy.Max() / divide / _configManager.Config.lipSync_threshold + 0.0001);

                        int maxPoolSize = 24;
                        int concurrentFrameCount = 8;
                        BlockingCollection<(int frameIndex, byte[] frameData)> frameQueue = new BlockingCollection<(int, byte[])>(maxPoolSize);

                        var tasks = Enumerable.Range(0, concurrentFrameCount)
                            .Select(_ => Task.Run(() =>
                            {
                                foreach (var item in frameQueue.GetConsumingEnumerable())
                                {
                                    int frame = item.frameIndex;
                                    byte[] frameBytes = item.frameData;
                                    using (MemoryStream ms = new MemoryStream())
                                    {
                                        ms.Write(frameBytes, 0, frameBytes.Length);
                                        byte[] allBytes = ms.ToArray();
                                        lock (process.StandardInput.BaseStream)
                                        {
                                            process.StandardInput.BaseStream.Write(allBytes, 0, allBytes.Length);
                                        }
                                    }
                                    if (frame % 10 == 0)
                                    {
                                        progressCallback(((float)frame / averageListCopy.Count * 100).ToString("f0") + "%");
                                    }

                                }
                            })).ToArray();

                        for (int frame = 0; frame < averageListCopy.Count; frame++)
                        {
                            // 音量によって表示画像を切り替える
                            int dispNum = ((int)(averageListCopy[frame] / step));
                            if (dispNum >= divide)
                            {
                                dispNum = divide - 1;
                            }
                            using (var outputMat = baseMat.Clone())
                            {
                                //Bodyの画像を合成
                                using (var bodyMask = inputsMatBody[dispNum].ExtractChannel(3))
                                {
                                    inputsMatBody[dispNum].CopyTo(outputMat, bodyMask);
                                }
                                // まばたき処理
                                _frameCount++;
                                if (_blinkFrameCount == 0)
                                {
                                    if (_frameCount % (int)(_configManager.Config.framerate * (1 / _configManager.Config.blink_frequency)) == 0)
                                    {
                                        _blinkFrameCount = 1;
                                        _nextBlinkFrame = 0;
                                    }
                                }

                                if (eye_exist)
                                {
                                    int eyeIndex = 0;
                                    if (_blinkFrameCount > 0)
                                    {
                                        int phaseLength = inputsMatEyes.Count;
                                        int normalizedIndex = _nextBlinkFrame % (phaseLength * 2 - 2);

                                        if (normalizedIndex < phaseLength)
                                            eyeIndex = normalizedIndex;
                                        else
                                            eyeIndex = phaseLength - (normalizedIndex - phaseLength) - 2;

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

                                    if (AlphaVideo)
                                    {
<<<<<<< Updated upstream
                                        //目の画像を合成(アルファブレンド)
                                        TransparentComposition(outputMat, inputsMatEyes[eyeIndex]);
                                    }
                                    else
                                    {
                                        //目の画像を合成(アルファブレンド)しない
                                        using (var eyeMask = inputsMatEyes[eyeIndex].ExtractChannel(3))
=======
                                        Cv2.CvtColor(outputMat, rgbaMat, OpenCvSharp.ColorConversionCodes.BGR2RGBA);

                                        // FFmpegにデータを送信
                                        byte[] frameBytes = new byte[rgbaMat.Width * rgbaMat.Height * 4];

                                        unsafe
>>>>>>> Stashed changes
                                        {
                                            inputsMatEyes[eyeIndex].CopyTo(outputMat, eyeMask);
                                        }
                                    }
                                }
                                // outputMatをBGRからRGBAに変換
                                using (Mat rgbaMat = new Mat())
                                {
                                    Cv2.CvtColor(outputMat, rgbaMat, ColorConversionCodes.BGR2RGBA);

                                    // FFmpegにデータを送信
                                    byte[] frameBytes = new byte[rgbaMat.Width * rgbaMat.Height * 4];

                                    unsafe
                                    {
                                        fixed (byte* p = frameBytes)
                                        {
                                            Buffer.MemoryCopy(rgbaMat.DataPointer, p, frameBytes.Length, frameBytes.Length);
                                        }
                                    }
                                    frameQueue.Add((frame, frameBytes));
                                }
                            }
                        }
                        frameQueue.CompleteAdding();
                        Task.WaitAll(tasks);
                        process.StandardInput.Close();
                        string error = process.StandardError.ReadToEnd();
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                            throw new Exception("ffmpeg failed:" + error);
                    }


                    //ReplaceAudioの処理
                    try
                    {
                        ReplaceAudio(tempMovPath, audioPath, outputPath, AlphaVideo);
                    }
                    finally
                    {
                        File.Delete(tempMovPath); // 一時ファイルを削除
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw new Exception(ex.Message, ex.InnerException); // 必要に応じてカスタム例外を検討
            }
            finally
            {
                foreach (var item in _resizedImageCache)
                {
                    item.Value.Dispose();
                }
                _resizedImageCache.Clear();
                GC.Collect();
            }
        }

        /// <summary>
        /// オーディオの入れ替え (FFmpeg コマンドライン版)
        /// </summary>
        private void ReplaceAudio(string inputVideoPath, string inputAudioPath, string outputVideoPath, bool AlphaVideo)
        {
            try
            {
                string ffmpegArgs;
                if (AlphaVideo)
                    ffmpegArgs = $"-y -i \"{inputVideoPath}\" -i \"{inputAudioPath}\" -c:v copy -c:a aac -map 0:v -map 1:a \"{outputVideoPath}\"";
                else
                    ffmpegArgs = $"-y -i \"{inputVideoPath}\" -i \"{inputAudioPath}\" -c:v copy -c:a aac  \"{outputVideoPath}\"";

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ConfigManager.CurrentDir + "\\ffmpeg\\ffmpeg.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Arguments = ffmpegArgs;
                    process.Start();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                        throw new Exception("ffmpeg failed:" + error);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during audio replacement: {ex.Message}");
                throw new Exception("Failed to replace audio", ex);
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

                        // アルファ値を維持（合成後のアルファ値は変更しない）
                        src_b[3] = (byte)Math.Min(255, (add_b[3] + src_b[3]));

                    }
                    else // add画像が完全に透明の場合、srcのアルファ値は変更しない
                    {
                        //必要であればここに処理を書く
                    }
                });
            }
        }
    }
}