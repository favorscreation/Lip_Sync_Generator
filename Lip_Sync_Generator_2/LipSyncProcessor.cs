using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;
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
        private WaveOutEvent _outputDevice; // フィールドとして保持
        private VideoProcessor _videoProcessor; // VideoProcessor を利用するためのフィールド
        public LipSyncProcessor(ConfigManager configManager)
        {
            _configManager = configManager;
            _outputDevice = new WaveOutEvent(); // コンストラクタで初期化
            _videoProcessor = new VideoProcessor(configManager);  // VideoProcessorの初期化

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
        public void DropFile(ListBox listBox, System.Windows.Controls.Border border, DragEventArgs e, Config.FileCollection fileCollection)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var fileNames = (string[])e.Data.GetData(DataFormats.FileDrop);


                var itemlist = listBox.ItemsSource as Config.FileList;
                if (itemlist != null) // itemlist が null でないことを確認
                {
                    foreach (var name in fileNames)
                    {
                        itemlist.Add(new Config.FileName(Path.GetFileName(name), name));
                    }
                    listBox.SelectedIndex = 0;
                }
            }
        }
        /// <summary>
        /// 音声のボリュームを解析
        /// </summary>
        public List<float> AnalyzeAudio(string audio_path)
        {
            return _videoProcessor.AnalyzeAudio(audio_path);
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
                try
                {
                    _videoProcessor.ConvertToTransparentWithPipe(p.Path, fileCollection, progressCallback, AlphaVideo);
                }
                catch (Exception ex)
                {
                    // 必要に応じて、UIスレッドでエラーメッセージを表示する処理を追加
                    Debug.WriteLine(ex);
                }
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