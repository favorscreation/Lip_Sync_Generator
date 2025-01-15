using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiveCharts;
using LiveCharts.Wpf;

namespace Lip_Sync_Generator_2
{
    public partial class MainWindow : Window
    {
        private LipSyncProcessor _lipSyncProcessor;
        private ConfigManager _configManager;
        private List<float> _currentAudioAnalysis;


        public MainWindow()
        {
            InitializeComponent();
            Debug.WriteLine("InitializeComponent() called");
            this.Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // ConfigManager の初期化
                _configManager = new ConfigManager();
                Debug.WriteLine("_configManager initialized");

                // UIへのバインド (ConfigManager初期化後)
                this.DataContext = _configManager.Config;
                body_listBox.ItemsSource = _configManager.FileCollection.Body;
                Eyes_listBox.ItemsSource = _configManager.FileCollection.Eyes;
                Audio_listBox.ItemsSource = _configManager.FileCollection.Audio;
                Debug.WriteLine("UI bindings initialized");

                // LipSyncProcessor の初期化
                _lipSyncProcessor = new LipSyncProcessor(_configManager);
                Debug.WriteLine("_lipSyncProcessor initialized");

                //UI要素の初期化確認
                if (LipSync_max_sensitivity_Slider == null)
                    Debug.WriteLine("LipSync_max_sensitivity_Slider is null in MainWindow_Loaded");
                else
                    Debug.WriteLine("LipSync_max_sensitivity_Slider is not null in MainWindow_Loaded");

                if (LipSync_max_sensitivity_TextBlock == null)
                    Debug.WriteLine("LipSync_max_sensitivity_TextBlock is null in MainWindow_Loaded");
                else
                    Debug.WriteLine("LipSync_max_sensitivity_TextBlock is not null in MainWindow_Loaded");

                // 初期選択 (ConfigManager初期化後)
                SetInitialSelection();
                Debug.WriteLine("Initial Selection set");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during MainWindow_Loaded: {ex}");
                MessageBox.Show($"初期化中にエラーが発生しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetInitialSelection()
        {
            if (body_listBox.Items.Count != 0)
            {
                body_listBox.SelectedIndex = 0;
            }
            if (Eyes_listBox.Items.Count != 0)
            {
                Eyes_listBox.SelectedIndex = 0;
            }
            if (Audio_listBox.Items.Count != 0)
            {
                Audio_listBox.SelectedIndex = 0;
            }
        }
        // UIイベントハンドラ
        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.UpItem(body_listBox, _configManager.FileCollection.Body);
        }
        private void DownButton_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DownItem(body_listBox, _configManager.FileCollection.Body);
        }

        private void UpButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.UpItem(Eyes_listBox, _configManager.FileCollection.Eyes);
        }

