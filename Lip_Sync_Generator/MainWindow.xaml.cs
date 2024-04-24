using System.Collections.ObjectModel;
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
using NAudio.Wave;
using System.Diagnostics;
using NAudio.Dmo;
using OpenCvSharp;
using FFMpegCore;
using Window = System.Windows.Window;
using System;
using System.Windows.Media.Media3D;
using System.IO;
using Path = System.IO.Path;

namespace Lip_Sync_Generator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ObservableCollection<FileName> FileList;

        public class FileName
        {
            private string name;
            private string path;

            public FileName(string name, string path)
            {
                this.name = name;
                this.path = path;
            }

            public string Name
            {
                get { return name; }
                set { name = value; }
            }

            public string Path
            {
                get { return path; }
                set { path = value; }
            }
        }
        public struct Variable
        {
            public float video_framerate { get; set; }
            //平均化するサンプル数
            public int average_samples { get; set; }
            //ボリューム倍率
            public float sample_scale { get; set; }
            //大きな口にするしきい値
            public float bigMouth_sens { get; set; }
            //小さい口にするしきい値
            public float smallMouth_sens { get; set; }

            public string default_image1 { get; set; }
            public string default_image2 { get; set; }
            public string default_image3 { get; set; }
            public string default_image4 { get; set; }

            public int blink_intervalFrame {  get; set; }
            public int blink_interval_randomFrame {  get; set; }
        }

        Variable variable = new Variable();

        public MainWindow()
        {
            InitializeComponent();
            FileList = new ObservableCollection<FileName>();
            drop_box.ItemsSource = FileList;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {

            //Imageにドラッグアンドドロップするには画像を用意する必要がある。
            image1.Source = new RenderTargetBitmap(
                                        500,       // Imageの幅
                                        500,      // Imageの高さ
                                        96.0d,
                                        96.0d,
                                        PixelFormats.Pbgra32);
            image2.Source = image1.Source;
            image3.Source = image1.Source;
            eye1.Source = image1.Source;
            eye2.Source = image1.Source;
            eye3.Source = image1.Source;

            //ffmpegのパスを通す
            GlobalFFOptions.Configure(options => options.BinaryFolder = "./ffmpeg");

            variable.smallMouth_sens = 1;
            variable.bigMouth_sens = 4;
            variable.video_framerate = 24;
            variable.blink_intervalFrame = 72;
            variable.blink_interval_randomFrame = 24;


            if (File.Exists(variable.default_image1))
                image1.Source = new BitmapImage(new Uri(variable.default_image1, UriKind.Relative));
            if (File.Exists(variable.default_image2))
                image2.Source = new BitmapImage(new Uri(variable.default_image2, UriKind.Relative));
            if (File.Exists(variable.default_image3))
                image3.Source = new BitmapImage(new Uri(variable.default_image3, UriKind.Relative));
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
                    FileList.Add(new FileName(Path.GetFileName(name),name));

                }
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

        /// <summary>
        /// 音声の音量を平均化したリスト
        /// </summary>
        List<float> averageList = new List<float>();

        //ファイルリストを読み込む
        private void Analyze_Click(object sender, RoutedEventArgs e)
        {
            averageList.Clear();

            variable.sample_scale = float.Parse(sample_scale_TB.Text);

            text_box.Text = "";

            //ファイルが選択されていない場合return
            if (drop_box.SelectedIndex == -1)
                return;
            AudioFileReader audio_reader = new AudioFileReader(FileList[drop_box.SelectedIndex].Path);

            float[] samples = new float[audio_reader.Length / audio_reader.BlockAlign * audio_reader.WaveFormat.Channels];
            audio_reader.Read(samples, 0, samples.Length);

            Debug.WriteLine(samples.Length);

            float time = (float)audio_reader.TotalTime.TotalSeconds;

            variable.average_samples = (int)(samples.Length / time / variable.video_framerate);

            Debug.WriteLine(time);
            Debug.WriteLine(variable.average_samples);


            //平均化処理 絶対値化
            for (int i = 0; i < samples.Length; i += variable.average_samples)
            {

                float sum = 0;
                for (int j = 0; j < variable.average_samples; j++)
                {
                    //samplesの範囲を超えないようにif
                    if (i + j >= samples.Length)
                        break;

                    //絶対値化
                    if (samples[i + j] < 0)
                        sum += samples[j] * -1;
                    else
                        sum += samples[i + j];
                }
                averageList.Add(sum / (float)variable.average_samples);
            }

            int quantify;//intで整数化

            string waveform = "";
            //音声のボリュームをテキストで視覚化する
            for (int i = 0; i < averageList.Count; i++)
            {
                quantify = (int)(averageList[i] * variable.sample_scale);

                waveform += i.ToString().PadLeft(4, '0') + " : ";

                for (int j = 0; j < quantify; j++)
                {
                    waveform += "■";
                }
                waveform += quantify;
                waveform += "\n";
            }
            text_box.Text = waveform;
        }

        private void image_DragOver(object sender, DragEventArgs e)
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

        private void image_Drop(object sender, DragEventArgs e)
        {
            string[] path = (string[])e.Data.GetData(DataFormats.FileDrop);

            //ディレクトリならreturn
            if (Directory.Exists(path[0]))
            {

                string[] files = Directory.GetFiles(path[0], "*.png");
                foreach (string file in files) { Debug.WriteLine(file); };

                foreach (string file in files)
                {
                    if (Path.GetFileNameWithoutExtension(file) == "1")
                        image1.Source = new BitmapImage(new Uri(file));

                    if (Path.GetFileNameWithoutExtension(file) == "2")
                        image2.Source = new BitmapImage(new Uri(file));

                    if (Path.GetFileNameWithoutExtension(file) == "3")
                        image3.Source = new BitmapImage(new Uri(file));

                    if (Path.GetFileNameWithoutExtension(file) == "eye1")
                        eye1.Source = new BitmapImage(new Uri(file));

                    if (Path.GetFileNameWithoutExtension(file) == "eye2")
                        eye2.Source = new BitmapImage(new Uri(file));

                    if (Path.GetFileNameWithoutExtension(file) == "eye3")
                        eye3.Source = new BitmapImage(new Uri(file));
                };

            }

            //ファイルならSourceに設定
            //発火元のコントロールに設定
            if (File.Exists(path[0]))
                ((Image)sender).Source = new BitmapImage(new Uri(path[0]));
        }


        private void Run_Button_Click(object sender, RoutedEventArgs e)
        {
            infoLine.Text = "";
            try
            {
                Create();
            }
            catch (Exception ex)
            {
                infoLine.Text = ex.Message;
            }

        }


        Random random = new Random();

        private void Create()
        {
            variable.smallMouth_sens = float.Parse(downside_threshold_TB.Text);
            variable.bigMouth_sens = float.Parse(upside_threshold_TB.Text);
            variable.blink_intervalFrame = int.Parse(blink_interval_frame_TB.Text);
            variable.blink_interval_randomFrame = int.Parse(blink_interval_random_frame_TB.Text);

            string pic_path1 = image1.Source.ToString().Remove(0, 8);
            string pic_path2 = image2.Source.ToString().Remove(0, 8);
            string pic_path3 = image3.Source.ToString().Remove(0, 8);

            string eye_path1 = eye1.Source.ToString().Remove(0, 8);
            string eye_path2 = eye2.Source.ToString().Remove(0, 8);
            string eye_path3 = eye3.Source.ToString().Remove(0, 8);

            string temp_mov_path = @"temp.mp4";
            string out_path = @"output.mp4";

            //アルファチャンネル込みで読み込む
            Mat input_mat1 = Cv2.ImRead(pic_path1, ImreadModes.Unchanged);
            Mat input_mat2 = Cv2.ImRead(pic_path2, ImreadModes.Unchanged);
            Mat input_mat3 = Cv2.ImRead(pic_path3, ImreadModes.Unchanged);

            Mat input_eye1 = Cv2.ImRead(eye_path1, ImreadModes.Unchanged);
            Mat input_eye2 = Cv2.ImRead(eye_path2, ImreadModes.Unchanged);
            Mat input_eye3 = Cv2.ImRead(eye_path3, ImreadModes.Unchanged);


            //透明色を黒に変更
            Transparent_replacement_ToBlack(input_eye1);
            Transparent_replacement_ToBlack(input_eye2);
            Transparent_replacement_ToBlack(input_eye3);
            //透明度削除
            input_eye1 = input_eye1.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye2 = input_eye2.CvtColor(ColorConversionCodes.BGRA2BGR);
            input_eye3 = input_eye3.CvtColor(ColorConversionCodes.BGRA2BGR);



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
            VideoWriter vw = new VideoWriter(temp_mov_path, FourCC.MP4V, variable.video_framerate, size);


            //目のまばたき乱数を生成
            List<int> reserveFrame = new List<int>();
            for (int i = 0; i < averageList.Count; i++)
            {
                if (i % variable.blink_intervalFrame == 0 && i !=0)
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

            string bgm_path = FileList[drop_box.SelectedIndex].Path;
            FFMpeg.ReplaceAudio(temp_mov_path, bgm_path, out_path);
        }

        //ピクセル操作
        //透明ピクセルを青に置換
        private void Transparent_replacement(Mat mat)
        {
            //At、Setによる置換は遅い
            /*
            for (int i = 0; i < mat.Height; i++)
            {
                for (int j = 0; j < mat.Width; j++)
                {
                    Vec4b pix = mat.At<Vec4b>(i, j);
                    if (pix[3] == 0)
                    {
                        pix[0] = 255;
                        pix[1] = 0;
                        pix[2] = 0;
                        mat.Set(i, j, pix);
                    }
                }
            }
            */


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
    }
}