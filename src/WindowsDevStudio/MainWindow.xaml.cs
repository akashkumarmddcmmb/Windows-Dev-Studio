
using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace WindowsDevStudio;

public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private string? _workspacePath;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = "Select a project folder",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            _workspacePath = dialog.SelectedPath;

            WorkspaceName.Text =
                Path.GetFileName(_workspacePath);

            StatusText.Text =
                $"Workspace selected: {_workspacePath}";
        }
    }

    private void OpenFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open source file",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (!string.IsNullOrWhiteSpace(_workspacePath))
        {
            dialog.InitialDirectory = _workspacePath;
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                _currentFilePath = dialog.FileName;

                EditorText.Text =
                    File.ReadAllText(_currentFilePath);

                EditorFileName.Text =
                    Path.GetFileName(_currentFilePath);

                StatusText.Text =
                    $"Opened: {_currentFilePath}";
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open file.\n\n{ex.Message}",
                    "Open File",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save source file",
                Filter = "All files (*.*)|*.*",
                FileName = "Untitled.txt"
            };

            if (dialog.ShowDialog() != true)
                return;

            _currentFilePath = dialog.FileName;
        }

        try
        {
            File.WriteAllText(
                _currentFilePath,
                EditorText.Text);

            EditorFileName.Text =
                Path.GetFileName(_currentFilePath);

            StatusText.Text =
                $"Saved: {_currentFilePath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save file.\n\n{ex.Message}",
                "Save File",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
