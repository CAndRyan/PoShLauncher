using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Xml;
using System.Xml.Serialization;
using System.Windows;
using System.Windows.Media;

namespace PSLauncher.CodeBehind {
    // Used to load and create files upon launching the application
    public class Loader {
        string CurrDir { get; }
        public string Path { get; }
        public string PathVal { get; }
        public string HelpFile { get; }
        public string SetPath { get; }
        public bool ValidConfig { get; }
        public string ValidConfigDetail { get; }
        public Param Parameter { get; }
        public List<string> ModuleList { get; }

        // Constructor to build constant file paths from the executable directory
        public Loader(string currDir) {
            CurrDir = currDir;
            Path = CurrDir + Constants.config;
            PathVal = Path + Constants.paramVal;
            HelpFile = Path + Constants.help;
            SetPath = Path + Constants.settings;
            ModuleList = new List<string>();

            // Check configuration validity
            ValidConfigDetail = Config();
            if (ValidConfigDetail.StartsWith("F")) {
                ValidConfig = false;
            }
            else {
                ValidConfigDetail = "Success";
                ValidConfig = true;
            }

            Parameter = new Param(CurrDir);     // Needs to happen after Loader.Config() has run so files are generated
        }

        // Method to check existence of configuration directories and files
        private string Config() {
            string modPath = CurrDir + Constants.modPath;
            string scrPath = CurrDir + Constants.scrPath;
            int maxBack = Constants.maxHelpBackup;
            string returnStr = "";

            // Check for existence of the Config directory and create if needed
            if (!Directory.Exists(Path)) {
                try {
                    Directory.CreateDirectory(Path);
                }
                catch {
                    returnStr += String.Format("Failed to create {0}\n", Path);
                }
            }

            // Check for existence of the validation file and create if needed
            if (!File.Exists(PathVal)) {
                try {
                    FileStream newFile = File.Create(PathVal);
                    newFile.Close();
                    //File.Create(PathVal);
                }
                catch {
                    returnStr += String.Format("Failed to create {0}\n", PathVal);
                }
            }

            // Check existence of settings file and create default one if not there
            if (!File.Exists(SetPath)) {
                try {
                    FileStream newFile = File.Create(SetPath);
                    StreamWriter writer = new StreamWriter(newFile);
                    writer.WriteLine("Default,1");
                    writer.WriteLine("BackgroundMain,");
                    writer.WriteLine("Title,");
                    writer.WriteLine("Logo,");
                    writer.WriteLine("HostProperties,");
                    writer.WriteLine("HostTitle,");
                    writer.WriteLine("UserSettings,0");
                    writer.WriteLine("UserPath,");
                    writer.Close();
                }
                catch {
                    returnStr += String.Format("Failed to write to {0}\n", SetPath);
                }
            }

            // Check for existence of the help file and create if needed or is invalid (empty, bad entries)
            bool validFile = false;
            while (!validFile) {
                if (!File.Exists(HelpFile)) {
                    try {
                        FileStream newFile = File.Create(HelpFile);
                        if (!Directory.Exists(modPath)) {
                            Directory.CreateDirectory(modPath);
                        }
                        if (!Directory.Exists(scrPath)) {
                            Directory.CreateDirectory(scrPath);
                        }
                        StreamWriter writer = new StreamWriter(newFile);
                        writer.WriteLine(String.Format("Function,{0},*.psm1", modPath));
                        writer.WriteLine(String.Format("Scripts,{0},*.ps1", scrPath));
                        writer.Close();

                        validFile = true;
                    }
                    catch {
                        returnStr += String.Format("Failed to write to {0}\n", HelpFile);
                    }
                }
                else if (new FileInfo(HelpFile).Length == 0) {        // check if help file is empty
                    try {
                        File.Delete(HelpFile);
                    }
                    catch {
                        returnStr += String.Format("Failed to delete empty {0}\n", HelpFile);
                    }
                }
                else {        // check if help file has only valid entries
                    StreamReader testFile = new StreamReader(HelpFile);
                    string testLine;
                    int tested = 0;
                    int totalLines = 0;
                    while ((testLine = testFile.ReadLine()) != null) {
                        string[] info = testLine.Split(',');
                        totalLines++;
                        if (info.Length == 3) {
                            tested++;
                        }
                    }
                    if (tested == totalLines) {
                        validFile = true;
                    }
                    else {
                        try {
                            string backup = "";
                            for (int k = 1; k < maxBack + 1; k++) {
                                backup = String.Format("{0}_{1}.csv", HelpFile.Substring(0, (HelpFile.Length - 4)), k);
                                if (!File.Exists(backup)) {     // use this number to backup to
                                    k = maxBack + 1;
                                }
                                if (k == maxBack) {     // start deleting final backup if reached
                                    File.Delete(backup);
                                }
                            }
                            File.Move(HelpFile, backup);
                            File.Delete(HelpFile);
                        }
                        catch {
                            returnStr += String.Format("Failed to create backup for {0}\n", HelpFile);
                        }
                    }
                }
            }

            return returnStr;
        }

