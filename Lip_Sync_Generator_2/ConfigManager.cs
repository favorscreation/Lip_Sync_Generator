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
        private const string ConfigFilePath = @"config\config.json";
        private const string PresetFileFilter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";
        private const string PresetFileExtension = ".json";

        public ConfigManager()
        {
            Encoding enc = Encoding.GetEncoding("utf-8");
            string configStr = "";


            // configフォルダがなければ作成
            if (!Directory.Exists("config"))
                Directory.CreateDirectory("config");

            // presetフォルダがなければ作成
            if (!Directory.Exists("preset"))
                Directory.CreateDirectory("preset");

            // outputsフォルダがなければ作成
            if (!Directory.Exists("outputs"))
                Directory.CreateDirectory("outputs");

            // 設定ファイル読み込み
            if (File.Exists(ConfigFilePath))
                configStr = new StreamReader(ConfigFilePath, enc).ReadToEnd();

            if (JsonUtil.JsonToConfig(configStr) == null)
            {
                Config = new Config.Values();
                Config.lipSync_threshold_percent = 40;  // スレッショルドの初期値を設定
                Debug.WriteLine("Default config applied");

                // 設定ファイル作成
                using (StreamWriter writer = new StreamWriter(ConfigFilePath, false, enc))
                {
                    writer.WriteLine(JsonUtil.ToJson(Config));
                }
            }
            else
            {
                Config = JsonUtil.JsonToConfig(configStr) ?? new Config.Values();  // nullの場合は新しいConfig.Valuesを作成
                Debug.WriteLine("Config loaded from file");
            }
        }

        /// <summary>
        /// プリセットを読み込む
        /// </summary>
        public void LoadPreset(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);
                var loadedCollection = JsonUtil.JsonToPreset(content) ?? new Config.FileCollection();

                Application.Current.Dispatcher.Invoke(() =>
                {
                    FileCollection.Audio = loadedCollection.Audio;
                    FileCollection.Body = loadedCollection.Body;
                    FileCollection.Eyes = loadedCollection.Eyes;
                });
                Config.lastPresetPath = filePath;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"プリセットの読み込みに失敗しました: {ex.Message}", "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// プリセットを読み込む
        /// </summary>
        public void LoadPreset()
        {
            var dialog = new OpenFileDialog();
            dialog.Filter = PresetFileFilter;
            dialog.InitialDirectory = CurrentDir + "\\preset";

            var result = dialog.ShowDialog() ?? false;

            if (!result)
            {
                return;
            }
            LoadPreset(dialog.FileName);
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
                Config.lastPresetPath = dialog.FileName;
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