        private void DownButton_Attach1_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DownItem(Eyes_listBox, _configManager.FileCollection.Eyes);
        }

        private void Drop_box_DragOver(object sender, DragEventArgs e)
        {
            if (sender is Border && e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.All;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void drop_box_Drop(object sender, DragEventArgs e)
        {
            if (sender == BodyDropBorder)
            {
                _lipSyncProcessor.DropFile(body_listBox, BodyDropBorder, e, _configManager.FileCollection);
            }
            else if (sender == EyesDropBorder)
            {
                _lipSyncProcessor.DropFile(Eyes_listBox, EyesDropBorder, e, _configManager.FileCollection);
            }
            else if (sender == AudioDropBorder)
            {
                _lipSyncProcessor.DropFile(Audio_listBox, AudioDropBorder, e, _configManager.FileCollection);
            }
        }

        private void Body_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (body_listBox.SelectedItem is Config.FileName selectedItem)
                {
                    BodyImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(selectedItem.Path));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading body image: {ex.Message}");
                Notice_TextBlock.Text = $"Error loading body image: {ex.Message}";
            }
        }

        private void Eye_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (Eyes_listBox.SelectedItem is Config.FileName selectedItem)
                {
                    EyeImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(selectedItem.Path));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading eye image: {ex.Message}");
                Notice_TextBlock.Text = $"Error loading eye image: {ex.Message}";
            }
        }

        private void Audio_listBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Audio_listBox.SelectedItem is not Config.FileName selectedItem)
            {
                return;
            }
            try
            {
                // UIスレッドを占有しないようにTask.Run内で処理を実行
                Task.Run(() =>
                {
                    _currentAudioAnalysis = _lipSyncProcessor.AnalyzeAudio(selectedItem.Path);
                    this.Dispatcher.Invoke(() =>
                    {
                        DrawingChart(_currentAudioAnalysis);
                    });
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error analyzing audio: {ex.Message}");
                Notice_TextBlock.Text = $"Error analyzing audio: {ex.Message}. Please check the selected audio file.";
            }
        }


        /// <summary>
        ///オーディオ波形描画
        /// </summary>
        private void DrawingChart(List<float> averageList)
        {
            if (audioChart == null)
            {
                Debug.WriteLine("audioChart is null");
                return;
            }

            if (averageList == null)
            {
                audioChart.Series.Clear();
                Debug.WriteLine("averageList is null");
                return;
            }
            if (averageList.Count == 0)
            {
                audioChart.Series.Clear();
                audioChart.Series.Add(new LineSeries { Values = new ChartValues<double> { 0 } });
                Debug.WriteLine("averageList is empty");
                return;
            }


            audioChart.Series.Clear();
            audioChart.DisableAnimations = true;
            audioChart.DataTooltip = null;
            audioChart.AxisY[0].MinValue = 0;
            audioChart.AxisX[0].MinValue = 0;


            double[] ys1 = Enumerable.Range(0, averageList.Count).Select(i => (double)averageList[i]).ToArray();

            LineSeries lineSeries = new LineSeries();
            lineSeries.PointGeometry = null;
            lineSeries.Values = new ChartValues<double>(ys1);

            audioChart.Series.Add(lineSeries);

            // スレッショルド線の設定
            double threshold = _configManager.Config.lipSync_threshold;
            LineSeries thresholdLine = new LineSeries
            {
                Values = new ChartValues<double>(Enumerable.Repeat(threshold, averageList.Count)),
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                PointGeometry = null,
                LineSmoothness = 0,
            };
            audioChart.Series.Add(thresholdLine);


            // 最大音量線の設定
            double maxVolume = 0;
            if (averageList.Any())
                maxVolume = averageList.Max() * Math.Max(_configManager.Config.lipSync_max_sensitivity, 0.0001);

            LineSeries maxVolumeLine = new LineSeries
            {
                Values = new ChartValues<double>(Enumerable.Repeat(maxVolume, averageList.Count)),
                Stroke = Brushes.Green,
                StrokeThickness = 2,
                PointGeometry = null,
                LineSmoothness = 0,
            };
            audioChart.Series.Add(maxVolumeLine);
        }

        private async void LipSync_th_Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            LipSync_th_TextBlock.Text = LipSync_th_Slider.Value.ToString("f1") + "%";
            _configManager.Config.lipSync_threshold = _configManager.Config.lipSync_threshold_percent / 100 * _configManager.Config.lipSync_threshold_max;

            if (_currentAudioAnalysis != null && Audio_listBox.SelectedItem is Config.FileName selectedItem)
            {
                await Task.Run(() =>
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        DrawingChart(_currentAudioAnalysis);
                    });
                });
            }
        }

        private void LipSync_max_sensitivity_Slider_ValueChanged(object sender, RoutedEventArgs e)
        {
            if (LipSync_max_sensitivity_TextBlock == null)
            {
                Debug.WriteLine("LipSync_max_sensitivity_TextBlock is null in ValueChanged");
                return;
            }
            LipSync_max_sensitivity_TextBlock.Text = LipSync_max_sensitivity_Slider.Value.ToString("f1");
            if (_currentAudioAnalysis != null)
            {
                DrawingChart(_currentAudioAnalysis);
            }
        }

        private void Delete_main_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(body_listBox, _configManager.FileCollection.Body);
            if (body_listBox.Items.Count == 0)
                BodyImage.Source = null;
        }

        private void Delete_eyes_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(Eyes_listBox, _configManager.FileCollection.Eyes);
            if (Eyes_listBox.Items.Count == 0)
                EyeImage.Source = null;
        }

        private void Delete_audio_Button_Click(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.DeleteItem(Audio_listBox, _configManager.FileCollection.Audio);
        }

        private void Load_preset_Button_Click(object sender, RoutedEventArgs e)
        {
            _configManager.LoadPreset();
            // ItemsSource を設定し直す
            body_listBox.ItemsSource = _configManager.FileCollection.Body;
            Eyes_listBox.ItemsSource = _configManager.FileCollection.Eyes;
            Audio_listBox.ItemsSource = _configManager.FileCollection.Audio;
            SetInitialSelection();
        }

        private void Save_preset_Button_Click(object sender, RoutedEventArgs e)
        {
            _configManager.SavePreset();
        }

        private void Outputs_dir_Button_Click(object sender, RoutedEventArgs e)
        {
            _configManager.OpenOutputsDirectory();
        }

        private void Play_Button_Click(object sender, RoutedEventArgs e)
        {
            if (Audio_listBox.SelectedItem is Config.FileName selectedItem)
            {
                _lipSyncProcessor.PlayAudio(selectedItem.Path);
            }
            else
            {
                Notice_TextBlock.Text = "オーディオファイルを選択してください。";
            }
        }

        private void AlphaVideo_CheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.AlphaVideo = false;
        }

        private void AlphaVideo_CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            _lipSyncProcessor.AlphaVideo = true;
        }
        private void BlinkFrequency_Slider_ValueChanged(object sender, RoutedEventArgs e)
        {
            BlinkFrequency_TextBlock.Text = BlinkFrequency_Slider.Value.ToString("f2");
        }
        private async void Run_Button_Click(object sender, RoutedEventArgs e)
        {
            Notice_TextBlock.Text = "動画生成を開始します...";
            Run_Button.IsEnabled = false;
            var selectedAudioItems = Audio_listBox.SelectedItems?.Cast<Config.FileName>().ToList() ?? new();

            try
            {
                // UIスレッドを占有しないようにTask.Run内で処理を実行
                await Task.Run(() =>
                {
                    _lipSyncProcessor.Run(selectedAudioItems, _configManager.FileCollection, (progress) =>
                    {
                        // UIスレッドに結果を反映
                        this.Dispatcher.Invoke(() =>
                        {
                            Notice_TextBlock.Text = progress;
                        });
                    });
                });

                this.Dispatcher.Invoke(() =>
                {
                    Notice_TextBlock.Text = "動画生成が完了しました。";
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during video generation: {ex.Message}");
                this.Dispatcher.Invoke(() =>
                {
                    Notice_TextBlock.Text = $"動画生成中にエラーが発生しました: {ex.Message}";
                });
            }
            finally
            {
                this.Dispatcher.Invoke(() =>
                {
                    Run_Button.IsEnabled = true;
                });
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            base.OnClosing(e);
            // アプリ終了時に設定を保存
            _configManager.SaveConfig();
        }
    }
}