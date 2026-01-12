using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using Path = System.IO.Path;
using NAudio.Wave;
using LiveCharts;
using LiveCharts.Wpf;
using Microsoft.Win32;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Lip_Sync_Generator_2
{
    public partial class MainWindow : Window
    {
        private LipSyncProcessor _lipSyncProcessor;
        private ConfigManager _configManager;

        public MainWindow()
        {
            InitializeComponent();
            _configManager = new ConfigManager();
            this.DataContext = _configManager.Config;
            _lipSyncProcessor = new LipSyncProcessor(_configManager);
            BindData();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            BindData();
        }

        // --- Event Handlers for Buttons ---
        private void UpButton_Click(object sender, RoutedEventArgs e) => _lipSyncProcessor.UpItem(body_listBox, _configManager.FileCollection.Body);
        private void DownButton_Click(object sender, RoutedEventArgs e) => _lipSyncProcessor.DownItem(body_listBox, _configManager.FileCollection.Body);
        private void UpButton_Attach1_Click(object sender, RoutedEventArgs e) => _lipSyncProcessor.UpItem(Eyes_listBox, _configManager.FileCollection.Eyes);
        private void DownButton_Attach1_Click(object sender, RoutedEventArgs e) => _lipSyncProcessor.DownItem(Eyes_listBox, _configManager.FileCollection.Eyes);

        // --- Drag & Drop ---
        private void Drop_box_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = (sender is Border && e.Data.GetDataPresent(DataFormats.FileDrop)) ? DragDropEffects.All : DragDropEffects.None;
            e.Handled = true;
        }

        private void drop_box_Drop(object sender, DragEventArgs e)
        {
            if (sender == BodyDropBorder) _lipSyncProcessor.DropFile(body_listBox, e, _configManager.FileCollection);
            else if (sender == EyesDropBorder) _lipSyncProcessor.DropFile(Eyes_listBox, e, _configManager.FileCollection);
            else if (sender == AudioDropBorder) _lipSyncProcessor.DropFile(Audio_listBox, e, _configManager.FileCollection);
        }

        // --- Selection Changed ---
        private void Body_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try { if (body_listBox.SelectedItem is Config.FileName item) BodyImage.Source = new BitmapImage(new Uri(item.Path)); }
            catch (Exception ex) { Notice_TextBlock.Text = $"Body画像読み込みエラー: {ex.Message}"; }
        }

        private void Eye_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try { if (Eyes_listBox.SelectedItem is Config.FileName item) EyeImage.Source = new BitmapImage(new Uri(item.Path)); }
            catch (Exception ex) { Notice_TextBlock.Text = $"Eye画像読み込みエラー: {ex.Message}"; }
        }

        private void Audio_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Audio_listBox.SelectedItem is not Config.FileName item) return;
            try
            {
                var averageList = _lipSyncProcessor.AnalyzeAudio(item.Path);
                DrawingChart(averageList);
            }
            catch (Exception ex) { Notice_TextBlock.Text = $"音声解析エラー: {ex.Message}"; }
        }

        private void DrawingChart(List<float> averageList)
        {
            audioChart.Series.Clear();
            audioChart.DisableAnimations = true;
            audioChart.DataTooltip = null;
            audioChart.AxisY[0].MinValue = 0;
            audioChart.AxisX[0].MinValue = 0;

            double[] ys1 = Enumerable.Range(0, averageList.Count).Select(i => (double)averageList[i]).ToArray();
            LineSeries lineSeries = new LineSeries { PointGeometry = null, Values = new ChartValues<double>(ys1) };
            audioChart.Series.Add(lineSeries);
        }

        // --- Delete Buttons ---
        private void Delete_main_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(body_listBox, _configManager.FileCollection.Body);
            if (body_listBox.Items.Count == 0) BodyImage.Source = null;
        }
        private void Delete_eyes_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(Eyes_listBox, _configManager.FileCollection.Eyes);
            if (Eyes_listBox.Items.Count == 0) EyeImage.Source = null;
        }
        private void Delete_audio_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(Audio_listBox, _configManager.FileCollection.Audio);
        }

        // --- Menu & Misc ---
        private void Load_preset_Button_Click(object sender, RoutedEventArgs e) { _configManager.LoadPreset(_configManager.FileCollection); BindData(); }
        private void Save_preset_Button_Click(object sender, RoutedEventArgs e) => _configManager.SavePreset();
        private void Outputs_dir_Button_Click(object sender, RoutedEventArgs e) => _configManager.OpenOutputsDirectory();

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Audio_listBox.SelectedItem is Config.FileName item) _lipSyncProcessor.PlayAudio(item.Path);
            else Notice_TextBlock.Text = "オーディオファイルを選択してください。";
        }

        private void AlphaVideo_CheckBox_Unchecked(object sender, RoutedEventArgs e) => _lipSyncProcessor.AlphaVideo = false;
        private void AlphaVideo_CheckBox_Checked(object sender, RoutedEventArgs e) => _lipSyncProcessor.AlphaVideo = true;
        private void LipSync_th_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => LipSync_th_TextBlock.Text = LipSync_th_Slider.Value.ToString("f1");
        private void BlinkFrequency_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => BlinkFrequency_TextBlock.Text = BlinkFrequency_Slider.Value.ToString("f2");

        void BindData()
        {
            body_listBox.ItemsSource = _configManager.FileCollection.Body;
            Eyes_listBox.ItemsSource = _configManager.FileCollection.Eyes;
            Audio_listBox.ItemsSource = _configManager.FileCollection.Audio;
            if (body_listBox.Items.Count > 0) body_listBox.SelectedIndex = 0;
            if (Eyes_listBox.Items.Count > 0) Eyes_listBox.SelectedIndex = 0;
            if (Audio_listBox.Items.Count > 0) Audio_listBox.SelectedIndex = 0;
        }

        // --- 【重要】修正された実行ボタン処理 ---
        private async void Run_Button_Click(object sender, RoutedEventArgs e)
        {
            Notice_TextBlock.Text = "動画生成を開始します...";
            Run_Button.IsEnabled = false;

            var selectedAudioItems = Audio_listBox.SelectedItems?.Cast<Config.FileName>().ToList() ?? new();

            try
            {
                await Task.Run(() =>
                {
                    _lipSyncProcessor.Run(
                        selectedAudioItems,
                        _configManager.FileCollection,
                        // 進捗コールバック
                        (progress) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                Notice_TextBlock.Text = progress;
                            });
                        },
                        // エラーコールバック (ユーザー向け, 開発者向け)
                        (userMsg, devMsg) =>
                        {
                            this.Dispatcher.Invoke(() =>
                            {
                                // ユーザーにはダイアログで通知
                                MessageBox.Show(userMsg, "処理エラー", MessageBoxButton.OK, MessageBoxImage.Error);

                                // 開発者向けログをテキストボックスに追記
                                Notice_TextBlock.Text += $"\n\n=========================\n{devMsg}\n=========================\n";
                            });
                        }
                    );
                });

                this.Dispatcher.Invoke(() =>
                {
                    // エラーログが出ていなければ完了表示
                    if (!Notice_TextBlock.Text.Contains("ERROR"))
                    {
                        Notice_TextBlock.Text = "動画生成が完了しました。";
                    }
                    else
                    {
                        Notice_TextBlock.Text += "\n処理が終了しました（一部エラーあり）。";
                    }
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Critical Error: {ex.Message}");
                this.Dispatcher.Invoke(() =>
                {
                    Notice_TextBlock.Text = $"予期せぬエラー: {ex.Message}";
                    MessageBox.Show("予期せぬエラーが発生しました。", "Critical Error", MessageBoxButton.OK, MessageBoxImage.Stop);
                });
            }
            finally
            {
                this.Dispatcher.Invoke(() => Run_Button.IsEnabled = true);
            }
        }
    }
}