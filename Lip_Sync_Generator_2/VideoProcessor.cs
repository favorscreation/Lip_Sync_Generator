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
        /// フレームを画像ファイルとして出力
        /// </summary>

        private void CreateVideoFromFramesPipe(List<Mat> frames, string audioPath, string outputPath, bool AlphaVideo, string tempVideoName)
        {
            string ffmpegArgs;
            //FFmpegの設定
            if (AlphaVideo)
            {
                // 透過動画 (WebM) を出力
                ffmpegArgs = $"-y -r {_configManager.Config.framerate} -f image2pipe -vcodec png -i - -c:v libvpx-vp9 -pix_fmt yuva420p -lossless 1 {tempVideoName}.webm";

            }
            else
            {
                // カラーキー処理 (MP4) を出力
                string bgColor = $"{_configManager.Config.background.R:X2}{_configManager.Config.background.G:X2}{_configManager.Config.background.B:X2}";
                ffmpegArgs = $"-y -hwaccel none -r {_configManager.Config.framerate} -f image2pipe -vcodec png -i - -vf colorkey=0x{bgColor}:{_configManager.Config.similarity}:{_configManager.Config.blend} -c:v libopenh264 -pix_fmt yuv420p {tempVideoName}.mp4";
            }
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = _configManager.Config.ffmpegPath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Arguments = ffmpegArgs;
                    Debug.WriteLine($"ffmpeg command(CreateVideoFromFramesPipe) : {process.StartInfo.FileName} {process.StartInfo.Arguments}");

                    process.Start();

                    using (var stdin = process.StandardInput.BaseStream)
                    {
                        foreach (Mat frame in frames)
                        {
                            byte[] byteArray = frame.ToBytes(".png");
                            stdin.Write(byteArray, 0, byteArray.Length);
                        }
                    }
                    process.WaitForExit();
                    string error = process.StandardError.ReadToEnd();
                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine($"ffmpeg error(CreateVideoFromFramesPipe): ");
                        throw new Exception("ffmpeg failed:" + error);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during video creation: {ex.Message}");
                throw new Exception("Failed to create video from frames", ex);
            }
        }


        /// <summary>
        /// 動画生成処理 (中間ファイル使用)
        /// </summary>
        public void ConvertToTransparentWithPipe(string audioPath, Config.FileCollection fileCollection, Action<string> progressCallback, bool AlphaVideo)
        {
            //tempファイル名を生成
            Guid g = System.Guid.NewGuid();
            string guid = g.ToString("N").Substring(0, 8);
            string tempVideoName = $"temp_video_{guid}";

            string outPath = @"outputs/";
            //出力パス
            if (!Directory.Exists(outPath))
                Directory.CreateDirectory(outPath);
            string outputPath;

            if (AlphaVideo)
            {
                outputPath = outPath + Path.GetFileNameWithoutExtension(audioPath) + ".webm";
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
            List<Mat> frames = new List<Mat>();


            try
            {
                using (var baseMat = new Mat(size, OpenCvSharp.MatType.CV_8UC4))
                {
                    if (AlphaVideo)
                        baseMat.SetTo(new OpenCvSharp.Scalar(0, 0, 0, 0));
                    else
                        baseMat.SetTo(new OpenCvSharp.Scalar(bb, gb, rb, 255));
                    //フレームごとの処理
                    //目存在フラグ
                    bool eye_exist = true;
                    if (fileCollection.Eyes.Count == 0)
                        eye_exist = false;

                    for (int currentFrame = 0; currentFrame < averageListCopy.Count; currentFrame++)
                    {
                        using (var outputMat = baseMat.Clone())
                        {
                            // 音量によって表示画像を切り替える
                            //分割数
                            int divide = inputsMatBody.Count;
                            //基準ステップ
                            float step = (float)(averageListCopy.Max() / divide / _configManager.Config.lipSync_threshold + 0.0001);
                            int dispNum = ((int)(averageListCopy[currentFrame] / step));
                            if (dispNum >= divide)
                            {
                                dispNum = divide - 1;
                            }

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
                                    //目の画像を合成(アルファブレンド)
                                    TransparentComposition(outputMat, inputsMatEyes[eyeIndex]);
                                }
                                else
                                {
                                    //目の画像を合成(アルファブレンド)しない
                                    using (var eyeMask = inputsMatEyes[eyeIndex].ExtractChannel(3))
                                    {
                                        inputsMatEyes[eyeIndex].CopyTo(outputMat, eyeMask);
                                    }
                                }
                            }
                            frames.Add(outputMat.Clone());
                        }
                        if (currentFrame % 10 == 0)
                        {
                            progressCallback(((float)currentFrame / averageListCopy.Count * 100).ToString("f0") + "%");
                        }
                    }

                    CreateVideoFromFramesPipe(frames, audioPath, outputPath, AlphaVideo, tempVideoName);
                    try
                    {
                        ReplaceAudio(AlphaVideo ? $"{tempVideoName}.webm" : $"{tempVideoName}.mp4", audioPath, outputPath, AlphaVideo);
                    }
                    finally
                    {
                        File.Delete(AlphaVideo ? $"{tempVideoName}.webm" : $"{tempVideoName}.mp4"); // 一時ファイルを削除
                    }

                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                throw new Exception(ex.Message, ex.InnerException);
            }
            finally
            {
                foreach (var item in _resizedImageCache)
                {
                    item.Value.Dispose();
                }
                _resizedImageCache.Clear();
                foreach (var frame in frames)
                {
                    frame.Dispose();
                }
                GC.Collect();
            }
        }


        /// <summary>
        /// 連番PNGファイルから動画を作成する (FFmpeg コマンドライン版)
        /// </summary>
        private void CreateVideoFromFrames(string tempDir, string audioPath, string outputPath, bool AlphaVideo, string tempVideoName)
        {
            string ffmpegArgs;
            //FFmpegの設定
            if (AlphaVideo)
            {
                // 透過動画 (WebM) を出力
                ffmpegArgs = $"-y -r {_configManager.Config.framerate} -i {tempDir}\\frame_%04d.png -c:v libvpx-vp9 -pix_fmt yuva420p -lossless 1 {tempVideoName}.webm";

            }
            else
            {
                // カラーキー処理 (MP4) を出力
                string bgColor = $"{_configManager.Config.background.R:X2}{_configManager.Config.background.G:X2}{_configManager.Config.background.B:X2}";
                ffmpegArgs = $"-y -hwaccel none -r {_configManager.Config.framerate} -i {tempDir}\\frame_%04d.png -vf colorkey=0x{bgColor}:{_configManager.Config.similarity}:{_configManager.Config.blend} -c:v libopenh264 -pix_fmt yuv420p {tempVideoName}.mp4";
            }
            try
            {
                using (Process process = new Process())
                {
                    process.StartInfo.FileName = _configManager.Config.ffmpegPath;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Arguments = ffmpegArgs;
                    Debug.WriteLine($"ffmpeg command(CreateVideoFromFrames) : {process.StartInfo.FileName} {process.StartInfo.Arguments}");

                    process.Start();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine($"ffmpeg error(CreateVideoFromFrames): ");
                        throw new Exception("ffmpeg failed:" + error);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during video creation: {ex.Message}");
                throw new Exception("Failed to create video from frames", ex);
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
                    ffmpegArgs = $"-y -i \"{inputVideoPath}\" -i \"{inputAudioPath}\" -c:v copy -c:a libopus -map 0:v -map 1:a \"{outputVideoPath}\"";
                else
                    ffmpegArgs = $"-y -i \"{inputVideoPath}\" -i \"{inputAudioPath}\" -c:v copy -c:a aac \"{outputVideoPath}\"";

                using (Process process = new Process())
                {
                    process.StartInfo.FileName = ConfigManager.CurrentDir + "\\ffmpeg\\ffmpeg.exe";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardError = true;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.Arguments = ffmpegArgs;
                    Debug.WriteLine($"ffmpeg command(ReplaceAudio) : {process.StartInfo.FileName} {process.StartInfo.Arguments}");
                    process.Start();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine($"ffmpeg error(ReplaceAudio): ");
                        throw new Exception("ffmpeg failed:" + error);
                    }
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

            int numPixels = src.Height * src.Width;

            byte[] srcData = new byte[numPixels * 4];
            byte[] addData = new byte[numPixels * 4];

            // Matからデータをコピー
            unsafe
            {
                fixed (byte* pSrc = srcData)
                {
                    Buffer.MemoryCopy(src.DataPointer, pSrc, srcData.Length, srcData.Length);
                }
                fixed (byte* pAdd = addData)
                {
                    Buffer.MemoryCopy(add.DataPointer, pAdd, addData.Length, addData.Length);
                }
            }

            // 並列処理でアルファブレンド
            Parallel.For(0, numPixels, (index) =>
            {
                int pixelIndex = index * 4;
                if (pixelIndex + 3 >= addData.Length || pixelIndex >= srcData.Length)
                {
                    // インデックスが範囲外の場合、処理をスキップ
                    return;
                }

                float alpha = (float)addData[pixelIndex + 3] / 255.0f; // 合成する画像のアルファ値を0-1の範囲に変換

                if (alpha > 0)
                {
                    // アルファブレンド処理
                    // B
                    srcData[pixelIndex] = (byte)((addData[pixelIndex] * alpha) + (srcData[pixelIndex] * (1 - alpha)));
                    // G
                    srcData[pixelIndex + 1] = (byte)((addData[pixelIndex + 1] * alpha) + (srcData[pixelIndex + 1] * (1 - alpha)));
                    // R
                    srcData[pixelIndex + 2] = (byte)((addData[pixelIndex + 2] * alpha) + (srcData[pixelIndex + 2] * (1 - alpha)));

                    // アルファ値を維持（合成後のアルファ値は変更しない）
                    if (pixelIndex + 3 < srcData.Length)
                        srcData[pixelIndex + 3] = (byte)(addData[pixelIndex + 3]);
                }
                // add画像が完全に透明の場合、srcのアルファ値は変更しない

            });
            unsafe
            {
                fixed (byte* pSrc = srcData)
                {
                    // 結果をMatに戻す
                    Buffer.MemoryCopy(pSrc, src.DataPointer, srcData.Length, srcData.Length);
                }
            }
        }
    }
}