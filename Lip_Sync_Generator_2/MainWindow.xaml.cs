using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using Path = System.IO.Path;
using FFMpegCore;
using NAudio.Wave;
using LiveCharts;
using LiveCharts.Wpf;
using LiveCharts.Defaults;
using OpenCvSharp;
using Window = System.Windows.Window;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;
using System.Security.Cryptography;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Window;
using static Lip_Sync_Generator_2.Config;
using System.Drawing;
using System.Diagnostics.Metrics;
using System.Security.Policy;
using System.Windows.Media.Media3D;

namespace Lip_Sync_Generator_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static string CurrentDir = System.IO.Directory.GetCurrentDirectory();
        Config.Values config = new Config.Values();
        public Config.FileCollection fileCollection = new Config.FileCollection();

        string ffmpegDir = CurrentDir + "\\ffmpeg";

        public MainWindow()
        {
            InitializeComponent();

            //ffmpegのパスを通す
            GlobalFFOptions.Configure(options => options.BinaryFolder = ffmpegDir);

            Encoding enc = Encoding.GetEncoding("utf-8");

            config = new Config.Values();

            string config_path = @"config\config.json";
            string str = "";

            if (File.Exists(@"config\config.json"))
                str = new StreamReader(config_path, enc).ReadToEnd();

            //configフォルダがなければ作成
            if (Directory.Exists("config") == false)
                Directory.CreateDirectory("config");

            //presetフォルダがなければ作成
            if (Directory.Exists("preset") == false)
                Directory.CreateDirectory("preset");

            //presetフォルダがなければ作成
            if (Directory.Exists("outputs") == false)
                Directory.CreateDirectory("outputs");

            if (JsonUtil.JsonToConfig(str) == null)
            {
                config = new Config.Values();
                Notice_TextBox.Text = "デフォルト設定が適用されました";
                //Debug.WriteLine(JsonUtil.ToJson(config));

                //設定ファイル作成
                using (StreamWriter writer = new StreamWriter(config_path, false, enc))
                {
                    writer.WriteLine(JsonUtil.ToJson(config));
                }
            }
            else
            {
                config = JsonUtil.JsonToConfig(str)!;
                Notice_TextBox.Text = "設定(外部ファイル)を読み込みました";
            }

            TextBox1.Text = "frameRate = " + config!.framerate;
            TextBox2.Text = "average samples = " + config!.average_samples;
            TextBox3.Text = "sample scale = " + config!.sample_scale;
            TextBox4.Text = "lipSync threshold = " + config!.lipSync_threshold;
            TextBox5.Text = "blink interval Frame = " + config!.blink_intervalFrame;
            TextBox6.Text = "blink interval RandomFrame = " + config!.blink_interval_randomFrame;
            TextBox7.Text = "background Color = [" + config!.background[0] + " , " + config!.background[1] + " , " + config!.background[2] + "]";
            TextBox8.Text = "similarity = " + config.similarity;
            TextBox9.Text = "blend = " + config.blend;

            LipSync_th_Slider.Value = config!.lipSync_threshold;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            bind();
        }

        private void UpItem(ListBox listBox, Config.FileList list)
        {
            //リストボックスで選択されているインデックス取得
            int index = listBox.SelectedIndex;

            //一つ上のインデックスが存在しない（-1）場合何もしない
            if (index - 1 == -1 || index == -1)
                return;

            //交換先のアイテムをバッファ
            var buff = list[index - 1];

            //交換実行
            list[index - 1] = list[index];
            list[index] = buff;

            //交換後のアイテムを選択
            listBox.SelectedIndex = index - 1;
        }

        private void DownItem(ListBox listBox, Config.FileList list)
        {
            int index = listBox.SelectedIndex;

            if (index + 1 == list.Count || index == -1)
                return;

            var buff = list[index + 1];
            list[index + 1] = list[index];
            list[index] = buff;
            listBox.SelectedIndex = index + 1;
        }

        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            UpItem(body_listBox, fileCollection.Body);
        }
        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            DownItem(body_listBox, fileCollection.Body);
        }

        private void UpButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            UpItem(Eyes_listBox, fileCollection.Eyes);
        }

        private void DownButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            DownItem(Eyes_listBox, fileCollection.Eyes);
        }

        //ドロップされたときの動作。ファイルリストに追加する。
        private void drop_box_Drop(object sender, DragEventArgs e)
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

        //ファイルドロップ時のカーソル変更
        private void Drop_box_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.All;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void Body_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (body_listBox.SelectedIndex != -1)
                    BodyImage.Source = new BitmapImage(new Uri(fileCollection.Body[body_listBox.SelectedIndex].Path));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void Eye_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Eyes_listBox.SelectedIndex != -1)
                    EyeImage.Source = new BitmapImage(new Uri(fileCollection.Eyes[Eyes_listBox.SelectedIndex].Path));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }


        /// <summary>
        /// 音声のボリュームを解析
        /// </summary>
        private List<float> audio_Analyze(string audio_path)
        {
            List<float> averageList = new List<float>();

            AudioFileReader audio_reader;
            try
            {
                audio_reader = new AudioFileReader(audio_path);
            }
            catch (Exception ex)
            {
                Notice_TextBox.Text = ex.Message;
                return new List<float>();
            }

            float[] samples = new float[audio_reader.Length / audio_reader.BlockAlign * audio_reader.WaveFormat.Channels];
            audio_reader.Read(samples, 0, samples.Length);

            float time = (float)audio_reader.TotalTime.TotalSeconds;

            config!.average_samples = (int)(samples.Length / time / config.framerate);


            //平均化処理
            for (int i = 0; i < samples.Length; i += config.average_samples)
            {

                float sum = 0;
                for (int j = 0; j < config.average_samples; j++)
                {
                    //samplesの範囲を超えないようにif
                    if (i + j >= samples.Length)
                        break;

                    //絶対値化
                    sum += Math.Abs(samples[i + j]);
                }
                averageList.Add(sum / (float)config.average_samples * config.sample_scale);
            }


            return averageList;
        }

        List<float> CurrentAverageList = new List<float>();

        /// <summary>
        /// 
        /// </summary>
        private void DrawingChart()
        {
            audioChart.Series.Clear(); //描画領域のクリア
            audioChart.DisableAnimations = true;  //アニメーション禁止
            audioChart.DataTooltip = null;//ツールチップ無効
            audioChart.AxisY[0].MinValue = 0;//軸は0からスタート
            audioChart.AxisX[0].MinValue = 0;

            //データ作成
            double[] ys1 = Enumerable.Range(0, CurrentAverageList.Count).Select(i => (double)CurrentAverageList[i]).ToArray();

            LineSeries lineSeries = new LineSeries();
            lineSeries.PointGeometry = null;
            lineSeries.Values = new ChartValues<double>(ys1);


            audioChart.Series.Add(lineSeries); //シリーズを登録
        }

        private void Audio_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fileCollection.Audio.Count == 0)
                return;

            try
            {
                CurrentAverageList = audio_Analyze(fileCollection.Audio[Audio_listBox.SelectedIndex].Path);
                DrawingChart();
            }
            catch (Exception ex)
            {
                Notice_TextBox.Text = ex.Message;
            }
        }

        Random random = new Random();

        private void newCreate(string audio_path, FileCollection fileCollection)
        {
            //目存在フラグ
            bool eye_exist = true;
            if (fileCollection.Eyes.Count == 0)
                eye_exist = false;

            //tempファイル名を生成
            Guid g = System.Guid.NewGuid();
            string guid = g.ToString("N").Substring(0, 8);
            string temp_mov_path = "temp_" + guid + ".mp4";
            string out_path = @"outputs/";

            List<float> averageListCopy = audio_Analyze(audio_path);

            List<Mat> inputs_Mat_body = new List<Mat>();

            List<Mat> inputs_Mat_eyes = new List<Mat>();

            OpenCvSharp.Size size = new OpenCvSharp.Size();
            bool CheckSize = false;

            //バックグラウンドカラー
            var rb = config.background[0];
            var gb = config.background[1];
            var bb = config.background[2];

            //body画像を取得
            foreach (var item in fileCollection.Body)
            {
                //透明度込みで読み込む
                var mat = Cv2.ImRead(item.Path, ImreadModes.Unchanged);
                inputs_Mat_body.Add(mat);

                //bodyの最初の1枚を基準サイズとする
                if (CheckSize == false)
                {
                    size = new OpenCvSharp.Size(inputs_Mat_body[0].Width, inputs_Mat_body[0].Height);
                }
                CheckSize = true;
            }

            //目画像を取得
            foreach (var item in fileCollection.Eyes)
            {
                //透明度込みで読み込む
                inputs_Mat_eyes.Add(Cv2.ImRead(item.Path, ImreadModes.Unchanged));
            }

            //目のまばたき乱数を生成
            List<int> reserveFrame = new List<int>();
            for (int i = 0; i < averageListCopy.Count; i++)
            {
                if (i % config.blink_intervalFrame == 0 && i != 0)
                {
                    reserveFrame.Add(i - random.Next(0, config.blink_interval_randomFrame));
                }
            }

            //口パク目パチ処理
            using (var vw = new VideoWriter(temp_mov_path, FourCC.H264, config!.framerate, size))
            using (var basemat = new Mat(size, MatType.CV_8UC4, new Scalar(bb, gb, rb, 255)))
            {

                //分割数
                int divide = inputs_Mat_body.Count;
                //基準ステップ
                float step = averageListCopy.Max() / divide / config.lipSync_threshold + 0.0001f;

                Debug.WriteLine(step);

                int blinkframe_count = 0;

                for (int frame = 0; frame < averageListCopy.Count; frame++)
                {

                    //音量によって表示画像を切り替える
                    int dispNum = ((int)(averageListCopy[frame] / step));
                    if (dispNum >= divide)
                        dispNum = divide - 1;

                    //Debug.WriteLine(dispNum);

                    //進捗表示
                    if (frame % 10 == 0)
                        this.Dispatcher.Invoke(() =>
                        {
                            Notice_TextBox.Text = ((float)frame / averageListCopy.Count * 100).ToString("f0") + "%";
                        });

                    if (frame % 1000 == 0)
                        GC.Collect();

                    using (var output_mat = basemat.Clone())
                    {
                        //アルファチャンネルをマスクとし、outputMatにコピーする
                        //メモリリーク原因となるのでExtractChannelもusing
                        using (var bodyMask = inputs_Mat_body[dispNum].ExtractChannel(3))
                        {
                            inputs_Mat_body[dispNum].CopyTo(output_mat, bodyMask);
                        }

                        //目パチ
                        if (eye_exist)
                            if (reserveFrame.Count > blinkframe_count)//範囲外にならないようにチェック
                            {
                                
                                int blinkNum = Math.Abs(reserveFrame[blinkframe_count] - frame);

                                if (blinkNum > inputs_Mat_eyes.Count - 1)
                                    blinkNum = inputs_Mat_eyes.Count - 1;

                                blinkNum = Math.Abs(blinkNum - inputs_Mat_eyes.Count) - 1;

                                if (reserveFrame[blinkframe_count] + inputs_Mat_eyes.Count == frame)
                                {
                                    blinkframe_count++;
                                }

                                //Debug.WriteLine(blinkNum);
                                
                                using (var eyeMask = inputs_Mat_eyes[blinkNum].ExtractChannel(3))
                                {
                                    inputs_Mat_eyes[blinkNum].CopyTo(output_mat, eyeMask);
                                }
                                

                            }
                            else
                            {
                                using (var eyeMask = inputs_Mat_eyes[0].ExtractChannel(3))
                                {
                                    inputs_Mat_eyes[0].CopyTo(output_mat, eyeMask);
                                }
                            }


                        //フレーム書き出し
                        //アルファチャンネルを含むとエラーになるので変換する
                        //vw.Write(output_mat.CvtColor(ColorConversionCodes.BGRA2BGR));
                        vw.Write(output_mat);
                    }
                }
            }


            //後処理
            foreach (Mat item in inputs_Mat_body)
            {
                item.Release();
                item.Dispose();
            }
            foreach (Mat item in inputs_Mat_eyes)
            {
                item.Release();
                item.Dispose();
            }

            GC.Collect();

            //出力パス
            if (!Directory.Exists(out_path))
                Directory.CreateDirectory(out_path);
            string output_path = out_path + Path.GetFileNameWithoutExtension(audio_path) + ".mp4";

            ReplaceAudio(temp_mov_path, audio_path, output_path);
            convert2Transparent(output_path);
        }

        private void ReplaceAudio(string input_VideoPath, string input_AudioPAth, string output_VideoPath)
        {
            try
            {
                FFMpeg.ReplaceAudio(input_VideoPath, input_AudioPAth, output_VideoPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                File.Delete(input_VideoPath);
            }
        }

        bool AlphaVideo = false;
        /// <summary>
        /// 透過動画に変換
        /// </summary>
        /// <param name="input_movie_path"></param>
        private void convert2Transparent(string input_movie_path)
        {
            if (AlphaVideo)
                try
                {
                    using (Process process = new Process())
                    {
                        string outPath = CurrentDir + @"\outputs\" + Path.GetFileNameWithoutExtension(input_movie_path) + ".mov";
                        process.StartInfo.FileName = ffmpegDir + "\\ffmpeg.exe";
                        string bgColor = config.background[0].ToString("x2") + config.background[1].ToString("x2") + config.background[2].ToString("x2");

                        //-y 上書き
                        process.StartInfo.Arguments = $@"-y -i {input_movie_path} -vf colorkey={bgColor}:{config.similarity}:{config.blend} -pix_fmt argb -c:v qtrle {outPath}";
                        process.Start();

                        // コマンド終了まで待機
                        process.WaitForExit();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.ToString());
                }
                finally
                {
                }
        }

        private async void Run()
        {
            Notice_TextBox.Text = "Run";
            Run_Button.IsEnabled = false;
            var selectedItems = Audio_listBox.SelectedItems?.ToList<Config.FileName>() ?? new();
            await Task.Run(() =>
            {
                bool done = true;
                try
                {
                    if (selectedItems.Count == 0)
                    {
                        MessageBox.Show("ファイルが不足しています。", "Error");
                        throw new Exception("Item == 0");
                    }
                    //スレッド数を8に制限
                    ParallelOptions option = new ParallelOptions();
                    option.MaxDegreeOfParallelism = 8;

                    Parallel.ForEach(selectedItems, option, p =>
                    {
                        newCreate(p.Path, fileCollection);
                    });
                }
                catch (Exception ex)
                {
                    done = false;
                    Debug.WriteLine($"{ex.Message}");
                    this.Dispatcher.Invoke(() =>
                    {
                        Notice_TextBox.Text = ex.ToString();
                    });
                }
                finally
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        Run_Button.IsEnabled = true;
                    });
                }

                if (done)
                    this.Dispatcher.Invoke(() =>
                    {
                        Notice_TextBox.Text = "DONE";
                    });

            });
        }

        private void Run_Button_Click(object sender, RoutedEventArgs e)
        {
            Run();
        }

        private void Delete_main_Button_Click(object sender, RoutedEventArgs e)
        {
            if (body_listBox.SelectedIndex >= 0)
                fileCollection.Body.RemoveAt(body_listBox.SelectedIndex);
            if (body_listBox.Items.Count == 0)
                BodyImage.Source = null;
        }

        private void Delete_eyes_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Eyes_listBox.SelectedIndex >= 0)
                fileCollection.Eyes.RemoveAt(Eyes_listBox.SelectedIndex);
            if (Eyes_listBox.Items.Count == 0)
                EyeImage.Source = null;
        }

        private void Delete_audio_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Audio_listBox.SelectedIndex >= 0)
                fileCollection.Audio.RemoveAt(Audio_listBox.SelectedIndex);
        }

        private void Load_preset_Button_Click(object sender, RoutedEventArgs e)
        {

            var dialog = new OpenFileDialog();
            dialog.Filter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";
            dialog.InitialDirectory = CurrentDir + "\\preset";

            var result = dialog.ShowDialog() ?? false;

            // 保存ボタン以外が押下された場合
            if (!result)
            {
                // 終了します。
                return;
            }
            string content = File.ReadAllText(dialog.FileName);

            fileCollection = new Config.FileCollection();
            bind();

            fileCollection = JsonUtil.JsonToPreset(content)!;
            bind();
        }

        /// <summary>
        /// コレクションをコントロールにバインド
        /// </summary>
        void bind()
        {
            body_listBox.ItemsSource = fileCollection.Body;
            Eyes_listBox.ItemsSource = fileCollection.Eyes;
            Audio_listBox.ItemsSource = fileCollection.Audio;

            if (body_listBox.Items.Count != 0)
                body_listBox.SelectedIndex = 0;

            if (Eyes_listBox.Items.Count != 0)
                Eyes_listBox.SelectedIndex = 0;

            if (Audio_listBox.Items.Count != 0)
                Audio_listBox.SelectedIndex = 0;
        }

        private void Save_preset_Button_Click(object sender, RoutedEventArgs e)
        {
            var str = JsonUtil.ToJson(fileCollection);

            var dialog = new SaveFileDialog();
            dialog.Filter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";
            dialog.InitialDirectory = CurrentDir + "\\preset";

            var result = dialog.ShowDialog() ?? false;

            // 保存ボタン以外が押下された場合
            if (!result)
            {
                // 終了します。
                return;
            }

            File.WriteAllText(dialog.FileName, str);
        }

        private void Outputs_dir_Button_Click(object sender, RoutedEventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", CurrentDir + "\\outputs");
        }

        WaveOutEvent outputDevice = new WaveOutEvent();
        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (fileCollection.Audio.Count == 0)
                return;

            if (outputDevice.PlaybackState == PlaybackState.Playing)
            {
                outputDevice.Stop();
                return;
            }


            var filename = fileCollection.Audio[Audio_listBox.SelectedIndex].Path;

            //オーディオ再生
            AudioFileReader afr = new AudioFileReader(filename);
            outputDevice.Init(afr);
            outputDevice.Play();
        }


        private void AlphaVideo_CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            AlphaVideo = false;
        }

        private void AlphaVideo_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            AlphaVideo = true;
        }

        private void LipSync_th_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LipSync_th_TextBlock.Text = LipSync_th_Slider.Value.ToString("f1");
            config.lipSync_threshold = (float)LipSync_th_Slider.Value;
        }
    }

    internal static class Extension
    {
        public static List<T> ToList<T>(this System.Collections.IList source)
        {
            return source?.Cast<T>().ToList() ?? new List<T>();
        }

        public static T[] ToArray<T>(this System.Collections.IList source)
        {
            return source?.Cast<T>().ToArray() ?? Array.Empty<T>();
        }
    }
}