        // Method to load the help data
        public List<Category> LoadHelp() {
            PSInterface psi = new PSInterface(CurrDir);
            // Extract help directories
            List<string> cmds = new List<string>();
            List<string> helpCat = new List<string>();
            List<string[]> helpDir = new List<string[]>();
            List<List<Help>> helpData = new List<List<Help>>();

            List<Category> Categories = new List<Category>();       // for storing the data to be placed the observable collection

            StreamReader file = new StreamReader(HelpFile);
            string line;
            while ((line = file.ReadLine()) != null) {
                string[] lineTxt = line.Split(',');
                helpCat.Add(lineTxt[0]);
                helpDir.Add(lineTxt);
            }
            file.Close();
            // Clean up the help categories of duplicates
            bool clean = false;
            while (!clean) {
                int toRemove = 0;
                for (int k = 0; k < helpCat.Count; k++) {
                    if (k != 0) {
                        if (helpCat[k] == helpCat[k - 1]) {
                            toRemove = k;
                            k = helpCat.Count;
                        }
                    }

                    // Exit loop if all have been tested
                    if (k == helpCat.Count - 1) {
                        clean = true;
                    }
                }

                if (toRemove != 0) {
                    helpCat.RemoveAt(toRemove);
                }
            }

            foreach (string[] dir in helpDir) {
                foreach (string cat in helpCat) {
                    if (dir[0] == cat) {
                        List<Help> data = new List<Help>();
                        string cache = String.Format("{0}{1}_Cache.xml", Path, cat);

                        // retreive the full path names of scripts and modules
                        string[] scriptPaths = Directory.GetFiles(dir[1], dir[2]);
                        foreach (string script in scriptPaths) {
                            cmds.Add(script.Replace(' ', '~'));
                        }

                        // If a modules is found add it to the modules list for the executing runspace
                        foreach (string cmd in cmds) {
                            if (cmd.EndsWith(".psm1")) {
                                ModuleList.Add(String.Format("{0}", cmd.Replace('~', ' ')));
                            }
                        }

                        // Check for existance of a help cache. Deserialize the xml if found. Generate if not
                        if (File.Exists(cache)) {
                            XmlSerializer serializer = new XmlSerializer(typeof(List<Help>));
                            using (XmlReader reader = XmlReader.Create(cache)) {
                                data = (List<Help>)serializer.Deserialize(reader);
                            }
                        }
                        else {
                            // Extract the help data and place in memory
                            data = psi.GetHelp(cmds);

                            // Serialize the retrieved help data
                            XmlSerializer serializer = new XmlSerializer(data.GetType());
                            using (XmlWriter writer = XmlWriter.Create(cache)) {
                                serializer.Serialize(writer, data);
                            }
                        }

                        List<HelpObj> dataObj = new List<HelpObj>();
                        helpData.Add(data);
                        foreach (Help help in data) {
                            // check for a null object
                            if (help != null) {
                                // Check for any errors and display them
                                if (help.GetName() == "Error") {
                                    string[] paramAry = help.GetParam();
                                    string errorMsg = "";
                                    for (int i = 0; i < paramAry.Length; i++) {
                                        errorMsg += paramAry[i] + "\n\n";
                                    }
                                    MessageBox.Show(errorMsg, "Error: Loading Help");
                                }
                                // Seperate functions from scripts
                                if (help.GetCategory() == "ExternalScript") {
                                    dataObj.Add(new HelpObj { Title = help.GetSynopsis(), Description = help.GetDetail(), Meta = help.GetMeta(), FullName = help.GetName(), Args = Parameter.ConvertParam(help.GetParam()), Formatting = help.GetFormat() });
                                }
                                else if (help.GetCategory() == "Function") {
                                    dataObj.Add(new HelpObj { Title = help.GetName(), Description = help.GetDetail(), Meta = help.GetMeta(), FullName = help.GetName(), Args = Parameter.ConvertParam(help.GetParam()), Formatting = help.GetFormat() });
                                }
                            }
                        }
                        cmds.Clear();
                        Categories.Add(new Category { Name = cat, Objects = dataObj });
                    }
                }
            }
            
            return Categories;
        }

