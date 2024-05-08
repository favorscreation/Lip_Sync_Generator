using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Lip_Sync_Generator_2
{
    public class Config
    {
        public class Values
        {
            // 文字列型
            [JsonPropertyName("framerate")]
            public float framerate { get; set; }

            [JsonPropertyName("average samples")]
            public int average_samples { get; set; }

            [JsonPropertyName("sample scale")]
            public float sample_scale { get; set; }

            [JsonPropertyName("bigMouth threshold")]
            public float bigMouth_th { get; set; }

            [JsonPropertyName("smallMouth threshold")]
            public float smallMouth_th { get; set; }

            [JsonPropertyName("blink intervalFrame")]
            public int blink_intervalFrame { get; set; }

            [JsonPropertyName("blink intervalRandomFrame")]
            public int blink_interval_randomFrame { get; set; }
        }

        public class FileCollection
        {
            FileList audioFiles;
            FileList bodyFiles;
            FileList eyesFiles;

            public FileCollection() : base()
            {
                audioFiles = new FileList();
                bodyFiles = new FileList();
                eyesFiles = new FileList();
            }

            public FileList Audio
            {
                get { return audioFiles; }
                set { audioFiles = (FileList)value; }
            }

            public FileList Body
            {
                get { return bodyFiles; }
                set { bodyFiles = (FileList)value; }
            }

            public FileList Eyes
            {
                get { return eyesFiles; }
                set { eyesFiles = (FileList)value; }
            }
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
}
