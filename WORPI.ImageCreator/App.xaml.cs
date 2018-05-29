using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;
using System.Security.Principal;

namespace WORPI.ImageCreator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App() {
            this.InitializeComponent();

            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "WinOnRaspi Image Creator", "temp");

            if (Directory.Exists(path))
            {
                //Exists
            }
            else
            {
                //Needs to be created
                Directory.CreateDirectory(path);
            }
        } 

    }
}
