﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace Lip_Sync_Generator_2
{
    public class Config
    {
        public class Values : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private float _framerate;
            [JsonPropertyName("framerate")]
            public float framerate
            {
                get { return _framerate; }
                set { _framerate = value; OnPropertyChanged(nameof(framerate)); }
            }

            private int _average_samples;
            [JsonPropertyName("average samples")]
            public int average_samples
            {
                get { return _average_samples; }
                set { _average_samples = value; OnPropertyChanged(nameof(average_samples)); }
            }

            private float _sample_scale;
            [JsonPropertyName("sample scale")]
            public float sample_scale
            {
                get { return _sample_scale; }
                set { _sample_scale = value; OnPropertyChanged(nameof(sample_scale)); }
            }

            private float _lipSync_threshold;
            [JsonPropertyName("lip sync threshold")]
            public float lipSync_threshold
            {
                get { return _lipSync_threshold; }
                set { _lipSync_threshold = value; OnPropertyChanged(nameof(lipSync_threshold)); }
            }

            private Color _background;
            [JsonPropertyName("BG_Color")]
            public Color background
            {
                get { return _background; }
                set { _background = value; OnPropertyChanged(nameof(background)); }
            }

            private float _similarity;
            [JsonPropertyName("similarity")]
            public float similarity
            {
                get { return _similarity; }
                set { _similarity = value; OnPropertyChanged(nameof(similarity)); }
            }


            private float _blend;
            [JsonPropertyName("blend")]
            public float blend
            {
                get { return _blend; }
                set { _blend = value; OnPropertyChanged(nameof(blend)); }
            }


            private float _blink_frequency;
            [JsonPropertyName("blink frequency")]
            public float blink_frequency
            {
                get { return _blink_frequency; }
                set { _blink_frequency = value; OnPropertyChanged(nameof(blink_frequency)); }
            }


            public Values()
            {
                framerate = 24;
                average_samples = 1000;
                sample_scale = 100;
                lipSync_threshold = 2;
                background = Colors.Blue;
                similarity = 0.2f;
                blend = 0.2f;
                blink_frequency = 0.1f;
            }
        }

        public class FileCollection : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }

            private FileList _audioFiles;
            private FileList _bodyFiles;
            private FileList _eyesFiles;

            public FileCollection()
            {
                _audioFiles = new FileList();
                _bodyFiles = new FileList();
                _eyesFiles = new FileList();
                _audioFiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Audio));
                _bodyFiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Body));
                _eyesFiles.CollectionChanged += (s, e) => OnPropertyChanged(nameof(Eyes));

            }
            [JsonPropertyName("Audio")]
            public FileList Audio
            {
                get { return _audioFiles; }
                set { _audioFiles = value; OnPropertyChanged(nameof(Audio)); }
            }
            [JsonPropertyName("Body")]
            public FileList Body
            {
                get { return _bodyFiles; }
                set { _bodyFiles = value; OnPropertyChanged(nameof(Body)); }
            }
            [JsonPropertyName("Eyes")]
            public FileList Eyes
            {
                get { return _eyesFiles; }
                set { _eyesFiles = value; OnPropertyChanged(nameof(Eyes)); }
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
            private string _fileName;
            private string _filePath;

            public FileName(string name, string path)
            {
                _fileName = name;
                _filePath = path;
            }
            [JsonPropertyName("Name")]
            public string Name
            {
                get { return _fileName; }

            }
            [JsonPropertyName("Path")]
            public string Path
            {
                get { return _filePath; }

            }
        }

    }
}