        // Method to load the settings file (create if needed) to generate Settings object
        public Settings LoadSettings() {
            Settings set = new Settings();
            bool iSettingReached = false;

            // Load the Settings file
            StreamReader file = new StreamReader(SetPath);
            string line;
            while ((line = file.ReadLine()) != null) {
                string[] lineTxt = line.Split(',');
                // Clean up white space from each element
                for (int i = 0; i < lineTxt.Length; i++) {
                    lineTxt[i] = lineTxt[i].Trim();
                }

                // Handle default settings check
                if (lineTxt[0] == "Default") {
                    if (lineTxt.Length > 1) {
                        if (lineTxt[1] == "1") {        // if default is true, break loop and use default settings
                            break;
                        }
                        else if (lineTxt[1] != "0") {       // if default is not true and not false (wrong value), break loop...
                            break;
                        }
                    }
                    else {      // if default value is not set, break loop and use default settings
                        break;
                    }
                }
                else if (iSettingReached && set.UserSettings == "0") {    // break if individual setting was reached and set to false
                    break;      // leaves defaults for additional settings (individual settings)
                }
                else {      // Load additional settings, modify default if included in file
                    if (lineTxt.Length > 1) {
                        if (lineTxt[1] != "") {
                            switch (lineTxt[0]) {
                                case "BackgroundMain":
                                    string bColor = lineTxt[1];

                                    try {
                                        SolidColorBrush brush = (SolidColorBrush)(new BrushConverter()).ConvertFrom(bColor);
                                    }
                                    catch {
                                        bColor = "";
                                        MessageBox.Show("Failed to Load background color. Check the hexadecimal value!", "Error: Generating background");
                                    }
                                    finally {
                                        if (bColor != "") {
                                            set.SetBackgroundBrush(bColor);
                                        }
                                    }
                                    break;
                                case "Title":
                                    set.Title = lineTxt[1];
                                    break;
                                case "Logo":
                                    string logo = lineTxt[1];
                                    // Allow partial paths, relative to the config location
                                    if (!logo.Contains(":")) {
                                        logo = CurrDir + Constants.config + logo;
                                    }
                                    if (File.Exists(logo) && (logo.EndsWith(".jpg") | logo.EndsWith(".png"))) {
                                        set.Logo = logo;
                                    }
                                    else {
                                        MessageBox.Show("Failed to load logo image. Check the file path and ensure it is either a .jpg or .png source.", "Error: Loading logo");
                                    }
                                    break;
                                case "HostProperties":
                                    string host = lineTxt[1];
                                    // Allow partial paths, relative to the UITools location
                                    if (!host.Contains(":")) {
                                        host = CurrDir + Constants.uiTools + host;
                                    }
                                    if (File.Exists(host) && (host.EndsWith(".ps1"))) {
                                        set.HostProperties = host;
                                    }
                                    else {
                                        MessageBox.Show("Failed to find valid host properties script. Check the file path and ensure it is a .ps1 source.", "Error: Locating redirect script");
                                    }
                                    break;
                                case "HostTitle":
                                    set.HostTitle = lineTxt[1];
                                    break;
                                case "UserSettings":
                                    set.UserSettings = lineTxt[1];
                                    iSettingReached = true;
                                    break;
                                case "UserPath":
                                    set.UserPath = lineTxt[1];
                                    break;
                            }
                        }
                    }
                }
                
            }
            file.Close();

            return set;
        }
    }
}
