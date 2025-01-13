using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Lip_Sync_Generator_2
{
    public class ConfigManager
    {
        public static string CurrentDir { get; } = System.IO.Directory.GetCurrentDirectory();
        public Config.Values Config { get; set; }
        public Config.FileCollection FileCollection { get; set; } = new Config.FileCollection();
        // private string _ffmpegDir = CurrentDir + "\\ffmpeg"; // 不要になる
        private const string ConfigFilePath = @"config\config.json";
        private const string PresetFileFilter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";
        private const string PresetFileExtension = ".json";
        public ConfigManager()
        {
            // ffmpegのパスを通す処理は削除
            // FFMpegCore.GlobalFFOptions.Configure(options => options.BinaryFolder = _ffmpegDir);

            Encoding enc = Encoding.GetEncoding("utf-8");
            string str = "";

            if (File.Exists(ConfigFilePath))
                str = new StreamReader(ConfigFilePath, enc).ReadToEnd();

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
                Config = new Config.Values();
                Config.lipSync_threshold_percent = 40;  // スレッショルドの初期値を設定
                // Notice_TextBox.Text = "デフォルト設定が適用されました";
                Debug.WriteLine(JsonUtil.ToJson(Config));

                //設定ファイル作成
                using (StreamWriter writer = new StreamWriter(ConfigFilePath, false, enc))
                {
                    writer.WriteLine(JsonUtil.ToJson(Config));
                }
            }
            else
            {
                Config = JsonUtil.JsonToConfig(str) ?? new Config.Values();  // nullの場合は新しいConfig.Valuesを作成
                //Notice_TextBox.Text = "設定(外部ファイル)を読み込みました";
            }

            FileCollection = new Config.FileCollection(); // 設定ファイルがない場合は、空のコレクションで初期化
        }

        /// <summary>
        /// プリセットを読み込む
        /// </summary>
        public void LoadPreset(Config.FileCollection fileCollection)
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = PresetFileFilter;
            dialog.InitialDirectory = CurrentDir + "\\preset";

            var result = dialog.ShowDialog() ?? false;

            if (!result)
            {
                return;
            }

            try
            {
                string content = File.ReadAllText(dialog.FileName);
                var loadedCollection = JsonUtil.JsonToPreset(content) ?? new Config.FileCollection();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    FileCollection.Audio = loadedCollection.Audio;
                    FileCollection.Body = loadedCollection.Body;
                    FileCollection.Eyes = loadedCollection.Eyes;
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// プリセットを保存する
        /// </summary>
        public void SavePreset()
        {
            var str = JsonUtil.ToJson(FileCollection);

            var dialog = new SaveFileDialog();
            dialog.Filter = PresetFileFilter;
            dialog.InitialDirectory = CurrentDir + "\\preset";
            dialog.DefaultExt = PresetFileExtension;

            var result = dialog.ShowDialog() ?? false;

            // 保存ボタン以外が押下された場合
            if (!result)
            {
                // 終了します。
                return;
            }

            try
            {
                File.WriteAllText(dialog.FileName, str);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの保存に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// コンフィグを保存する
        /// </summary>
        public void SaveConfig()
        {
            string str = JsonUtil.ToJson(Config);
            Encoding enc = Encoding.GetEncoding("utf-8");
            try
            {
                using (StreamWriter writer = new StreamWriter(ConfigFilePath, false, enc))
                {
                    writer.WriteLine(str);
                }
                Debug.WriteLine("Config saved automatically.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Config save failed : {ex.Message}");
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            str = JsonUtil.ToJson(FileCollection);
            try
            {
                using (StreamWriter writer = new StreamWriter(@"preset\default.json", false, enc))
                {
                    writer.WriteLine(str);
                }
                Debug.WriteLine("FileCollection saved automatically.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"FileCollection save failed : {ex.Message}");
                MessageBox.Show($"設定の保存に失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        /// <summary>
        /// outputsフォルダを開く
        /// </summary>
        public void OpenOutputsDirectory()
        {
            System.Diagnostics.Process.Start("explorer.exe", CurrentDir + "\\outputs");
        }
    }
}