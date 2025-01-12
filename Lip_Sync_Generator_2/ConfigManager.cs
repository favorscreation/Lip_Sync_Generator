using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;
using System.Windows.Media;

namespace Lip_Sync_Generator_2
{
    public class ConfigManager
    {
        public static string CurrentDir { get; } = System.IO.Directory.GetCurrentDirectory();
        public Config.Values Config { get; set; }
        public Config.FileCollection FileCollection { get; set; } = new Config.FileCollection();
        private string _ffmpegDir = CurrentDir + "\\ffmpeg";
        private const string ConfigFilePath = @"config\config.json";
        private const string PresetFileFilter = "JSONファイル(*.json)|*.json|全てのファイル(*.*)|*.*";
        private const string PresetFileExtension = ".json";
        public ConfigManager()
        {

            //ffmpegのパスを通す
            FFMpegCore.GlobalFFOptions.Configure(options => options.BinaryFolder = _ffmpegDir);

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
                Config = JsonUtil.JsonToConfig(str)!;
                //Notice_TextBox.Text = "設定(外部ファイル)を読み込みました";
            }
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

            // 保存ボタン以外が押下された場合
            if (!result)
            {
                // 終了します。
                return;
            }

            try
            {
                string content = File.ReadAllText(dialog.FileName);

                FileCollection = JsonUtil.JsonToPreset(content) ?? new Config.FileCollection();
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
        /// outputsフォルダを開く
        /// </summary>
        public void OpenOutputsDirectory()
        {
            System.Diagnostics.Process.Start("explorer.exe", CurrentDir + "\\outputs");
        }
    }
}