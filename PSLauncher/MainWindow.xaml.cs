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
using PSLauncher.CodeBehind;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using System.Management.Automation.Runspaces;
using System.Diagnostics;
using System.Xml;
using System.Xml.Serialization;

namespace PSLauncher {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            // Set window properties based on loaded settings
            Background = psData.AppSet.BackgroundMainBrush;
            authBox.Background = psData.AppSet.BackgroundMainBrush;
            statusPanel.Background = psData.AppSet.BackgroundMainBrush;
            Title = psData.AppSet.Title;
            if (psData.AppSet.Logo != "") {
                ImageSourceConverter imgs = new ImageSourceConverter();
                logoImage.SetValue(Image.SourceProperty, imgs.ConvertFromString(psData.AppSet.Logo));
            }

            // Establish event handlers
            this.Closed += new EventHandler(MainWindow_Closed);
            runButton.Click += RunButton_Click;
            clearButton.Click += ClearButton_Click;
            refreshButton.Click += RefreshButton_Click;
            scriptBox.SelectionChanged += ScriptBox_SelectionChanged;
            
            // Set up the runspace with all the necessary modules
            runspace.Open();
            if (moduleList.Count > 0) {
                foreach (string mod in moduleList) {
                    Collection<PSObject> importResults = invoker.Invoke(String.Format("Import-Module '{0}'", mod));
                }
            }

            // Set up the observable collection from the PSData object
            foreach (Category cat in psData.Categories) {
                Categories.Add(cat);
            }

