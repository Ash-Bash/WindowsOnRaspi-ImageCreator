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

        private DiskItemObject[] diskItems = new DiskItemObject[1];
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

                diskItems.Append(new DiskItemObject(index, model, mediaType, size.ToString() + "GB"));
                sdCardPathComboBox.Items.Add("Disk " + index + " - " + model + " - " + mediaType + " - " + size.ToString() + "GB");
            }

            if (sdCardPathComboBox.Items.Count > 0)
            {
                sdCardPathComboBox.SelectedIndex = 0;
                selectedDisk = diskItems[sdCardPathComboBox.SelectedIndex];

            }
        }

        private void sdCardPathComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var combobox = (ComboBox)sender;
            selectedDisk = diskItems[combobox.SelectedIndex];

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
            setupTempFolderStructure();
        }

        // Creates a Folder Structure for Temp Directory
        private void setupTempFolderStructure() {


            statusTextBlock.Text = "Setting Up Structure";
           
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

            // Creates Directories for Required Folders+
            Directory.CreateDirectory(tempUEFIPath);
            Directory.CreateDirectory(tempUUPPath);
            Directory.CreateDirectory(tempSystem32Path);
            Directory.CreateDirectory(tempExtractedFoldersPath);
            Directory.CreateDirectory(tempImagePath);

            imageProgressBar.Value = 5;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

            copySourceFiles();
        }

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
                    string regFileContent = string.Format(firstStartUpString, System.IO.Path.Combine(appPath, "temp"));
                    File.WriteAllText(System.IO.Path.Combine(appPath, "temp") + "/firststartup.reg", firstStartUpString);

                    copyRaspiPackages();
                    return true;
                });
                imageProgressBar.Value = 10;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
            } catch (IOException err)
            {
                MessageBox.Show("Something went wrong" + System.Environment.NewLine + "IOException source: {0}", err.Source);
            }
        }

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
            }
            else {
                Debug.WriteLine("ISO CMD Path Exists: " + false);
            }
        }

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

        private void copyRaspiPackages() {

            var UEFIPath = System.IO.Path.Combine(tempFolders[3], "RaspberryPiPkg", "Binary", "prebuilt", "2018Mar1-GCC49", "DEBUG");
            CopyFilesRecursively(new DirectoryInfo(UEFIPath), new DirectoryInfo(tempFolders[0]));

            var driverPath = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "driver_prebuilts");
            CopyFilesRecursively(new DirectoryInfo(driverPath), new DirectoryInfo(tempFolders[2]));

            var driver2Path = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "winpe_stuff");
            CopyFilesRecursively(new DirectoryInfo(driver2Path), new DirectoryInfo(tempFolders[2]));

            mountInstallWimFile();
        }

        private void mountInstallWimFile() {
            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/mount-image /imagefile:install.wim /Index:1 /MountDir:Image";
            //dismArgs[1] = "/image:Image /add-driver /driver:system32 /recurse /forceunsigned";
            //dismArgs[2] = "/unmount-wim /mountdir:Image /commit";

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
                /*cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                //cmd.StartInfo.CreateNoWindow = true;
                cmd.StartInfo.UseShellExecute = false;
                cmd.Start();

                cmd.StandardInput.WriteLine("cd " + cdPath);
                cmd.StandardInput.WriteLine("dism /mount-image /imagefile:install.wim /Index:1 /MountDir:Image");
                cmd.StandardInput.WriteLine("dism /image:Image /add-driver /driver:system32 /recurse /forceunsigned");
                cmd.StandardInput.WriteLine("dism /unmount-wim /mountdir:Image /commit");
                //cmd.StandardInput.WriteLine("dism /mount-image /imagefile:install.wim /Index:1 /MountDir:Image");
                cmd.StandardInput.Flush();
                cmd.StandardInput.Close();
                cmd.WaitForExit();*/

                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
            }
        }

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

        private void createSCCardPartitions() {

            Process cmd = new Process();
            cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "diskpart.exe");
            cmd.StartInfo.Verb = "runas";

            string[] diskPartArgs = new string[3];
            diskPartArgs[0] = "select disk " + selectedDisk.diskNumber;
            diskPartArgs[1] = "create partition primary size=400";
            diskPartArgs[2] = "select partition 1";
            diskPartArgs[3] = "active";
            diskPartArgs[4] = "format fs=fat32 quick label=BOOT";
            diskPartArgs[5] = "assign letter=P";
            diskPartArgs[6] = "select disk " + selectedDisk.diskNumber;
            diskPartArgs[7] = "create partition primary";
            diskPartArgs[8] = "select partition 2";
            diskPartArgs[9] = "active";
            diskPartArgs[10] = "format fs=ntfs quick label=Windows";
            diskPartArgs[11] = "assign letter=I";
            diskPartArgs[12] = "exit";


            foreach (string arg in diskPartArgs)
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
                
            }
            /*cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            //cmd.StartInfo.CreateNoWindow = true;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();

            cmd.StandardInput.WriteLine("cd " + cdPath);
            cmd.StandardInput.WriteLine("dism /mount-image /imagefile:install.wim /Index:1 /MountDir:Image");
            cmd.StandardInput.WriteLine("dism /image:Image /add-driver /driver:system32 /recurse /forceunsigned");
            cmd.StandardInput.WriteLine("dism /unmount-wim /mountdir:Image /commit");
            //cmd.StandardInput.WriteLine("dism /mount-image /imagefile:install.wim /Index:1 /MountDir:Image");
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            cmd.WaitForExit();*/
        }

        private void createWindowsOnRaspiInstallerImageFile() {
            /*long diskUEFISize = 300 * 1024 * 1024; //300MB
            long diskWindowsSize = 16 * 1024 * 1024 * 1024; //8GB
            using (Stream vhdStream = File.Create(System.IO.Path.Combine(appPath, "temp", "windowsOnRaspi.vhd")))
            {
                Disk UEFIDisk = Disk.Initialize(vhdStream, Ownership.Dispose, diskUEFISize);
                Disk WindowsDisk = Disk.Initialize(vhdStream, Ownership.Dispose, diskWindowsSize);
                BiosPartitionTable.Initialize(UEFIDisk, WellKnownPartitionType.WindowsFat);
                BiosPartitionTable.Initialize(WindowsDisk, WellKnownPartitionType.WindowsNtfs);
                VolumeManager vm = new VolumeManager();
                vm.AddDisk(UEFIDisk);
                vm.AddDisk(WindowsDisk);

                using (FatFileSystem fs = FatFileSystem.FormatPartition(UEFIDisk, 0, "BOOT"))
                {
                    //fs.CreateDirectory(@"TestDir\CHILD");
                    // do other things with the file system...
                    
                    //var uefiPath = System.IO.Path.Combine(appPath, "temp", "UEFI");
                    //fs.MoveFile(uefiPath, fs.Root.ToString());
                    //fs.CreateDirectory(uefiPath);
                    
                }

                using (NtfsFileSystem fs = NtfsFileSystem.Format(vm.GetLogicalVolumes()[1], "Windows"))
                {

                    //fs.CreateDirectory(@"externalFiles");
                    
                }
            }*/
        }

        private void cleanUp() {

        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern int GetDriveType(string lpRootPathName);

        private static bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void CopyFilesRecursively(DirectoryInfo source, DirectoryInfo target)
        {
            foreach (DirectoryInfo dir in source.GetDirectories())
                CopyFilesRecursively(dir, target.CreateSubdirectory(dir.Name));
            foreach (FileInfo file in source.GetFiles())
                file.CopyTo(System.IO.Path.Combine(target.FullName, file.Name));
        }

        private static void ExecuteCommand(string command)
        {
            var processInfo = new ProcessStartInfo("cmd.exe", "/c " + command);
            processInfo.CreateNoWindow = true;
            processInfo.UseShellExecute = false;
            processInfo.RedirectStandardError = true;
            processInfo.RedirectStandardOutput = true;

            var process = Process.Start(processInfo);

            process.OutputDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine("output>>" + e.Data);
            process.BeginOutputReadLine();

            process.ErrorDataReceived += (object sender, DataReceivedEventArgs e) =>
                Console.WriteLine("error>>" + e.Data);
            process.BeginErrorReadLine();

            process.WaitForExit();

            Console.WriteLine("ExitCode: {0}", process.ExitCode);
            process.Close();
        }
    }
}
