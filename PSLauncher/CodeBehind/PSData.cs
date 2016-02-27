using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace PSLauncher.CodeBehind {
    // Class to store all the necessary data for the scripts and functions
    public class PSData {
        public string CurrDir { get; }
        public Loader Load { get; }
        public List<Category> Categories { get; }
        public Settings AppSet { get; }

        // Constructor
        public PSData(string currDir) {
            CurrDir = currDir;
            Load = new Loader(CurrDir);

            // Load the categories if the config is valid and loaded
            if (Load.ValidConfig) {
                Categories = Load.LoadHelp();
                AppSet = Load.LoadSettings();
            }
            else {
                Categories = new List<Category>();
                AppSet = new Settings();
                MessageBox.Show(Load.ValidConfigDetail, "Error: Loading configuration");
            }
        }
    }
}
