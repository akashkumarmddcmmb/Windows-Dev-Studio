
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MessageBox = System.Windows.MessageBox;

namespace WindowsDevStudio;

public partial class MainWindow : Window
{
    private const string PlaceholderTag = "__PROJECT_TREE_PLACEHOLDER__";

    private string? _currentFilePath;
    private string? _workspacePath;

    private bool _hasUnsavedChanges;
    private bool _isLoadingDocument;

    public MainWindow()
    {
        InitializeComponent();

        EditorText.TextChanged += EditorText_TextChanged;

        UpdateWindowTitle();
    }

    private void EditorText_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        if (_isLoadingDocument)
            return;

        _hasUnsavedChanges = true;
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

        if (result == MessageBoxResult.No)
            return true;

        return SaveCurrentFile();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!ConfirmSaveChanges())
            e.Cancel = true;

        base.OnClosing(e);
    }

    private void OpenFolder_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!ConfirmSaveChanges())
            return;

        using var dialog = new System.Windows.Forms.FolderBrowserDialog
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

        if (dialog.ShowDialog() !=
            System.Windows.Forms.DialogResult.OK)
            return;

        _workspacePath = dialog.SelectedPath;

        _currentFilePath = null;
        _hasUnsavedChanges = false;

        EditorFileName.Text = "CODE EDITOR";
        WorkspaceName.Text = Path.GetFileName(
            _workspacePath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(WorkspaceName.Text))
            WorkspaceName.Text = _workspacePath;

        LoadProjectTree();

        StatusText.Text = $"Workspace opened: {_workspacePath}";

        UpdateWindowTitle();
    }

    private void LoadProjectTree()
    {
        ProjectTree.Items.Clear();

        if (string.IsNullOrWhiteSpace(_workspacePath) ||
            !Directory.Exists(_workspacePath))
            return;

        TreeViewItem root = CreateDirectoryNode(_workspacePath);

        ProjectTree.Items.Add(root);
        root.IsExpanded = true;
    }

    private TreeViewItem CreateDirectoryNode(string folderPath)
    {
        string folderName = Path.GetFileName(
            folderPath.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar));

        if (string.IsNullOrWhiteSpace(folderName))
            folderName = folderPath;

        var node = new TreeViewItem
        {
            Header = "📁 " + folderName,
            Tag = folderPath
        };

        node.Items.Add(new TreeViewItem
        {
            Header = "Loading...",
            Tag = PlaceholderTag
        });

        node.Expanded += DirectoryNode_Expanded;

        return node;
    }

    private void DirectoryNode_Expanded(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not TreeViewItem node ||
            node.Tag is not string folderPath ||
            !Directory.Exists(folderPath))
            return;

        if (node.Items.Count != 1 ||
            node.Items[0] is not TreeViewItem placeholder ||
            !Equals(placeholder.Tag, PlaceholderTag))
            return;

        node.Items.Clear();

        try
        {
            foreach (string directory in
                     Directory.GetDirectories(folderPath)
                         .OrderBy(Path.GetFileName))
            {
                try
                {
                    node.Items.Add(CreateDirectoryNode(directory));
                }
                catch (UnauthorizedAccessException)
                {
                    // Skip inaccessible folders.
                }
                catch (IOException)
                {
                    // Skip folders that cannot be read.
                }
            }

            foreach (string file in
                     Directory.GetFiles(folderPath)
                         .OrderBy(Path.GetFileName))
            {
                node.Items.Add(new TreeViewItem
                {
                    Header = "📄 " + Path.GetFileName(file),
                    Tag = file
                });
            }
        }
        catch (UnauthorizedAccessException)
        {
            node.Items.Add(new TreeViewItem
            {
                Header = "Access denied",
                IsEnabled = false
            });
        }
        catch (IOException)
        {
            node.Items.Add(new TreeViewItem
            {
                Header = "Unable to read folder",
                IsEnabled = false
            });
        }
    }

    private void ProjectTree_MouseDoubleClick(
        object sender,
        MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not FrameworkElement element)
            return;

        if (element.DataContext is not TreeViewItem item)
            return;

        if (item.Tag is not string filePath ||
            filePath == PlaceholderTag ||
            !File.Exists(filePath))
            return;

        OpenDocument(filePath);
        e.Handled = true;
    }

    private void OpenDocument(string filePath)
    {
        if (!ConfirmSaveChanges())
            return;

        try
        {
            string content = File.ReadAllText(filePath);

            _isLoadingDocument = true;

            try
            {
                EditorText.Text = content;
            }
            finally
            {
                _isLoadingDocument = false;
            }

            _currentFilePath = filePath;
            _hasUnsavedChanges = false;

            EditorFileName.Text = Path.GetFileName(filePath);
            StatusText.Text = $"Opened: {filePath}";

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

        if (dialog.ShowDialog() != true)
            return;

        OpenDocument(dialog.FileName);
    }

    private void SaveFile_Click(
        object sender,
        RoutedEventArgs e)
    {
        SaveCurrentFile();
    }

    private bool SaveCurrentFile()
    {
        string? targetPath = _currentFilePath;

        if (string.IsNullOrWhiteSpace(targetPath))
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

            targetPath = dialog.FileName;
        }

        try
        {
            File.WriteAllText(targetPath, EditorText.Text);

            _currentFilePath = targetPath;
            _hasUnsavedChanges = false;

            EditorFileName.Text = Path.GetFileName(targetPath);
            StatusText.Text = $"Saved: {targetPath}";

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
