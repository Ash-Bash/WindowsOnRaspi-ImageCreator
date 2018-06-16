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
using System.Management.Automation;
using System.Runtime.InteropServices;
using System.Management.Automation.Runspaces;
using System.Reflection;

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

            AdminRelauncher();

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
            statusTextBlock.Text = "";
            imageProgressBar.Value = 0;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";


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
            //addUEFIFilesToBoot();
        }

        // Creates a Folder Structure for Temp Directory
        private async void setupTempFolderStructure() {


            statusTextBlock.Text = "Setting Up Structure";
            imageProgressBar.Value = 5;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

            var createDirTask = Task.Run(() =>
            {
                // Creates Directories for Required Folders+
                Directory.CreateDirectory(tempFolders[0]);
                Directory.CreateDirectory(tempFolders[1]);
                Directory.CreateDirectory(tempFolders[2]);
                Directory.CreateDirectory(tempFolders[3]);
                Directory.CreateDirectory(tempFolders[4]);
            });

            await createDirTask;

            copySourceFiles();
        }

        // Copies Source Files from Zip files to their initial dirs
        private async void copySourceFiles() {
            statusTextBlock.Text = "Extracting Files";
            imageProgressBar.Value = 10;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

            try
            {
                var transferZipsTask = Task.Run(() =>
                {
                    // Extracts all required Zip Files
                    File.Copy(winImagePath, System.IO.Path.Combine(tempFolders[1], "Windows10-Arm64.iso"));
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

                    return true;
                });

                await transferZipsTask;

                statusTextBlock.Text = "Copying Install.Wim files from ISO";
                imageProgressBar.Value = 20;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

                copyInstallWimFile();

            } catch (IOException err)
            {
                MessageBox.Show("Something went wrong" + System.Environment.NewLine + "IOException source: {0}", err.Source);
            }
        }

        // Copies the install.wim file from the ISO File (Still Needs work on)
        private async void copyInstallWimFile() {
            DirectoryInfo extrFolders = new DirectoryInfo(tempFolders[1]);
            FileInfo[] fileInfo = extrFolders.GetFiles();

            Debug.WriteLine(System.IO.Path.Combine(tempFolders[1], fileInfo[0].ToString()));
            var isoPath = System.IO.Path.Combine(tempFolders[1], fileInfo[0].ToString());
            string driveLetter = null;

            var isoMountTask = Task.Run(() => { 
                using (var ps = PowerShell.Create())
                {
                    var command = ps.AddCommand("Mount-DiskImage");
                    command.AddParameter("ImagePath", isoPath);
                    command.Invoke();
                    ps.Commands.Clear();

                    //Get Drive Letter ISO Image Was Mounted To
                    var runSpace = ps.Runspace;
                    var pipeLine = runSpace.CreatePipeline();
                    var getImageCommand = new Command("Get-DiskImage");
                    getImageCommand.Parameters.Add("ImagePath", isoPath);
                    pipeLine.Commands.Add(getImageCommand);
                    pipeLine.Commands.Add("Get-Volume");

                    foreach (PSObject psObject in pipeLine.Invoke())
                    {
                        if (psObject != null)
                        {
                            driveLetter = psObject.Members["DriveLetter"].Value.ToString();
                            Console.WriteLine("Mounted On Drive: " + driveLetter);
                        }
                    }
                }
            });

            await isoMountTask;

            if (File.Exists(driveLetter + @":\sources\install.wim"))
            {
                Debug.WriteLine("File Exists: " + true);
                Debug.WriteLine("File Path: " + driveLetter + @":\sources\install.wim");

                var copyFileTask = Task.Run(() =>
                {
                    string[] dismArgs = new string[3];
                    dismArgs[0] = "/Export-Image /SourceImageFile:" + driveLetter + @":\sources\install.wim /SourceIndex:1 /DestinationImageFile:" + System.IO.Path.Combine(appPath, "temp", "install.wim");

                    Process cmd = new Process();

                    if (Environment.Is64BitOperatingSystem)
                    {
                        cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "dism.exe");
                    }
                    else
                    {
                        cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                    }

                    cmd.StartInfo.Verb = "runas";

                    foreach (string arg in dismArgs)
                    {
                        cmd.StartInfo.Arguments += arg;
                    }

                    cmd.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                    cmd.StartInfo.UseShellExecute = false;
                    cmd.StartInfo.RedirectStandardOutput = true;
                    cmd.EnableRaisingEvents = true;


                    cmd.Start();

                    Console.WriteLine("mountInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());

                    cmd.WaitForExit();
                });

                await copyFileTask;

                statusTextBlock.Text = "Copying Raspberry Pi Package Files";
                imageProgressBar.Value = 30;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
                copyRaspiPackages();

            }
            else {
                Debug.WriteLine("File Exists: " + false);
            }
        }

        // Copies Provided packages to their right dirs ready for processing for the next few steps
        private async void copyRaspiPackages() {


            var packagesTask = Task.Run(() =>
            {
                var UEFIPath = System.IO.Path.Combine(tempFolders[3], "RaspberryPiPkg", "Binary", "prebuilt");
                var UEFIFilePaths = Directory.GetDirectories(UEFIPath);
                Debug.WriteLine("UEFI Files Paths: " + UEFIFilePaths);
                CopyFilesRecursively(new DirectoryInfo(System.IO.Path.Combine(UEFIFilePaths[UEFIFilePaths.Length - 1], "DEBUG")), new DirectoryInfo(tempFolders[0]));

                var driverPath = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "driver_prebuilts");
                CopyFilesRecursively(new DirectoryInfo(driverPath), new DirectoryInfo(tempFolders[2]));

                var driver2Path = System.IO.Path.Combine(tempFolders[3], "winOnRaspi", "winpe_stuff");
                CopyFilesRecursively(new DirectoryInfo(driver2Path), new DirectoryInfo(tempFolders[2]));
            });

            await packagesTask;

            statusTextBlock.Text = "Mounting Install.Wim File to Image Folder";
            imageProgressBar.Value = 45;
            percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";

            MountInstallWimFile();
        }

        // Mounts install.wim file to Image Dir
        private void MountInstallWimFile()
        {
            var wimpath = System.IO.Path.Combine(appPath, "temp", "install.wim");
            var cdPath = System.IO.Path.Combine(appPath, "temp");

            string[] dismArgs = new string[3];
            dismArgs[0] = "/mount-image /imagefile:install.wim /Index:1 /MountDir:Image";

            //if () 
            if (File.Exists(wimpath))
            {
                Debug.WriteLine("install.wim Path Exists: " + true);


                /*var mountWimTask = Task.Run(() =>
                {
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

                    Console.WriteLine("mountInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());

                    cmd.WaitForExit();
                });

                await mountWimTask;*/

                Process cmd = new Process();
                cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
                //cmd.StartInfo.WorkingDirectory = cdPath;
                cmd.StartInfo.Verb = "runas";

                /*foreach (string arg in dismArgs)
                {
                    cmd.StartInfo.Arguments += arg;
                }*/

                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.StartInfo.RedirectStandardError = true;
                cmd.EnableRaisingEvents = true;

                cmd.Start();
                cmd.StandardInput.WriteLine("/mount-image /imagefile:install.wim /Index:1 /MountDir:Image");

                Console.WriteLine("mountInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());
                var results = cmd.StandardOutput.ReadToEnd();

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    if (Directory.GetFiles(System.IO.Path.Combine(appPath, "temp", "Image")).Length > 0)
                    {
                        statusTextBlock.Text = "Adding Driver Files to Image";
                        imageProgressBar.Value = 50;
                        percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
                        addDriversInstallWimFile();
                    }
                    else
                    {
                        MessageBox.Show("Couldn't Mount image.wim to Image Folder", "Error!", MessageBoxButton.OK);
                        cleanUp(false);
                    }
                }

                /*statusTextBlock.Text = "Adding Driver Files to Image";
                imageProgressBar.Value = 50;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
                addDriversInstallWimFile();*/


                Debug.WriteLine("CD Path: " + cdPath);
            }
            else
            {
                Debug.WriteLine("install.wim Path Exists: " + false);
                MessageBox.Show("install.wim file doesnt exist", "Error!", MessageBoxButton.OK);
                cleanUp(false);
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

                Console.WriteLine("addDriversInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    statusTextBlock.Text = "Committing Install.Wim changes & unmounting";
                    imageProgressBar.Value = 64;
                    percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
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

                Console.WriteLine("unmountInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    statusTextBlock.Text = "Creating SD Card Partitions";
                    imageProgressBar.Value = 72;
                    percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
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

            Console.WriteLine("createSCCardPartitions() Process: " + cmd.StandardOutput.ReadToEnd());

            cmd.WaitForExit();
            if (cmd.HasExited)
            {
                statusTextBlock.Text = "Adding Files from Install.Wim File to I:/ (Windows)";
                imageProgressBar.Value = 82;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
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
                    statusTextBlock.Text = "Adding UEFI Files to P:/ (Boot)";
                    imageProgressBar.Value = 90;
                    percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
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
        private async void addUEFIFilesToBoot()
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

                string[] bcdArgs = new string[3];
                bcdArgs[0] = "bcdboot I:\\Windows /s P: /f UEFI";

                Process cmd = new Process();
                cmd.StartInfo.FileName = "cmd.exe";
                cmd.StartInfo.WorkingDirectory = "c:/";
                cmd.StartInfo.Verb = "runas";
                cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
                cmd.StartInfo.UseShellExecute = false;
                cmd.StartInfo.RedirectStandardError = true;
                cmd.StartInfo.RedirectStandardOutput = true;
                cmd.StartInfo.RedirectStandardInput = true;
                cmd.EnableRaisingEvents = true;



                //cmd.StartInfo.Arguments = String.Join(" ", bcdArgs);
                string parameters = String.Format("/k \"{0}\"", path);
                //cmd = Process.Start(path);
                cmd.Start();

                cmd.StandardInput.WriteLine(path);

                Console.WriteLine(cmd.StandardOutput.ReadToEnd());

                cmd.WaitForExit();

                if (cmd.HasExited)
                {
                    statusTextBlock.Text = "Siging Windows Files";
                    imageProgressBar.Value = 95;
                    percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
                    signWindowsFiles();
                    cmd.Close();
                }
            }
        }

        // Signs Windows Files in the UEFI Partition  (Still Needs work on)
        private void signWindowsFiles() {
            Debug.WriteLine(System.IO.Path.Combine(appPath, "temp", "SignUEFIFiles.cmd"));
            var path = System.IO.Path.Combine(appPath, "temp", "SignUEFIFiles.cmd");

            string[] bcdArgs = new string[3];
            bcdArgs[0] = "bcdedit /store P:\\EFI\\Microsoft\\Boot\\bcd /set {default} testsigning on";
            bcdArgs[1] = "bcdedit /store P:\\EFI\\Microsoft\\Boot\\bcd /set {default} nointegritychecks on";

            Process cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.WorkingDirectory = "c:/";
            cmd.StartInfo.Verb = "runas";
            cmd.StartInfo.WindowStyle = ProcessWindowStyle.Normal;
            cmd.StartInfo.UseShellExecute = false;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.EnableRaisingEvents = true;

            //cmd.StartInfo.Arguments = String.Join(" ", bcdArgs);

            cmd.Start();
            cmd.StandardInput.WriteLine(path);

            Console.WriteLine(cmd.StandardOutput.ReadToEnd());

            cmd.WaitForExit();

            if (cmd.HasExited)
            {
                statusTextBlock.Text = "Cleaning Up";
                imageProgressBar.Value = 100;
                percentageTextBlock.Text = imageProgressBar.Value.ToString() + "%";
                cleanUp(true);
            }
        }

        // Deletes Temp dirs and Files and Unmounts any Images that were mounted when installing files on the SD Card (Still Needs work on)
        private async void cleanUp(bool wasSuccess) {
            DirectoryInfo extrFolders = new DirectoryInfo(tempFolders[1]);
            FileInfo[] fileInfo = extrFolders.GetFiles();

            var isoPath = System.IO.Path.Combine(tempFolders[1], fileInfo[0].ToString());
            var isoMountTask = Task.Run(() => {
                using (var ps = PowerShell.Create())
                {
                    //Unmount Via Image File Path
                    var command = ps.AddCommand("Dismount-DiskImage");
                    command.AddParameter("ImagePath", isoPath);
                    ps.Invoke();
                    ps.Commands.Clear();
                }
            });

            await isoMountTask;

            var unmountWimTask = Task.Run(() => {
                using (var ps = PowerShell.Create())
                {
                    string[] dismArgs = new string[3];
                    dismArgs[0] = "/cleanup-wim";

                    Process cmd = new Process();
                    cmd.StartInfo.FileName = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "dism.exe");
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

                    Console.WriteLine("unmountInstallWimFile() Process: " + cmd.StandardOutput.ReadToEnd());

                    cmd.WaitForExit();
                }
            });

            await unmountWimTask;

            var deleteFilesTask = Task.Run(() =>
            {
                System.IO.DirectoryInfo di = new DirectoryInfo(System.IO.Path.Combine(appPath, "temp"));

                foreach (FileInfo file in di.GetFiles())
                {
                    file.Delete();
                }
                foreach (DirectoryInfo dir in di.GetDirectories())
                {
                    dir.Delete(true);
                }
            });

            await deleteFilesTask;

            if (wasSuccess)
            {
                CompletedProcess();
            } else
            {
                RollbackProcess();
            }
        }

        public void CompletedProcess() {
            MessageBox.Show("Successfully Copied Files & Data to The SD Card Ready for Raspberry Pi 3 B", "Successfully Completed");
        }

        public void RollbackProcess() {
            MessageBox.Show("Successfully Rollbacked Changes", "Rollback Completed");
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
            catch (Exception e){
                Debug.WriteLine("Somewthing Went Wrong!! " + e);
            }
        }

        private void AdminRelauncher()
        {
            if (!IsRunAsAdmin())
            {
                ProcessStartInfo proc = new ProcessStartInfo();
                proc.UseShellExecute = true;
                proc.WorkingDirectory = Environment.CurrentDirectory;
                proc.FileName = Assembly.GetEntryAssembly().CodeBase;

                proc.Verb = "runas";

                try
                {
                    Process.Start(proc);
                    Application.Current.Shutdown();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("This program must be run as an administrator! \n\n" + ex.ToString());
                }
            }
        }

        private bool IsRunAsAdmin()
        {
            WindowsIdentity id = WindowsIdentity.GetCurrent();
            WindowsPrincipal principal = new WindowsPrincipal(id);

            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }
}
