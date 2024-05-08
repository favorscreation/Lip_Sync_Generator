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

namespace Lip_Sync_Generator_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Config.Values config = new Config.Values();
        public Config.FileCollection fileCollection = new Config.FileCollection();

        public MainWindow()
        {
            InitializeComponent();

            //ffmpegのパスを通す
            GlobalFFOptions.Configure(options => options.BinaryFolder = "./ffmpeg");

            Encoding enc = Encoding.GetEncoding("utf-8");

            config = new Config.Values();

            string config_path = @"config\config.json";
            string str = "";

            if (File.Exists(@"config\config.json"))
                str = new StreamReader(config_path, enc).ReadToEnd();

            //configフォルダがなければ作成
            if (Directory.Exists("config") == false)
                Directory.CreateDirectory("config");

            if (JsonUtil.JsonToConfig(str) == null)
            {
                config = new Config.Values
                {
                    framerate = 24,
                    average_samples = 1000,
                    sample_scale = 100,
                    smallMouth_th = 1,
                    bigMouth_th = 4,
                    blink_intervalFrame = 72,
                    blink_interval_randomFrame = 24
                };
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
            TextBox4.Text = "smallMouth th = " + config!.smallMouth_th;
            TextBox5.Text = "bigMouth th = " + config!.bigMouth_th;
            TextBox6.Text = "blink interval Frame = " + config!.blink_intervalFrame;
            TextBox7.Text = "blink interval RandomFrame = " + config!.blink_interval_randomFrame;
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
                    Debug.WriteLine(name);

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

        private void Main_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
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
                averageList.Add(sum / (float)config.average_samples);
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
            double[] ys1 = Enumerable.Range(0, CurrentAverageList.Count).Select(i => (double)CurrentAverageList[i] * config!.sample_scale).ToArray();

            LineSeries lineSeries = new LineSeries();
            lineSeries.PointGeometry = null;
            lineSeries.Values = new ChartValues<double>(ys1);


            audioChart.Series.Add(lineSeries); //シリーズを登録
        }

        private void Audio_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (fileCollection.Audio.Count == 0)
                return;

            CurrentAverageList = audio_Analyze(fileCollection.Audio[Audio_listBox.SelectedIndex].Path);
            DrawingChart();
        }

        Random random = new Random();

        /// <summary>
        /// 仮の関数、将来的に4枚以上に対応
        /// </summary>
        /// <param name="audio_path"></param>
        private void Create(string audio_path)
        {
            List<float> averageListCopy = audio_Analyze(audio_path);          

            string pic_path1 = "";
            string pic_path2 = "";
            string pic_path3 = "";
            string eye_path1 = "";
            string eye_path2 = "";
            string eye_path3 = "";
            bool eye_exist = true;

            pic_path1 = fileCollection.Body[0].Path;
            pic_path2 = fileCollection.Body[1].Path;
            pic_path3 = fileCollection.Body[1].Path;
            if (fileCollection.Body.Count > 2)
                pic_path3 = fileCollection.Body[2].Path;

            if (fileCollection.Eyes.Count == 0)
                eye_exist = false;
            else
            {
                eye_path1 = fileCollection.Eyes[0].Path;
                eye_path2 = fileCollection.Eyes[1].Path;
                eye_path3 = fileCollection.Eyes[1].Path;
                if (fileCollection.Eyes.Count > 2)
                    eye_path3 = fileCollection.Eyes[2].Path;
            }

            if (pic_path3 == "")
            {
                pic_path3 = pic_path2;
            }
            if (eye_path3 == "")
            {
                eye_path3 = eye_path2;
            }


            //tempファイル名を生成
            Guid g = System.Guid.NewGuid();
            string guid = g.ToString("N").Substring(0, 8);
            string temp_mov_path =　"temp_" + guid + ".mp4";

            string out_path = @"outputs/";


            ///メイン画像
            //アルファチャンネル込みで読み込む
            Mat input_mat1 = Cv2.ImRead(pic_path1, ImreadModes.Unchanged);
            Mat input_mat2 = Cv2.ImRead(pic_path2, ImreadModes.Unchanged);
            Mat input_mat3 = Cv2.ImRead(pic_path3, ImreadModes.Unchanged);
            //透明ピクセルを置換
            MatFunction.Transparent_replacement(input_mat1);
            MatFunction.Transparent_replacement(input_mat2);
            MatFunction.Transparent_replacement(input_mat3);
            //透明度削除
            input_mat1 = input_mat1.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_mat2 = input_mat2.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_mat3 = input_mat3.CvtColor(ColorConversionCodes.BGRA2BGR);


            //サイズが異なると合成できないため揃える
            int hight = input_mat1.Height;
            int width = input_mat1.Width;
            OpenCvSharp.Size size = new OpenCvSharp.Size(width, hight);


            ///目の画像
            //空の画像を生成
            Mat input_eye1 = new Mat(size, MatType.CV_8UC4);
            Mat input_eye2 = new Mat(size, MatType.CV_8UC4);
            Mat input_eye3 = new Mat(size, MatType.CV_8UC4);

            //目のファイルが存在するなら
            if (eye_exist)
            {
                input_eye1 = Cv2.ImRead(eye_path1, ImreadModes.Unchanged);
                input_eye2 = Cv2.ImRead(eye_path2, ImreadModes.Unchanged);
                input_eye3 = Cv2.ImRead(eye_path3, ImreadModes.Unchanged);
            }

            //リサイズ
            input_mat1 = input_mat1.Resize(size);
            input_mat2 = input_mat2.Resize(size);
            input_mat3 = input_mat3.Resize(size);
            input_eye1 = input_eye1.Resize(size);
            input_eye2 = input_eye2.Resize(size);
            input_eye3 = input_eye3.Resize(size);

            //透明色を黒に変更
            MatFunction.Transparent_replacement_ToBlack(input_eye1);
            MatFunction.Transparent_replacement_ToBlack(input_eye2);
            MatFunction.Transparent_replacement_ToBlack(input_eye3);
            //透明度削除
            input_eye1 = input_eye1.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye2 = input_eye2.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye3 = input_eye3.CvtColor(ColorConversionCodes.BGRA2BGR);


            VideoWriter vw = new VideoWriter(temp_mov_path, FourCC.MP4V, config!.framerate, size);


            //目のまばたき乱数を生成
            List<int> reserveFrame = new List<int>();
            for (int i = 0; i < averageListCopy.Count; i++)
            {
                if (i % config.blink_intervalFrame == 0 && i != 0)
                {
                    reserveFrame.Add(i - random.Next(0, config.blink_interval_randomFrame));
                }
            }

            int count = 0;

            //背景色を青に設定 BGR
            using (var basemat = new Mat(size, MatType.CV_8UC3, new Scalar(255, 0, 0, 0)))
            {

                for (int frame = 0; frame < averageListCopy.Count; frame++)
                {
                    //Debug.WriteLine("frame:" + frame);

                    if (frame % 10 == 0)
                        this.Dispatcher.Invoke(() =>
                        {
                            Notice_TextBox.Text = ((float)frame / averageListCopy.Count * 100).ToString("f0") + "%";
                        });


                    using (var output_mat = basemat.Clone())
                    {

                        float sens = averageListCopy[frame] * config.sample_scale;

                        if (sens >= config.bigMouth_th)
                        {
                            input_mat3.CopyTo(output_mat);   //大きい口
                        }
                        else if (sens > config.smallMouth_th && sens < config.bigMouth_th)
                        {
                            input_mat2.CopyTo(output_mat);  //中くらいの口
                        }
                        else
                        {
                            input_mat1.CopyTo(output_mat);   //閉じた口
                        }

                        //目を合成
                        if (count < reserveFrame.Count) //配列の範囲外にならないようチェック
                        {

                            if (frame == reserveFrame[count] + 1)
                                input_eye2.CopyTo(output_mat, input_eye2);

                            else if (frame == reserveFrame[count] + 2)
                                input_eye3.CopyTo(output_mat, input_eye3);

                            else if (frame == reserveFrame[count] + 3)
                            {
                                input_eye2.CopyTo(output_mat, input_eye2);
                                count++;
                            }
                            else
                                input_eye1.CopyTo(output_mat, input_eye1);
                        }
                        else { input_eye1.CopyTo(output_mat, input_eye1); }


                        //フレーム書き出し
                        //アルファチャンネルを含むとエラーになる
                        //vw.Write(output_mat.CvtColor(ColorConversionCodes.BGRA2BGR));
                        vw.Write(output_mat);
                    }
                }
            }


            vw.Release();
            vw.Dispose();

            input_mat1.Dispose();
            input_mat2.Dispose();
            input_mat3.Dispose();
            input_eye1.Dispose();
            input_eye2.Dispose();
            input_eye3.Dispose();

            if (!Directory.Exists(out_path))
                Directory.CreateDirectory(out_path);

            try
            {
                FFMpeg.ReplaceAudio(temp_mov_path, audio_path, out_path + Path.GetFileNameWithoutExtension(audio_path) + ".mp4");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                File.Delete(temp_mov_path);
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
                        Debug.WriteLine(p.Path);
                        Create(p.Path);
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

        private void Load_preset_Button_Click(object sender, RoutedEventArgs e)
        {

            var dialog = new OpenFileDialog();
            dialog.Filter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";

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
            //presetフォルダがなければ作成
            if (Directory.Exists("preset") == false)
                Directory.CreateDirectory("preset");

            var str = JsonUtil.ToJson(fileCollection);

            var dialog = new SaveFileDialog();
            dialog.Filter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";

            var result = dialog.ShowDialog() ?? false;

            // 保存ボタン以外が押下された場合
            if (!result)
            {
                // 終了します。
                return;
            }

            File.WriteAllText(dialog.FileName, str);
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