            // Set the first selected item
            comboBox.SelectedIndex = 0;
        }

        //***
        // Objects to be loaded in memory for use throughout the application
        public static string currDir = System.IO.Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
        public static PSData psData = new PSData(currDir);
        public static Param param = psData.Load.Parameter;
        public static List<string> moduleList = psData.Load.ModuleList;
        // Set up a runspace for executing commands
        public static InitialSessionState initial = InitialSessionState.CreateDefault();
        public static Runspace runspace = RunspaceFactory.CreateRunspace(initial);
        public static RunspaceInvoke invoker = new RunspaceInvoke(runspace);
        // Category information to bind combo box selections and items in the list box
        private ObservableCollection<Category> catColl = new ObservableCollection<Category>();
        public ObservableCollection<Category> Categories {
            get { return catColl; }
            set { catColl = value; }
        }
        //***

        // Event for running a command
        private void RunButton_Click(object sender, RoutedEventArgs e) {
            if (scriptBox.SelectedItem != null) {
                outBox.Document.Blocks.Clear();

                string exec = ((HelpObj)scriptBox.SelectedItem).FullName;
                string format = ((HelpObj)scriptBox.SelectedItem).Formatting;
                string execParam = param.CreateParamList((HelpObj)scriptBox.SelectedItem, new string[] { inputBox1.Text, inputBox2.Text, inputBox3.Text, inputBox4.Text });
                
                // If an invalid parameter is found, display the message. Otherwise execute the command
                string[] paramCheck = execParam.Split('~');
                if (paramCheck[0] == "i") {
                    for (int i = 1; i < paramCheck.Length; i++) {
                        outBox.AppendText(String.Format("{0}\n\n", paramCheck[i]));
                    }
                }
                else if (!exec.EndsWith(".ps1")) {
                    // Build the final format commands determined from the help
                    if (!format.StartsWith("<n")) {
                        format = String.Format("|{0} |Out-String", format);
                    }
                    else {
                        format = "|Out-String";
                    }

                    string execString = String.Format("{0}{1}", paramCheck[1], format);

                    using (PowerShell ps = PowerShell.Create()) {
                        ps.Runspace = runspace;
                        ps.AddScript(execString);

                        // Catch any errors thrown when invoking the powershell command
                        try {
                            Collection<PSObject> results = ps.Invoke();
                            // Display the results
                            foreach (PSObject obj in results) {
                                outBox.AppendText(obj.BaseObject.ToString());
                            }

                            // Display any errors returned from the powershell command
                            if (ps.Streams.Error.Count > 0) {
                                Collection<ErrorRecord> errors = ps.Streams.Error.ReadAll();
                                string errorString = "";
                                foreach (ErrorRecord er in errors) {
                                    errorString += String.Format("{0}\n>{1}\n>{2}\n>{3}\n\n", er.Exception.GetType(), er.Exception.Message, er.Exception.Source, er.Exception.TargetSite);
                                }
                                MessageBox.Show(errorString, "Error: PowerShell Execution");
                            }
                        }
                        catch (Exception exep) {
                            MessageBox.Show(String.Format("{0}\n\n{1}", exep.GetType(), exep.Message), "Error: Launching Command");
                        }
                    }

                    // Store the command for refreshing later
                    refreshGhostBox.Text = String.Format("{0}", execString);
                }
                else {
                    string pCheck = paramCheck[1];

                    // Prepend host properties script if specified in settings
                    if (psData.AppSet.HostProperties != "") {
                        if (psData.AppSet.HostProperties == "") {
                            pCheck = String.Format("'{0}'; & {1}", psData.AppSet.HostProperties, pCheck);
                        }
                        else {
                            // If listed in the Settings, add a hostTitle parameter and pass in script name
                            pCheck = String.Format("'{0}' -{1} '{2}'; & {3}", psData.AppSet.HostProperties, psData.AppSet.HostTitle, ((HelpObj)scriptBox.SelectedItem).Title, pCheck);
                        }

                    }

                    string sBlock = "{& " + String.Format("{0}", pCheck) + "}";
                    string execString = String.Format("Start-Process Powershell.exe -ArgumentList {0}", sBlock);
                    invoker.Invoke(execString);
                    
                    // Store the command for refreshing later
                    refreshGhostBox.Text = String.Format("{0}.ps1", execString);
                }

                // Clear the input boxes in preparation for the next run
                inputBox1.Clear();
                inputBox2.Clear();
                inputBox3.Clear();
                inputBox4.Clear();
            }
        }

        // Event for clearing the input boxes
        private void ClearButton_Click(object sender, RoutedEventArgs e) {
            inputBox1.Clear();
            inputBox2.Clear();
            inputBox3.Clear();
            inputBox4.Clear();
        }

        // Event for re-reunning the last successful command
        private void RefreshButton_Click(object sender, RoutedEventArgs e) {
            outBox.Document.Blocks.Clear();
            string exec = refreshGhostBox.Text;

            if (exec == null) {
                outBox.AppendText("No valid command!");
            }
            else if (!exec.EndsWith(".ps1")) {
                using (PowerShell ps = PowerShell.Create()) {
                    ps.Runspace = runspace;
                    ps.AddScript(exec);

                    // Catch any errors thrown when invoking the powershell command
                    try {
                        Collection<PSObject> results = ps.Invoke();
                        // Display the results
                        foreach (PSObject obj in results) {
                            outBox.AppendText(obj.BaseObject.ToString());
                        }

                        // Display any errors returned from the powershell command
                        if (ps.Streams.Error.Count > 0) {
                            Collection<ErrorRecord> errors = ps.Streams.Error.ReadAll();
                            string errorString = "";
                            foreach (ErrorRecord er in errors) {
                                errorString += String.Format("{0}\n>{1}\n>{2}\n>{3}\n\n", er.Exception.GetType(), er.Exception.Message, er.Exception.Source, er.Exception.TargetSite);
                            }
                            MessageBox.Show(errorString, "Error: PowerShell Execution");
                        }
                    }
                    catch (Exception exep) {
                        MessageBox.Show(String.Format("{0}\n\n{1}", exep.GetType(), exep.Message), "Error: Launching Command");
                    }
                }
            }
            else {
                exec = exec.Substring(0, (exec.Length - 4));
                invoker.Invoke(exec);
            }

            // Clear the input boxes in preparation for the next run
            inputBox1.Clear();
            inputBox2.Clear();
            inputBox3.Clear();
            inputBox4.Clear();
        }

        // Cleanup code to run upon the window being closed
        private void MainWindow_Closed(object sender, EventArgs e) {
            try {
                // Ignore any errors that might occur while closing the runspace
                runspace.Close();
            }
            catch { }
        }

        // Script Box selection changed event
        private void ScriptBox_SelectionChanged(object sender, RoutedEventArgs e) {
            List<ParamObj> selParam;
            if (scriptBox.SelectedItem != null) {
                selParam = ((HelpObj)scriptBox.SelectedItem).Args;
            }
            else {
                selParam = param.ConvertParam(new string[0]);
            }

            // Update the display, focusability, and readOnly attributes for each input box according to the associated label
            if (selParam[0].Label == "") {
                inputBox1.Visibility = Visibility.Hidden;
                inputBox1.Focusable = false;
                inputBox1.IsReadOnly = true;
            }
            else {
                inputBox1.Visibility = Visibility.Visible;
                inputBox1.Focusable = true;
                inputBox1.IsReadOnly = false;
            }
            if (selParam[1].Label == "") {
                inputBox2.Visibility = Visibility.Hidden;
                inputBox2.Focusable = false;
                inputBox2.IsReadOnly = true;
            }
            else {
                inputBox2.Visibility = Visibility.Visible;
                inputBox2.Focusable = true;
                inputBox2.IsReadOnly = false;
            }
            if (selParam[2].Label == "") {
                inputBox3.Visibility = Visibility.Hidden;
                inputBox3.Focusable = false;
                inputBox3.IsReadOnly = true;
            }
            else {
                inputBox3.Visibility = Visibility.Visible;
                inputBox3.Focusable = true;
                inputBox3.IsReadOnly = false;
            }
            if (selParam[3].Label == "") {
                inputBox4.Visibility = Visibility.Hidden;
                inputBox4.Focusable = false;
                inputBox4.IsReadOnly = true;
            }
            else {
                inputBox4.Visibility = Visibility.Visible;
                inputBox4.Focusable = true;
                inputBox4.IsReadOnly = false;
            }
        }
    }
}
