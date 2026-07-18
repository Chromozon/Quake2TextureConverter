using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;

namespace Quake2TextureConverter
{
    public class MainWindowViewModel : BindableBase
    {
        public DelegateCommand SelectColormapCommand =>
            _selectColormapCommand ??= new DelegateCommand(SelectColormapCommandExecute);

        public DelegateCommand SelectTextureFolderCommand =>
            _selectTextureFolderCommand ??= new DelegateCommand(SelectTextureFolderCommandExecute);

        public DelegateCommand SelectOutputFolderCommand =>
            _selectOutputFolderCommand ??= new DelegateCommand(SelectOutputFolderCommandExecute);

        public DelegateCommand ListTexturesCommand =>
            _listTexturesCommand ??= new DelegateCommand(ListTexturesCommandExecute);

        public DelegateCommand ConvertTexturesCommand =>
            _convertTexturesCommand ??= new DelegateCommand(ConvertTexturesCommandExecute);

        public DelegateCommand ClearLogCommand =>
            _clearLogCommand ??= new DelegateCommand(ClearLogCommandExecute);

        public DelegateCommand RandomStyleColorsCommand =>
            _randomStyleColorsCommand ??= new DelegateCommand(RandomStyleColorsCommandExecute);

        public System.Windows.Media.Color StyleColor1
        {
            get => _styleColor1;
            set => SetProperty(ref _styleColor1, value);
        }

        public System.Windows.Media.Color StyleColor2
        {
            get => _styleColor2;
            set => SetProperty(ref _styleColor2, value);
        }

        public string ColormapPath
        {
            get => _colormapPath;
            set => SetProperty(ref _colormapPath, value);
        }

        public string TextureFolder
        {
            get => _textureFolder;
            set => SetProperty(ref _textureFolder, value);
        }

        public string OutputFolder
        {
            get => _outputFolder;
            set => SetProperty(ref _outputFolder, value);
        }

        public bool IsPngChecked
        {
            get => _isPngChecked;
            set => SetProperty(ref _isPngChecked, value);
        }

        public bool IsReplaceTransparentColorChecked
        {
            get => _isReplaceTransparentColorChecked;
            set => SetProperty(ref _isReplaceTransparentColorChecked, value);
        }

        public bool IsJpgChecked
        {
            get => _isJpgChecked;
            set => SetProperty(ref _isJpgChecked, value);
        }

        public string JpgQuality
        {
            get => _jpgQuality;
            set => SetProperty(ref _jpgQuality, value);
        }

        public bool IsMaintainFolderStructureChecked
        {
            get => _isMaintainFolderStructureChecked;
            set => SetProperty(ref _isMaintainFolderStructureChecked, value);
        }

        public bool IsFlattenChecked
        {
            get => _isFlattenChecked;
            set => SetProperty(ref _isFlattenChecked, value);
        }

        public string LogMessages
        {
            get => _logMessages;
            set => SetProperty(ref _logMessages, value);
        }

        public bool IsCommandInProgress
        {
            get => _isCommandInProgress;
            set => SetProperty(ref _isCommandInProgress, value);
        }

        public MainWindowViewModel(IEventAggregator eventAggregator)
        {
            _eventAggregator = eventAggregator;
            LogInfo("Welcome!  This app will convert .wal files to the desired modern formats.");
        }

        private void SelectColormapCommandExecute()
        {
            var dialog = new OpenFileDialog
            {
                FileName = "colormap.pcx",
                Title = "Open Colormap File",
                Filter = "PCX Files (*.pcx)|*.pcx"
            };
            if (dialog.ShowDialog() == true)
            {
                ColormapPath = dialog.FileName;
            }
        }

        private void SelectTextureFolderCommandExecute()
        {
            var dialog = new OpenFolderDialog
            {
                Multiselect = false,
                Title = "Select texture folder"
            };
            if (dialog.ShowDialog() == true)
            {
                TextureFolder = dialog.FolderName;
            }
        }

