
using Microsoft.Win32;
using System;
using System.ComponentModel;
using System.Diagnostics;
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

    private Process? _terminalProcess;
    private string _terminalShell = "PowerShell";

    public MainWindow()
    {
        InitializeComponent();

        EditorText.TextChanged += EditorText_TextChanged;

        UpdateWindowTitle();
        StartTerminal("PowerShell");
    }

    // ---------------------------------------------------------
    // Window and Editor
    // ---------------------------------------------------------

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
        {
            e.Cancel = true;
        }
        else
        {
            StopTerminal();
        }

        base.OnClosing(e);
    }

    // ---------------------------------------------------------
    // Project Folder
    // ---------------------------------------------------------

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

        RestartTerminal();
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
        DependencyObject? current = e.OriginalSource as DependencyObject;

        while (current != null && current is not TreeViewItem)
            current = System.Windows.Media.VisualTreeHelper.GetParent(current);

        if (current is not TreeViewItem item)
            return;

        if (item.Tag is not string filePath ||
            filePath == PlaceholderTag ||
            !File.Exists(filePath))
            return;

        OpenDocument(filePath);
        e.Handled = true;
    }

    // ---------------------------------------------------------
    // File Operations
    // ---------------------------------------------------------

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

    // ---------------------------------------------------------
    // Windows Terminal
    // ---------------------------------------------------------

    private void StartTerminal(string shell)
    {
        StopTerminal();

        _terminalShell = shell;

        string workingDirectory =
            !string.IsNullOrWhiteSpace(_workspacePath) &&
            Directory.Exists(_workspacePath)
                ? _workspacePath
                : Environment.CurrentDirectory;

        var startInfo = new ProcessStartInfo
        {
            FileName = shell == "CMD" ? "cmd.exe" : "powershell.exe",
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        if (shell == "CMD")
        {
            startInfo.ArgumentList.Add("/Q");
            startInfo.ArgumentList.Add("/K");
        }
        else
        {
            startInfo.ArgumentList.Add("-NoLogo");
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NoExit");
        }

        try
        {
            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.OutputDataReceived += Terminal_OutputDataReceived;
            process.ErrorDataReceived += Terminal_ErrorDataReceived;

            if (!process.Start())
            {
                process.Dispose();
                AppendTerminalText("Error: Terminal process could not start.");
                return;
            }

            _terminalProcess = process;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            TerminalShellLabel.Text = shell;

            AppendTerminalText(
                $"[{shell} started] Working directory: {workingDirectory}");

            if (shell == "PowerShell")
            {
                SendTerminalCommand(
                    $"Set-Location -LiteralPath '{workingDirectory.Replace("'", "''")}'");
            }
            else
            {
                SendTerminalCommand(
                    $"cd /d \"{workingDirectory}\"");
            }

            StatusText.Text = $"{shell} terminal ready";
            TerminalInput.Focus();
        }
        catch (Exception ex)
        {
            AppendTerminalText(
                $"Terminal startup error: {ex.Message}");

            StatusText.Text = "Terminal could not start";
        }
    }

    private void StopTerminal()
    {
        Process? process = _terminalProcess;
        _terminalProcess = null;

        if (process == null)
            return;

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit(1000);
            }
        }
                catch (ObjectDisposedException)
        {
            // Process already disposed.
        }
        catch (System.ComponentModel.Win32Exception)
        {
            // Process could not be stopped.
        }
        catch (InvalidOperationException)
        {
            // Process already stopped.
        }
        
    }

    private void RestartTerminal()
    {
        StartTerminal(_terminalShell);
    }

    private void SendTerminalCommand(string command)
    {
        Process? process = _terminalProcess;

        if (process == null)
        {
            AppendTerminalText("Terminal is not running.");
            return;
        }

        try
        {
            if (process.HasExited)
            {
                AppendTerminalText("Terminal process has exited. Restarting...");
                StartTerminal(_terminalShell);
                process = _terminalProcess;

                if (process == null || process.HasExited)
                    return;
            }

            process.StandardInput.WriteLine(command);
            process.StandardInput.Flush();
        }
        catch (Exception ex)
        {
            AppendTerminalText($"Terminal input error: {ex.Message}");
        }
    }

    private void TerminalInput_KeyDown(
        object sender,
        KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;

        string command = TerminalInput.Text.Trim();

        if (string.IsNullOrWhiteSpace(command))
            return;

        AppendTerminalText(
            $"{(_terminalShell == "CMD" ? "C:\\>" : "PS>")} {command}");

        TerminalInput.Clear();

        SendTerminalCommand(command);
    }

    private void Terminal_OutputDataReceived(
        object sender,
        DataReceivedEventArgs e)
    {
        if (e.Data != null)
            Dispatcher.BeginInvoke(
                new Action(() => AppendTerminalText(e.Data)));
    }

    private void Terminal_ErrorDataReceived(
        object sender,
        DataReceivedEventArgs e)
    {
        if (e.Data != null)
        {
            Dispatcher.BeginInvoke(
                new Action(() => AppendTerminalText(e.Data)));
        }
    }

    private void AppendTerminalText(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(
                new Action(() => AppendTerminalText(text)));
            return;
        }

        TerminalOutput.AppendText(text + Environment.NewLine);
        TerminalOutput.ScrollToEnd();
    }

    private void PowerShell_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_terminalShell == "PowerShell" &&
            _terminalProcess != null &&
            !_terminalProcess.HasExited)
            return;

        TerminalOutput.Clear();
        StartTerminal("PowerShell");
    }

    private void CommandPrompt_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_terminalShell == "CMD" &&
            _terminalProcess != null &&
            !_terminalProcess.HasExited)
            return;

        TerminalOutput.Clear();
        StartTerminal("CMD");
    }

    private void ClearTerminal_Click(
        object sender,
        RoutedEventArgs e)
    {
        TerminalOutput.Clear();
        TerminalInput.Focus();
    }
}
