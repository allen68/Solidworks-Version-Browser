using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices; // Required for Dark Mode Hack
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;         // Required for Window Handle
using System.Windows.Media.Imaging;

namespace VersionBrowser
{
    public partial class MainWindow : Window
    {
        // --- 1. DARK MODE TITLE BAR SETTINGS ---
        [DllImport("dwmapi.dll", PreserveSig = true)]
        public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // --- 2. DATA MODELS ---
        public class NodeItem
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public string Icon { get; set; }        // Icons: ☁, 💻, 📁, 📄
            public string IconColor { get; set; }   // Hex Colors
            public string Type { get; set; }        // "Drive", "Folder", "File"

            public ObservableCollection<NodeItem> Items { get; set; } = new ObservableCollection<NodeItem>();
            public NodeItem() { }
        }

        public class VersionItem
        {
            public string Name { get; set; }
            public string Date { get; set; }
            public string FullPath { get; set; }
            public string JpgPath { get; set; }
            public string TxtPath { get; set; }
        }

        public MainWindow()
        {
            InitializeComponent();

            // Enable Dark Mode Title Bar when window initializes
            this.SourceInitialized += (s, e) => EnableDarkModeTitleBar();

            LoadDrives();
        }

        private void EnableDarkModeTitleBar()
        {
            var windowInteropHelper = new WindowInteropHelper(this);
            IntPtr hWnd = windowInteropHelper.Handle;
            int preference = 1; // True
            DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref preference, sizeof(int));
        }

        // --- 3. FILE TREE LOGIC ---
        private void LoadDrives()
        {
            fileTree.Items.Clear();

            // A. SPECIAL: FIND ONEDRIVE
            string oneDrivePath = Environment.GetEnvironmentVariable("OneDrive");
            if (string.IsNullOrEmpty(oneDrivePath)) oneDrivePath = Environment.GetEnvironmentVariable("OneDriveConsumer");
            if (string.IsNullOrEmpty(oneDrivePath)) oneDrivePath = Environment.GetEnvironmentVariable("OneDriveCommercial");

            if (!string.IsNullOrEmpty(oneDrivePath) && Directory.Exists(oneDrivePath))
            {
                NodeItem cloudNode = new NodeItem
                {
                    Name = "OneDrive",
                    FullPath = oneDrivePath,
                    Icon = "☁",
                    IconColor = "#33A1DE",
                    Type = "Folder"
                };
                cloudNode.Items.Add(new NodeItem { Name = "Loading..." });
                fileTree.Items.Add(cloudNode);
            }

            // B. STANDARD DRIVES
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady)
                {
                    NodeItem driveNode = new NodeItem
                    {
                        Name = $"{drive.VolumeLabel} ({drive.Name})",
                        FullPath = drive.Name,
                        Icon = "💻",
                        IconColor = "#4CC2FF",
                        Type = "Drive"
                    };
                    driveNode.Items.Add(new NodeItem { Name = "Loading..." });
                    fileTree.Items.Add(driveNode);
                }
            }
        }

        // Lazy Loading: Fires when user clicks arrow >
        private void fileTree_Expanded(object sender, RoutedEventArgs e)
        {
            TreeViewItem item = e.OriginalSource as TreeViewItem;
            if (item == null) return;

            NodeItem node = item.DataContext as NodeItem;

            // If it has a dummy item, clear it and load real folders
            if (node.Items.Count == 1 && node.Items[0].Name == "Loading...")
            {
                node.Items.Clear();
                LoadDirectory(node);
            }
        }

        private void LoadDirectory(NodeItem parentNode)
        {
            try
            {
                string path = parentNode.FullPath;

                // Load Subfolders
                foreach (string dir in Directory.GetDirectories(path))
                {
                    string dirName = Path.GetFileName(dir);
                    // Skip hidden .history folders
                    if (dirName.StartsWith(".") || (new DirectoryInfo(dir).Attributes & FileAttributes.Hidden) != 0) continue;

                    NodeItem folderNode = new NodeItem
                    {
                        Name = dirName,
                        FullPath = dir,
                        Icon = "📁",
                        IconColor = "#FFD700",
                        Type = "Folder"
                    };
                    folderNode.Items.Add(new NodeItem { Name = "Loading..." }); // Dummy for next level
                    parentNode.Items.Add(folderNode);
                }

                // Load SLDPRT Files
                foreach (string file in Directory.GetFiles(path, "*.SLDPRT"))
                {
                    if (Path.GetFileName(file).StartsWith("~")) continue; // Skip temp files

                    NodeItem fileNode = new NodeItem
                    {
                        Name = Path.GetFileName(file),
                        FullPath = file,
                        Icon = "📄",
                        IconColor = "#FFFFFF",
                        Type = "File"
                    };
                    parentNode.Items.Add(fileNode);
                }
            }
            catch { /* Ignore permission errors */ }
        }

        // --- 4. TIMELINE & PREVIEW LOGIC ---
        private void fileTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            NodeItem selected = e.NewValue as NodeItem;
            if (selected == null || selected.Type != "File") return;

            LoadHistoryForFile(selected.FullPath);
        }

        private void LoadHistoryForFile(string filePath)
        {
            string parentFolder = Path.GetDirectoryName(filePath);
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string historyPath = Path.Combine(parentFolder, ".history", fileName);

            lstVersions.ItemsSource = null;
            imgPreview.Source = null;
            txtNoPreview.Visibility = Visibility.Visible;
            txtData.Text = "Select a version to see feature details...";

            if (!Directory.Exists(historyPath)) return;

            var versions = new List<VersionItem>();
            foreach (var file in Directory.GetFiles(historyPath, "*.SLDPRT"))
            {
                string fName = Path.GetFileNameWithoutExtension(file);
                // Parse Date from Filename
                string dateStr = "Unknown";
                try
                {
                    string[] parts = fName.Split(new string[] { "_v_" }, StringSplitOptions.None);
                    if (parts.Length > 1) dateStr = parts[1].Replace("-", "/").Replace("_", ":");
                }
                catch { }

                versions.Add(new VersionItem
                {
                    Name = fName,
                    Date = dateStr,
                    FullPath = file,
                    JpgPath = Path.ChangeExtension(file, ".jpg"),
                    TxtPath = Path.ChangeExtension(file, ".txt")
                });
            }

            lstVersions.ItemsSource = versions.OrderByDescending(v => v.Name);
        }

        private void lstVersions_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (lstVersions.SelectedItem is VersionItem selected)
            {
                // Show JPG
                if (File.Exists(selected.JpgPath))
                {
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(selected.JpgPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    imgPreview.Source = bitmap;
                    txtNoPreview.Visibility = Visibility.Collapsed;
                }
                else
                {
                    imgPreview.Source = null;
                    txtNoPreview.Visibility = Visibility.Visible;
                }

                // Show Text Data
                if (File.Exists(selected.TxtPath))
                    txtData.Text = File.ReadAllText(selected.TxtPath);
                else
                    txtData.Text = "No data available.";
            }
        }

        // --- 5. BUTTON ACTIONS ---

        // RESTORE BUTTON
        private void btnRestore_Click(object sender, RoutedEventArgs e)
        {
            if (!(lstVersions.SelectedItem is VersionItem selected)) return;

            string restoreName = "RESTORED_" + Path.GetFileName(selected.FullPath);

            // Try to find original folder by going up two levels from .history/partname/file
            string targetFolder = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            string parentHistory = Directory.GetParent(Path.GetDirectoryName(selected.FullPath)).FullName;

            if (parentHistory.EndsWith(".history"))
            {
                targetFolder = Directory.GetParent(parentHistory).FullName;
            }

            string targetPath = Path.Combine(targetFolder, restoreName);

            try
            {
                File.Copy(selected.FullPath, targetPath, true);
                OpenSolidWorksAndFile(targetPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        // BRANCH BUTTON
        private void btnBranch_Click(object sender, RoutedEventArgs e)
        {
            if (!(lstVersions.SelectedItem is VersionItem selected))
            {
                MessageBox.Show("Please select a version to branch from.");
                return;
            }

            string branchName = ShowInputDialog("Enter a name for this new branch:", "Create Branch");
            if (string.IsNullOrWhiteSpace(branchName)) return;

            // Calculate paths
            string historyFolder = Path.GetDirectoryName(selected.FullPath);
            string historyRoot = Directory.GetParent(historyFolder).FullName;
            string projectRoot = Directory.GetParent(historyRoot).FullName;

            string branchFolder = Path.Combine(projectRoot, branchName);

            if (Directory.Exists(branchFolder))
            {
                MessageBox.Show("A folder with this name already exists!");
                return;
            }

            try
            {
                Directory.CreateDirectory(branchFolder);

                // Clean filename (remove timestamp)
                string originalFileName = Path.GetFileName(selected.FullPath);
                if (originalFileName.Contains("_v_"))
                {
                    originalFileName = originalFileName.Substring(0, originalFileName.IndexOf("_v_")) + ".SLDPRT";
                }

                string targetPath = Path.Combine(branchFolder, originalFileName);

                File.Copy(selected.FullPath, targetPath);

                MessageBox.Show($"Branch Created!\nLocation: {branchFolder}\n\nOpening SolidWorks...");
                OpenSolidWorksAndFile(targetPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error creating branch: " + ex.Message);
            }
        }

        // --- 6. HELPERS ---

        // Safe Open Method (Fixes "Invocation" Crash)
        private void OpenSolidWorksAndFile(string filePath)
        {
            try
            {
                Process.Start(new ProcessStartInfo(filePath) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not open file: " + ex.Message);
            }
        }

        // Simple Input Dialog Helper
        private string ShowInputDialog(string text, string title)
        {
            Window inputWindow = new Window
            {
                Width = 300,
                Height = 150,
                Title = title,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30))
            };

            StackPanel stack = new StackPanel { Margin = new Thickness(10) };

            TextBlock lbl = new TextBlock { Text = text, Foreground = System.Windows.Media.Brushes.White, Margin = new Thickness(0, 0, 0, 10) };
            TextBox txt = new TextBox { Margin = new Thickness(0, 0, 0, 10) };

            Button btnOk = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = HorizontalAlignment.Right, Width = 60 };
            btnOk.Click += (s, e) => { inputWindow.DialogResult = true; inputWindow.Close(); };

            stack.Children.Add(lbl);
            stack.Children.Add(txt);
            stack.Children.Add(btnOk);
            inputWindow.Content = stack;

            if (inputWindow.ShowDialog() == true)
            {
                return txt.Text;
            }
            return "";
        }
    }
}