        private void SelectOutputFolderCommandExecute()
        {
            var dialog = new OpenFolderDialog
            {
                Multiselect = false,
                Title = "Select output folder"
            };
            if (dialog.ShowDialog() == true)
            {
                OutputFolder = dialog.FolderName;
            }
        }

        private async void ListTexturesCommandExecute()
        {
            try
            {
                IsCommandInProgress = true;
                await ListTexturesTask();
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
            finally
            {
                IsCommandInProgress = false;
            }
        }

        private async Task ListTexturesTask()
        {
            if (!Directory.Exists(TextureFolder))
            {
                LogError($"Texture folder '{TextureFolder}' does not exist");
                return;
            }
            LogInfo("Searching for wal files...");
            List<ConvertData> allWals = null;
            await Task.Run(() =>
            {
                allWals = ListAllWals(TextureFolder, OutputFolder, IsFlattenChecked);
            });
            LogInfo($"Found {allWals.Count} wal files to convert");
        }

        private async void ConvertTexturesCommandExecute()
        {
            try
            {
                IsCommandInProgress = true;
                await ConvertTexturesTask();
            }
            catch (Exception ex)
            {
                LogError(ex.Message);
            }
            finally
            {
                IsCommandInProgress = false;
            }
        }

        private async Task ConvertTexturesTask()
        {
            byte[] pcxBytes = null;
            if (string.IsNullOrWhiteSpace(ColormapPath) || ColormapPath.StartsWith('('))
            {
                var streamInfo = Application.GetResourceStream(
                    new Uri("pack://application:,,,/quake2/colormap.pcx"));
                using (MemoryStream ms = new())
                {
                    streamInfo.Stream.CopyTo(ms);
                    pcxBytes = ms.ToArray();
                }
            }
            else
            {
                if (!File.Exists(ColormapPath))
                {
                    LogError($"Colormap file '{ColormapPath}' does not exist");
                    return;
                }
                pcxBytes = File.ReadAllBytes(ColormapPath);
            }
            
            if (!Directory.Exists(TextureFolder))
            {
                LogError($"Texture folder '{TextureFolder}' does not exist");
                return;
            }

            if (!IsPngChecked && !IsJpgChecked)
            {
                LogError("Must choose at least one option, PNG or JPG, to convert to");
                return;
            }

            int jpgQuality = 90;
            if (IsJpgChecked)
            {
                if (!int.TryParse(JpgQuality, out jpgQuality)
                    || (jpgQuality < 0)
                    || (jpgQuality > 100))
                {
                    LogError("JPG Quality should be an integer from 0 to 100");
                    return;
                }
            }

            LogInfo("Searching for wal files...");
            List<ConvertData> allWals = null;
            await Task.Run(() =>
            {
                allWals = ListAllWals(TextureFolder, OutputFolder, IsFlattenChecked);
            });
            LogInfo($"Converting {allWals.Count} wal files");

            var palette = TextureUtils.ReadColorPalette(pcxBytes);

            var start = DateTimeOffset.Now;
            int processCount = 0;
            foreach (var wal in allWals)
            {
                try
                {
                    await Task.Run(() =>
                    {
                        TextureUtils.ConvertWalFile(palette, wal.SourceFile, wal.OutputFolder,
                            IsReplaceTransparentColorChecked, IsPngChecked, IsJpgChecked, jpgQuality);
                    }); 
                }
                catch (Exception ex)
                {
                    LogError($"Failed to process '{wal.SourceFile}'");
                    LogError(ex.Message);
                }

                processCount++;
                if ((processCount % 1000) == 0)
                {
                    LogInfo($"Finished {processCount} files");
                }
            }

            var end = DateTimeOffset.Now;
            LogInfo($"Conversion complete ({GetDurationString(end - start)})");
        }

        private static List<ConvertData> ListAllWals(string inputDir, string outputDir, bool flatten)
        {
            List<ConvertData> data = new();
            Queue<string> processDirs = new();
            processDirs.Enqueue(inputDir);
            while (processDirs.Count > 0)
            {
                string processDir = processDirs.Dequeue();
                Debug.WriteLine($"Processing {processDir}");
                var files = Directory.EnumerateFiles(processDir, "*.wal");
                string relativePath = Path.GetRelativePath(inputDir, processDir);
                foreach (var file in files)
                {
                    var convertData = new ConvertData()
                    {
                        SourceFile = file,
                        OutputFolder = flatten ? outputDir : Path.Combine(outputDir, relativePath)
                    };
                    data.Add(convertData);
                }
                var processSubDirs = Directory.EnumerateDirectories(processDir);
                foreach (var processSubDir in processSubDirs)
                {
                    processDirs.Enqueue(processSubDir);
                }
            }
            return data;
        }

        private static string GetDurationString(TimeSpan duration)
        {
            if (duration.TotalHours > 1)
            {
                return $"{duration.TotalHours:F2} hrs";
            }
            else if (duration.TotalMinutes > 1)
            {
                return $"{duration.TotalMinutes:F2} mins";
            }
            else if (duration.TotalSeconds > 1)
            {
                return $"{duration.TotalSeconds:F2} s";
            }
            else
            {
                return $"{(int)duration.TotalMilliseconds} ms";
            }
        }

        private void ClearLogCommandExecute()
        {
            LogMessages = string.Empty;
            ScrollLogMessagesToEnd();
        }

        private void LogInfo(string message)
        {
            LogMessages += $"{message}\n";
            ScrollLogMessagesToEnd();
        }

        private void LogWarning(string message)
        {
            LogMessages += $"WARNING: {message}\n";
            ScrollLogMessagesToEnd();
        }

        private void LogError(string message)
        {
            LogMessages += $"ERROR: {message}\n";
            ScrollLogMessagesToEnd();
        }

        private void ScrollLogMessagesToEnd()
        {
            _eventAggregator.GetEvent<LogMessagesScrollToEndEvent>().Publish();
        }

        private void RandomStyleColorsCommandExecute()
        {
            Random rnd = new();
            StyleColor1 = System.Windows.Media.Color.FromRgb(
                (byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256));
            StyleColor2 = System.Windows.Media.Color.FromRgb(
                (byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256), (byte)rnd.Next(0, 256));
            LogInfo($"Changed colors to {GetRGBString(StyleColor1)} and {GetRGBString(StyleColor2)}");
        }

        private static string GetRGBString(System.Windows.Media.Color color)
        {
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        // Private variables
        private IEventAggregator _eventAggregator;

        private DelegateCommand _selectColormapCommand;
        private DelegateCommand _selectTextureFolderCommand;
        private DelegateCommand _selectOutputFolderCommand;
        private DelegateCommand _listTexturesCommand;
        private DelegateCommand _convertTexturesCommand;
        private DelegateCommand _clearLogCommand;
        private DelegateCommand _randomStyleColorsCommand;

        private string _colormapPath = "(optional)";
        private string _textureFolder = "D:/Quake2/";
        private string _outputFolder = "./converted/";
        private bool _isPngChecked = true;
        private bool _isReplaceTransparentColorChecked = true;
        private bool _isJpgChecked = false;
        private string _jpgQuality = "90";
        private bool _isMaintainFolderStructureChecked = true;
        private bool _isFlattenChecked = false;
        private string _logMessages = string.Empty;
        private bool _isCommandInProgress = false;

        private System.Windows.Media.Color _styleColor1 =
            System.Windows.Media.Color.FromRgb(255, 255, 255);
        private System.Windows.Media.Color _styleColor2 =
            System.Windows.Media.Color.FromRgb(210, 180, 140);

        private class ConvertData
        {
            public string SourceFile { get; set; }
            public string OutputFolder { get; set; }
        }
    }
}
