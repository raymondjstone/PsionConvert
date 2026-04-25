using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace PsionConvert
{
    public sealed partial class MainWindow : Window
    {
        private ParseResult? _lastResult;
        private string _lastFormattedText = "";

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add("*");

            StorageFile? file = await picker.PickSingleFileAsync();
            if (file == null) return;

            try
            {
                byte[] data = File.ReadAllBytes(file.Path);
                _lastResult = PsionDbParser.Parse(data);

                if (!string.IsNullOrEmpty(_lastResult.Error))
                {
                    StatusText.Text = "Error: " + _lastResult.Error;
                    return;
                }

                DisplayResults(file.Name, _lastResult);
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to read file: " + ex.Message;
            }
        }

        private void DisplayResults(string fileName, ParseResult result)
        {
            FilePathText.Text = fileName;
            FormatText.Text = result.FormatName;
            StatusText.Text = $"{result.Records.Count} records";

            var viewModels = result.Records.Select(r => new RecordViewModel
            {
                Field1 = r.Fields.Count > 0 ? r.Fields[0] : "",
                Field2 = r.Fields.Count > 1 ? r.Fields[1] : "",
                Field3 = r.Fields.Count > 2 ? r.Fields[2] : "",
            }).ToList();

            RecordsList.ItemsSource = viewModels;

            _lastFormattedText = BuildTextOutput(result);

            CopyButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
        }

        private static string BuildTextOutput(ParseResult result)
        {
            if (result.Records.Count == 0) return "";

            int maxFields = result.Records.Max(r => r.Fields.Count);
            var widths = new int[maxFields];
            foreach (var rec in result.Records)
                for (int i = 0; i < rec.Fields.Count; i++)
                    widths[i] = Math.Max(widths[i], rec.Fields[i].Length);

            var sb = new StringBuilder();
            foreach (var rec in result.Records)
            {
                var parts = new List<string>();
                for (int i = 0; i < maxFields; i++)
                {
                    string val = i < rec.Fields.Count ? rec.Fields[i] : "";
                    parts.Add(i < maxFields - 1 ? val.PadRight(widths[i]) : val);
                }
                sb.AppendLine(string.Join("  ", parts));
            }
            return sb.ToString();
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFormattedText)) return;
            var dataPackage = new DataPackage();
            dataPackage.SetText(_lastFormattedText);
            Clipboard.SetContent(dataPackage);
            StatusText.Text = $"{_lastResult?.Records.Count ?? 0} records — copied to clipboard";
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_lastFormattedText)) return;

            var picker = new FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            picker.SuggestedFileName = (FilePathText.Text ?? "output") + ".txt";
            picker.FileTypeChoices.Add("Text file", new List<string> { ".txt" });
            picker.FileTypeChoices.Add("CSV file", new List<string> { ".csv" });

            StorageFile? file = await picker.PickSaveFileAsync();
            if (file == null) return;

            try
            {
                string ext = Path.GetExtension(file.Path).ToLowerInvariant();
                string content = ext == ".csv" ? BuildCsvOutput(_lastResult!) : _lastFormattedText;
                File.WriteAllText(file.Path, content, Encoding.UTF8);
                StatusText.Text = $"Saved to {file.Name}";
            }
            catch (Exception ex)
            {
                StatusText.Text = "Failed to save: " + ex.Message;
            }
        }

        private static string BuildCsvOutput(ParseResult result)
        {
            var sb = new StringBuilder();
            foreach (var rec in result.Records)
            {
                var parts = rec.Fields.Select(f => f.Contains(',') ? $"\"{f}\"" : f);
                sb.AppendLine(string.Join(",", parts));
            }
            return sb.ToString();
        }
    }
}
