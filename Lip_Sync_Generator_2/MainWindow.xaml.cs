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

namespace Lip_Sync_Generator_2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {



        public MainWindow()
        {
            InitializeComponent();

            //ffmpegのパスを通す
            GlobalFFOptions.Configure(options => options.BinaryFolder = "./ffmpeg");
        }

        public FileList fileList_main = new FileList();
        public FileList fileList_Eyes = new FileList();
        public FileList fileList_Audio = new FileList();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            main_listBox.ItemsSource = fileList_main;
            Eyes_listBox.ItemsSource = fileList_Eyes;
            Audio_listBox.ItemsSource = fileList_Audio;
        }

        private void UpItem(ListBox listBox, FileList list)
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

        private void DownItem(ListBox listBox, FileList list)
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
            UpItem(main_listBox, fileList_main);
        }
        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            DownItem(main_listBox, fileList_main);
        }

        private void UpButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            UpItem(Eyes_listBox, fileList_Eyes);
        }

        private void DownButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            DownItem(Eyes_listBox, fileList_Eyes);
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

                    var itemlist = (FileList)((ListBox)sender).ItemsSource;
                    itemlist.Add(new FileName(Path.GetFileName(name), name));
                }
                ((ListBox)sender).SelectedIndex = 0;
            }
        }

        //ファイルドロップ時のカーソル変更
        private void drop_box_DragOver(object sender, DragEventArgs e)
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

        private void main_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                MainImage.Source = new BitmapImage(new Uri(fileList_main[main_listBox.SelectedIndex].Path));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        private void Attach1_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                EyeImage.Source = new BitmapImage(new Uri(fileList_Eyes[Eyes_listBox.SelectedIndex].Path));
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
            }
        }

        /// <summary>
        /// 音声の音量を平均化したリスト
        /// </summary>
        List<float> averageList = new List<float>();

        /// <summary>
        /// 共通変数
        /// </summary>
        commonVariable variable = new commonVariable();


        /// <summary>
        /// 音声のボリュームを解析
        /// </summary>
        private void audio_Analyze()
        {
            averageList.Clear();

            //ファイルが選択されていない場合return
            if (Audio_listBox.SelectedIndex == -1)
                return;

            AudioFileReader audio_reader;
            try
            {
                audio_reader = new AudioFileReader(fileList_Audio[Audio_listBox.SelectedIndex].Path);
            }
            catch (Exception ex)
            {
                Notice_TextBox.Text = ex.Message;
                return;
            }

            float[] samples = new float[audio_reader.Length / audio_reader.BlockAlign * audio_reader.WaveFormat.Channels];
            audio_reader.Read(samples, 0, samples.Length);

            float time = (float)audio_reader.TotalTime.TotalSeconds;

            variable.average_samples = (int)(samples.Length / time / variable.video_framerate);


            //平均化処理
            for (int i = 0; i < samples.Length; i += variable.average_samples)
            {

                float sum = 0;
                for (int j = 0; j < variable.average_samples; j++)
                {
                    //samplesの範囲を超えないようにif
                    if (i + j >= samples.Length)
                        break;

                    //絶対値化
                    sum += Math.Abs(samples[i + j]);
                }
                averageList.Add(sum / (float)variable.average_samples);
            }

            audioChart.Series.Clear(); //描画領域のクリア
            audioChart.DisableAnimations = true;  //アニメーション禁止
            audioChart.DataTooltip = null;//ツールチップ無効
            audioChart.AxisY[0].MinValue = 0;//軸は0からスタート
            audioChart.AxisX[0].MinValue = 0;

            //データ作成
            double[] ys1 = Enumerable.Range(0, averageList.Count).Select(i => (double)averageList[i] * variable.sample_scale).ToArray();

            LineSeries lineSeries = new LineSeries();
            lineSeries.PointGeometry = null;
            lineSeries.Values = new ChartValues<double>(ys1);


            audioChart.Series.Add(lineSeries); //LiveChartsにシリーズを登録
        }

        private void Audio_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            audio_Analyze();
        }

        Random random = new Random();

        private void Create()
        {
            Notice_TextBox.Text = "Run";

            audio_Analyze();

            string bgm_path = "";
            string pic_path1 = "";
            string pic_path2 = "";
            string pic_path3 = "";
            string eye_path1 = "";
            string eye_path2 = "";
            string eye_path3 = "";
            bool eye_exist = true;
            try
            {
                bgm_path = fileList_Audio[Audio_listBox.SelectedIndex].Path;

                pic_path1 = fileList_main[0].Path;
                pic_path2 = fileList_main[1].Path;
                pic_path3 = fileList_main[1].Path;
                if (fileList_main.Count > 2)
                    pic_path3 = fileList_main[2].Path;

                if (fileList_Eyes.Count == 0)
                    eye_exist = false;
                else
                {
                    eye_path1 = fileList_Eyes[0].Path;
                    eye_path2 = fileList_Eyes[1].Path;
                    eye_path3 = fileList_Eyes[1].Path;
                    if (fileList_Eyes.Count > 2)
                        eye_path3 = fileList_Eyes[2].Path;
                }

                if (pic_path3 == "")
                {
                    pic_path3 = pic_path2;
                }
                if (eye_path3 == "")
                {
                    eye_path3 = eye_path2;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("ファイルが不足しています。追加してください。");
                Notice_TextBox.Text = ex.Message;
                return;
            }


            string temp_mov_path = @"temp.mp4";
            string out_path = @"outputs/";


            ///メイン画像
            //アルファチャンネル込みで読み込む
            Mat input_mat1 = Cv2.ImRead(pic_path1, ImreadModes.Unchanged);
            Mat input_mat2 = Cv2.ImRead(pic_path2, ImreadModes.Unchanged);
            Mat input_mat3 = Cv2.ImRead(pic_path3, ImreadModes.Unchanged);
            //透明ピクセルを置換
            Transparent_replacement(input_mat1);
            Transparent_replacement(input_mat2);
            Transparent_replacement(input_mat3);
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

            //透明色を黒に変更
            Transparent_replacement_ToBlack(input_eye1);
            Transparent_replacement_ToBlack(input_eye2);
            Transparent_replacement_ToBlack(input_eye3);
            //透明度削除
            input_eye1 = input_eye1.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye2 = input_eye2.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye3 = input_eye3.CvtColor(ColorConversionCodes.BGRA2BGR);


            VideoWriter vw = new VideoWriter(temp_mov_path, FourCC.MP4V, variable.video_framerate, size);


            //目のまばたき乱数を生成
            List<int> reserveFrame = new List<int>();
            for (int i = 0; i < averageList.Count; i++)
            {
                if (i % variable.blink_intervalFrame == 0 && i != 0)
                {
                    reserveFrame.Add(i - random.Next(0, variable.blink_interval_randomFrame));
                }
            }

            int count = 0;

            //背景色を青に設定 BGR
            using (var basemat = new Mat(size, MatType.CV_8UC3, new Scalar(255, 0, 0, 0)))
            {

                for (int frame = 0; frame < averageList.Count; frame++)
                {
                    Debug.WriteLine("frame:" + frame);


                    using (var output_mat = basemat.Clone())
                    {

                        float sens = averageList[frame] * variable.sample_scale;

                        if (sens >= variable.bigMouth_sens)
                        {
                            input_mat3.CopyTo(output_mat);   //大きい口
                        }
                        else if (sens > variable.smallMouth_sens && sens < variable.bigMouth_sens)
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

            FFMpeg.ReplaceAudio(temp_mov_path, bgm_path, out_path + Path.GetFileNameWithoutExtension(bgm_path) + ".mp4");
            File.Delete(temp_mov_path);
            Notice_TextBox.Text = "";
        }

        /// <summary>
        ///         //透明ピクセルを青に置換
        /// </summary>
        /// <param name="mat"></param>
        private void Transparent_replacement(Mat mat)
        {
            //ポインタによるアクセス
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0)
                        {
                            b[0] = 255; //B
                            b[1] = 0;   //G
                            b[2] = 0;   //R
                            b[3] = 255; //A
                        }
                        b = b + 4;
                    }
                }
            }
        }

        /// <summary>
        /// 透明ピクセルを黒に置換　マスク処理用
        /// </summary>
        /// <param name="mat"></param>
        private void Transparent_replacement_ToBlack(Mat mat)
        {
            //ポインタによるアクセス
            unsafe
            {
                byte* b = mat.DataPointer;
                for (int i = 0; i < mat.Height; i++)
                {
                    for (int j = 0; j < mat.Width; j++)
                    {
                        if (b[3] == 0)
                        {
                            b[0] = 0; //B
                            b[1] = 0; //G
                            b[2] = 0; //R
                            b[3] = 0; //A
                        }
                        b = b + 4;
                    }
                }
            }
        }

        private void Run_Button_Click(object sender, RoutedEventArgs e)
        {
            Create();
        }

        private void delete_main_Button_Click(object sender, RoutedEventArgs e)
        {
            if (main_listBox.SelectedIndex >= 0)
                fileList_main.RemoveAt(main_listBox.SelectedIndex);
        }

        private void delete_eyes_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Eyes_listBox.SelectedIndex >= 0)
                fileList_Eyes.RemoveAt(Eyes_listBox.SelectedIndex);
        }
    }

    public class commonVariable
    {
        public commonVariable() : base()
        {
            video_framerate = 24;
            average_samples = 1000;
            sample_scale = 100;
            bigMouth_sens = 4;
            smallMouth_sens = 1;
            blink_intervalFrame = 72;
            blink_interval_randomFrame = 24;
        }

        public float video_framerate { get; set; }
        //平均化するサンプル数
        public int average_samples { get; set; }
        //ボリューム倍率
        public float sample_scale { get; set; }
        //大きな口にするしきい値
        public float bigMouth_sens { get; set; }
        //小さい口にするしきい値
        public float smallMouth_sens { get; set; }

        public int blink_intervalFrame { get; set; }
        public int blink_interval_randomFrame { get; set; }
    }



    public class FileList : ObservableCollection<FileName>
    {
        public FileList() : base()
        {
            /*
            Add(new FileName("file1", "path1"));
            Add(new FileName("file2", "path2"));
            Add(new FileName("file3", "path3"));
            Add(new FileName("file4", "path4"));
            */
        }
    }

    public class FileName
    {
        private string fileName;
        private string filePath;

        public FileName(string name, string path)
        {
            this.fileName = name;
            this.filePath = path;
        }

        public string Name
        {
            get { return fileName; }
            set { fileName = value; }
        }

        public string Path
        {
            get { return filePath; }
            set { filePath = value; }
        }
    }
}