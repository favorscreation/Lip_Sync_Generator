using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;

namespace Lip_Sync_Generator_2
{
    //https://qiita.com/iwasiman/items/57ed8a015859a88f3cb0
    internal class JsonUtil
    {

        /// <summary>
        /// 入力をJSON文字列に変換します。
        /// </summary>
        /// <param name="poco">定義済みのクラスオブジェクト</param>
        /// <returns>JSON文字列 (入力が異常な場合はnull)</returns>
        public static string? ToJson(Object poco)
        {
            try
            {
                var json = JsonSerializer.Serialize(poco, JsonUtil.GetOption());
                return json;
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        /// <summary>
        /// 入力のJSON文字列をクラスに変換します。
        /// </summary>
        /// <param name="json">JSON文字列</param>
        /// <returns>SampleUserPoco型の出力</returns>
        public static Config.Values? JsonToConfig(string json)
        {
            if (String.IsNullOrEmpty(json))
            {
                return null;
            }
            try
            {
                Config.Values? poco = JsonSerializer.Deserialize<Config.Values>(json, GetOption());
                return poco;
            }
            catch (JsonException e)
            {
                Debug.WriteLine(e.Message);
                return null;
            }
        }

        /// <summary>
        /// オプションを設定します。内部メソッドです。
        /// </summary>
        /// <returns>JsonSerializerOptions型のオプション</returns>
        private static JsonSerializerOptions GetOption()
        {
            // ユニコードのレンジ指定で日本語も正しく表示、インデントされるように指定
            var options = new JsonSerializerOptions
            {
                Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
                WriteIndented = true,
            };
            return options;
        }

    }
}
