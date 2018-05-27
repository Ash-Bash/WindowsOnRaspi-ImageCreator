using Microsoft.Win32;
using Microsoft.WindowsAPICodePack.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using DiscUtils.Iso9660;
using System.Security.Principal;
using DiscUtils.Fat;
using DiscUtils.Ntfs;
using DiscUtils.Partitions;
using DiscUtils.Raw;
using DiscUtils;
using System.Management;
using System.Runtime.InteropServices;

namespace WORPI.ImageCreator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        // Variables
        private string sdCardPath;
        private string winImagePath;
        private string raspiPkgPath;
        private string winOnRaspiPath;

        private List<DiskItemObject> diskItemsList = new List<DiskItemObject>();
        private DiskItemObject selectedDisk;

        private string[] tempFolders;

        private string appPath;

        public MainWindow()
        {
            InitializeComponent();

            appPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WinOnRaspiImageCreator");
            tempFolders = new string[5];
            statusTextBlock.Text = "";

            ManagementObjectSearcher win32DiskDrives = new ManagementObjectSearcher("select * from Win32_DiskDrive");
            foreach (ManagementObject win32DiskDrive in win32DiskDrives.Get())
            {
                Int64 size;
                int index = Convert.ToInt32(win32DiskDrive.Properties["Index"].Value);
                string model = win32DiskDrive.Properties["Model"].Value.ToString();
                string mediaType;
                if (win32DiskDrive.Properties["Size"].Value != null)
                {
                    string sizeString = win32DiskDrive.Properties["Size"].Value.ToString();
                    size = Int64.Parse(sizeString) / 1024 / 1024 / 1024;
                }
                else {
                    size = 0;
                }

                if (win32DiskDrive.Properties["MediaType"].Value != null)
                {
                    mediaType = win32DiskDrive.Properties["MediaType"].Value.ToString();
                }
                else {
                    mediaType = "Unknown Media Type";
                }

                diskItemsList.Add(new DiskItemObject(index, model, mediaType, size.ToString() + "GB"));
                sdCardPathComboBox.Items.Add("Disk " + index + " - " + model + " - " + mediaType + " - " + size.ToString() + "GB");
            }

            if (sdCardPathComboBox.Items.Count > 0)
            {
                sdCardPathComboBox.SelectedIndex = 0;
                selectedDisk = diskItemsList.ToArray()[sdCardPathComboBox.SelectedIndex];

            }
        }

        private void sdCardPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combobox = (ComboBox)sender;
            selectedDisk = diskItemsList.ToArray()[combobox.SelectedIndex];
            Debug.WriteLine("Selected Disk: " + selectedDisk);

        }

        private void winImageBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFileDialog = new CommonOpenFileDialog();
            CommonFileDialogResult result = openFileDialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                string dir = openFileDialog.FileName;
                winImagePath = dir;
                winImageComboBox.Text = dir;
            }
        }

        private void raspPiPkgBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFileDialog = new CommonOpenFileDialog();
            CommonFileDialogResult result = openFileDialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                string dir = openFileDialog.FileName;
                raspiPkgPath = dir;
                raspiPkgDirComboBox.Text = dir;
            }
        }

        private void winOnPiBrowseButton_Click(object sender, RoutedEventArgs e)
        {
            CommonOpenFileDialog openFileDialog = new CommonOpenFileDialog();
            CommonFileDialogResult result = openFileDialog.ShowDialog();

            if (result == CommonFileDialogResult.Ok)
            {
                string dir = openFileDialog.FileName;
                winOnRaspiPath = dir;
                winOnRaspiDirComboBox.Text = dir;
            }
        }

        private void compileWindowsButton_Click(object sender, RoutedEventArgs e)
        {
            // Sets up Paths for required folders
            var tempUEFIPath = System.IO.Path.Combine(appPath, "temp", "UEFI");
            var tempUUPPath = System.IO.Path.Combine(appPath, "temp", "UUP");
            var tempSystem32Path = System.IO.Path.Combine(appPath, "temp", "System32");
            var tempExtractedFoldersPath = System.IO.Path.Combine(appPath, "temp", "Extracted Folders");
            var tempImagePath = System.IO.Path.Combine(appPath, "temp", "Image");

            // Save temp Folders to an array
            tempFolders[0] = tempUEFIPath;
            tempFolders[1] = tempUUPPath;
            tempFolders[2] = tempSystem32Path;
            tempFolders[3] = tempExtractedFoldersPath;
            tempFolders[4] = tempImagePath;

            setupTempFolderStructure();
        }

        // Creates a Folder Structure for Temp Directory
        private void setupTempFolderStructure() {


            statusTextBlock.Text = "Setting Up Structure";

            // Creates Directories for Required Folders+
            Directory.CreateDirectory(tempFolders[0]);
            Directory.CreateDirectory(tempFolders[1]);
            Directory.CreateDirectory(tempFolders[2]);
            Directory.CreateDirectory(tempFolders[3]);
            Directory.CreateDirectory(tempFolders[4]);

            imageProgressBar.Value = 5;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

            copySourceFiles();
        }

        // Copies Source Files from Zip files to their initial dirs
        private void copySourceFiles() {
            statusTextBlock.Text = "Extracting Files";

            try
            {
                Task.Run(() => {
                    // Extracts all required Zip Files
                    ZipFile.ExtractToDirectory(winImagePath, tempFolders[1]);
                    ZipFile.ExtractToDirectory(raspiPkgPath, tempFolders[3]);
                    ZipFile.ExtractToDirectory(winOnRaspiPath, tempFolders[3]);

                    // Rename Folders in the Extracted Folders
                    var extrFolders = Directory.GetDirectories(tempFolders[3]);
                    Directory.Move(extrFolders[0], System.IO.Path.Combine(appPath, "temp", "Extracted Folders", "RaspberryPiPkg"));
                    Directory.Move(extrFolders[1], System.IO.Path.Combine(appPath, "temp", "Extracted Folders", "winOnRaspi"));

                    //createWindowsISOFile();
                    //copyInstallWimFile();
                    

                    //Creates First Start Up Reg File
                    string firstStartUpString = Properties.Resources.firststartup;
                    string instalUEFIString = Properties.Resources.InstallUEFI;
                    string signUEFIFilesString = Properties.Resources.SignUEFIFiles;
                    File.WriteAllText(System.IO.Path.Combine(appPath, "temp") + "/firststartup.reg", firstStartUpString);
                    File.WriteAllText(System.IO.Path.Combine(appPath, "temp") + "/InstallUEFI.cmd", instalUEFIString);
                    File.WriteAllText(System.IO.Path.Combine(appPath, "temp") + "/SignUEFIFiles.cmd", signUEFIFilesString);

                    createWindowsISOFile();
                    return true;
                });

                imageProgressBar.Value = 10;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
            } catch (IOException err)
            {
                MessageBox.Show("Something went wrong" + System.Environment.NewLine + "IOException source: {0}", err.Source);
            }
        }

        // Creates a Windows ISO File
        private void createWindowsISOFile() {

            Debug.WriteLine(System.IO.Path.Combine(tempFolders[1], "creatingISO.cmd"));
            var path = System.IO.Path.Combine(tempFolders[1], "creatingISO.cmd");

            if (File.Exists(path))
            {
                Debug.WriteLine("ISO CMD Path Exists: " + true);
                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.WriteLine(path);
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    copyInstallWimFile();
                }
            }
            else {
                Debug.WriteLine("ISO CMD Path Exists: " + false);
            }
        }

        // Copies the install.wim file from the ISO File (Still Needs work on)
        private void copyInstallWimFile() {
            DirectoryInfo extrFolders = new DirectoryInfo(tempFolders[1]);
            FileInfo[] fileInfo = extrFolders.GetFiles();

            using (FileStream isoStream = File.OpenRead(System.IO.Path.Combine(tempFolders[1], fileInfo[0].ToString())))
            {
                CDReader cd = new CDReader(isoStream, true);
                //cd.CopyFile(@"sources\install.wim", System.IO.Path.Combine(appPath, "temp"));
                Stream fileStream = cd.OpenFile(@"sources\install.wim", FileMode.Open);
                // Use fileStream...
                using (var stream = new FileStream(System.IO.Path.Combine(appPath, "temp"), FileMode.Create, FileAccess.Write))
                {
                    fileStream.CopyTo(stream);
                }
            }
        }

        // Copies Provided packages to their right dirs ready for processing for the next few steps
        private void copyRaspiPackages() {

            var UEFIPath = System.IO.Path.Combine(tempFolders[3], "RaspberryPiPkg", "Binary", "prebuilt", "2018Mar1-GCC49", "DEBUG");
            CopyFilesRecursively(new DirectoryInfo(UEFIPath), new DirectoryInfo(tempFolders[0]));

            var driverPath = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "driver_prebuilts");
            CopyFilesRecursively(new DirectoryInfo(driverPath), new DirectoryInfo(tempFolders[2]));

            var driver2Path = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "winpe_stuff");
            CopyFilesRecursively(new DirectoryInfo(driver2Path), new DirectoryInfo(tempFolders[2]));

            mountInstallWimFile();
        }

        // Mounts install.wim file to Image Dir
        private void mountInstallWimFile() {
            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/mount-image /imagefile:install.wim /Index:1 /MountDir:Image";

            //if () 
            if (File.Exists(wimpath))
            {
                Debug.WriteLine("install.wim Path Exists: " + true);
                Process cmd = new Process();
                cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                cmd.StartInfo.WorkingDirectory = cdPath;
                cmd.StartInfo.Verb = "runas";

                foreach (string arg in dismArgs)
                {
                    cmd.StartInfo.Arguments += arg;
                }
                
                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.EnableRaisingEvents = true;

                cmd.Start();

                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();
                if (cmd.HasExited) { 
                    addDriversInstallWimFile();
                }

                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
            }
        }

        // Adds Drivers to Image Dir ready to be commit to install.wim
        private void addDriversInstallWimFile()
        {
            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/image:Image /add-driver /driver:system32 /recurse /forceunsigned";

            //if () 
            if (File.Exists(wimpath))
            {
                Debug.WriteLine("install.wim Path Exists: " + true);
                Process cmd = new Process();
                cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                cmd.StartInfo.WorkingDirectory = cdPath;
                cmd.StartInfo.Verb = "runas";

                foreach (string arg in dismArgs)
                {
                    cmd.StartInfo.Arguments += arg;
                }

                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.EnableRaisingEvents = true;


                cmd.Start();

                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    unmountInstallWimFile();
                }

                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
            }
        }

        // Unmounts & Commits changes to Install.wim file
        private void unmountInstallWimFile()
        {
            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/unmount-wim /mountdir:Image /commit";

            //if () 
            if (File.Exists(wimpath))
            {
                Debug.WriteLine("install.wim Path Exists: " + true);
                Process cmd = new Process();
                cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                cmd.StartInfo.WorkingDirectory = cdPath;
                cmd.StartInfo.Verb = "runas";

                foreach (string arg in dismArgs)
                {
                    cmd.StartInfo.Arguments += arg;
                }

                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.EnableRaisingEvents = true;

                cmd.Start();

                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    createSCCardPartitions();
                }

                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
            }
        }

        // Creates Required Partitions for Windows Files using diskpart
        private void createSCCardPartitions() {

            Process cmd = new Process();
            cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "diskpart.exe");
            cmd.StartInfo.Verb = "runas";

            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.EnableRaisingEvents = true;

            cmd.Start();
            cmd.StandardInput.WriteLine("select disk " + selectedDisk.diskNumber);
            cmd.StandardInput.WriteLine("clean");
            cmd.StandardInput.WriteLine("create partition primary size=400");
            cmd.StandardInput.WriteLine("create partition primary");
            cmd.StandardInput.WriteLine("select partition 1");
            cmd.StandardInput.WriteLine("active");
            cmd.StandardInput.WriteLine("format fs=fat32 quick label=BOOT");
            cmd.StandardInput.WriteLine("assign letter=P");
            cmd.StandardInput.WriteLine("select partition 2");
            cmd.StandardInput.WriteLine("active");
            cmd.StandardInput.WriteLine("format fs=ntfs quick label=Windows");
            cmd.StandardInput.WriteLine("assign letter=I");
            cmd.StandardInput.WriteLine("exit");

            Console.WriteLine(cmd.StandardOutput.ReadToEnd());

            cmd.WaitForExit();
            if (cmd.HasExited)
            {
                addWindowsFilesToWindows();
            }
        }

        // Added Required Windows Files from install.wim
        private void addWindowsFilesToWindows()
        {

            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/apply-image /imagefile:install.wim /index:1 /applydir:" + @"I:\";

            //if () 
            if (File.Exists(wimpath))
            {
                Debug.WriteLine("install.wim Path Exists: " + true);
                Process cmd = new Process();
                cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                cmd.StartInfo.WorkingDirectory = cdPath;
                cmd.StartInfo.Verb = "runas";

                foreach (string arg in dismArgs)
                {
                    cmd.StartInfo.Arguments += arg;
                }

                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.EnableRaisingEvents = true;

                cmd.Start();

                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();
                if (cmd.HasExited)
                {
                    addUEFIFilesToBoot();
                }

                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
            }
        }

        // Added Required UEFIFiles from both i:/windows and Sourced Files that where provided  (Still Needs work on)
        private void addUEFIFilesToBoot()
        {

            Debug.WriteLine(System.IO.Path.Combine(appPath, "temp", "InstallUEFI.cmd"));
            var path = System.IO.Path.Combine(appPath, "temp", "InstallUEFI.cmd");

            CopyFilesRecursively(new DirectoryInfo(tempFolders[0]), new DirectoryInfo("P:/"));

            bool hasFiles = false;
            var files = Directory.GetFiles("P:/");

            if (files.Length > 0)
            {
                hasFiles = true;
            }
            else {
                hasFiles = false;
            }

            if (hasFiles)
            {
                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.WriteLine("bcdboot " + @"i:\windows /s p: /f UEFI");
                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    signWindowsFiles();
                }
            }
        }

        // Signs Windows Files in the UEFI Partition  (Still Needs work on)
        private void signWindowsFiles() {
            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.Verb = "runas";

            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.EnableRaisingEvents = true;

            cmd.Start();
            cmd.StandardInput.WriteLine("bcdedit /store " + @"P:\EFI\Microsoft\Boot\bcd" + " /set {default} testsigning on");
            cmd.StandardInput.WriteLine("bcdedit /store " + @"P:\EFI\Microsoft\Boot\bcd" + " /set {default} nointegritychecks on");

            Console.WriteLine(cmd.StandardOutput.ReadToEnd());

            cmd.WaitForExit();
            if (cmd.HasExited)
            {
                cleanUp();
            }
        }

        // Deletes Temp dirs and Files and Unmounts any Images that were mounted when installing files on the SD Card (Still Needs work on)
        private void cleanUp() {

        }

        // Allows Copying files and Folders from one Dir to Another
        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            try
            {
                foreach (DirectoryInfo dir in source.GetDirectories())
                    CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
                foreach (FileInfo file in source.GetFiles())
                    file.CopyTo(System.IO.Path.Combine(target.FullName, file.Name));
            }
            catch {
                Debug.WriteLine("Somewthing Went Wrong!!");
            }
        }
    }
}
