
using Microsoft.Win32;
using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace WindowsDevStudio;

public partial class MainWindow : Window
{
    private string? _currentFilePath;
    private string? _workspacePath;
    private bool _hasUnsavedChanges;

    public MainWindow()
    {
        InitializeComponent();

        EditorText.TextChanged += EditorText_TextChanged;
    }

    private void EditorText_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_currentFilePath is null)
        {
            _hasUnsavedChanges = true;
        }
        else
        {
            _hasUnsavedChanges = true;
        }

        UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        string fileName = string.IsNullOrWhiteSpace(_currentFilePath)
            ? "Untitled"
            : Path.GetFileName(_currentFilePath);

        Title = $"Windows Dev Studio - {fileName}" +
                (_hasUnsavedChanges ? " *" : string.Empty);
    }

    private bool ConfirmSaveChanges()
    {
        if (!_hasUnsavedChanges)
            return true;

        MessageBoxResult result = MessageBox.Show(
            "You have unsaved changes. Do you want to save them?",
            "Unsaved Changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (result == MessageBoxResult.Cancel)
            return false;

        if (result == MessageBoxResult.Yes)
            return SaveCurrentFile();

        return true;
    }

    private void OpenFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ConfirmSaveChanges())
            return;

        using var dialog =
            new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "Select a project folder",
                UseDescriptionForTitle = true,
                ShowNewFolderButton = true
            };

        if (!string.IsNullOrWhiteSpace(_workspacePath) &&
            Directory.Exists(_workspacePath))
        {
            dialog.InitialDirectory = _workspacePath;
        }

        if (dialog.ShowDialog() ==
            System.Windows.Forms.DialogResult.OK)
        {
            _workspacePath = dialog.SelectedPath;

            WorkspaceName.Text =
                new DirectoryInfo(_workspacePath).Name;

            StatusText.Text =
                $"Workspace selected: {_workspacePath}";

            UpdateWindowTitle();
        }
    }

    private void OpenFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ConfirmSaveChanges())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Open source file",
            Filter = "All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (!string.IsNullOrWhiteSpace(_workspacePath) &&
            Directory.Exists(_workspacePath))
        {
            dialog.InitialDirectory = _workspacePath;
        }

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string content =
                    File.ReadAllText(dialog.FileName);

                _currentFilePath = dialog.FileName;
                EditorText.Text = content;
                _hasUnsavedChanges = false;

                EditorFileName.Text =
                    Path.GetFileName(_currentFilePath);

                StatusText.Text =
                    $"Opened: {_currentFilePath}";

                UpdateWindowTitle();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Could not open file.\n\n{ex.Message}",
                    "Open File Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
    }

    private void SaveFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveCurrentFile();
    }

    private bool SaveCurrentFile()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            var dialog = new SaveFileDialog
            {
                Title = "Save source file",
                Filter = "All files (*.*)|*.*",
                FileName = "Untitled.txt",
                AddExtension = false,
                OverwritePrompt = true
            };

            if (!string.IsNullOrWhiteSpace(_workspacePath) &&
                Directory.Exists(_workspacePath))
            {
                dialog.InitialDirectory = _workspacePath;
            }

            if (dialog.ShowDialog() != true)
                return false;

            _currentFilePath = dialog.FileName;
        }

        try
        {
            File.WriteAllText(
                _currentFilePath,
                EditorText.Text);

            _hasUnsavedChanges = false;

            EditorFileName.Text =
                Path.GetFileName(_currentFilePath);

            StatusText.Text =
                $"Saved: {_currentFilePath}";

            UpdateWindowTitle();

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save file.\n\n{ex.Message}",
                "Save File Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }
}
