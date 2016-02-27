using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace PSLauncher.CodeBehind {
    public class Settings {
        public string BackgroundMain { get; set; }
        public SolidColorBrush BackgroundMainBrush { get; set; }
        public string Title { get; set; }
        public string Logo { get; set; }
        public string HostProperties { get; set; }      // script re-direct to set up the PS host window
        public string HostTitle { get; set; }           // used if script redirect is included
        public string UserSettings { get; set; }     
        //start individual settings
        public string UserPath { get; set; }

        public Settings() {
            // Establish settings defaults
            BackgroundMain = "#4eadd7";        // hexadecimal
            BackgroundMainBrush = (SolidColorBrush)(new BrushConverter()).ConvertFrom(BackgroundMain);
            Title = "PowerShell Launcher";
            Logo = "";
            HostProperties = "";
            HostTitle = "";
            UserSettings = "0";           // binary
            UserPath = "";  
        }

        public void SetBackgroundBrush(string hex) {
            BackgroundMain = hex;
            BackgroundMainBrush = (SolidColorBrush)(new BrushConverter()).ConvertFrom(BackgroundMain);
        }
    }
}
