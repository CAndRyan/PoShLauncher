using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using System.Collections.ObjectModel;
using System.Windows;

namespace PSLauncher.CodeBehind {
    // Help data object
    public class Help {
        public string Name;
        public string Synopsis;
        public string Detail;
        public string Meta;
        public string[] Param;
        public string Role;
        public string Category;
        public string Formatting;

        // Constructor for a Help object
        public Help(string name, string synopsis, string detail, string meta, string[] param, string role, string category, string formatting) {
            this.Name = name;
            this.Synopsis = synopsis;
            this.Detail = detail;
            this.Meta = meta;
            this.Param = param;
            this.Role = role;
            this.Category = category;
            this.Formatting = formatting;
        }

        // Parameterless constructor for serializing/deserializing
        public Help() {
            Name = "";
            this.Synopsis = "";
            this.Detail = "";
            this.Meta = "";
            this.Param = new string[1];
            this.Role = "";
            this.Category = "";
            this.Formatting = "";
        }

        // Methods to return each variable
        public string GetName() {
            return Name;
        }
        public string GetSynopsis() {
            return Synopsis;
        }
        public string GetDetail() {
            return Detail;
        }
        public string GetMeta() {
            return Meta;
        }
        public string[] GetParam() {
            return Param;
        }
        public string GetRole() {
            return Role;
        }
        public string GetCategory() {
            return Category;
        }
        public string GetFormat() {
            return Formatting;
        }
    }

    // Interface object with Powershell
    public class PSInterface {
        string CurrDir;

        // Constructor for the PSInterface class
        public PSInterface(string currDir) {
            this.CurrDir = currDir;
        }

        // Method to call Get-Help through Powershell
        public List<Help> GetHelp(List<string> cmdArray) {
            if (cmdArray.Count != 0) {
                using (PowerShell pipe = PowerShell.Create()) {
                    // add powershell commands to the pipe
                    string commands = cmdArray[0];
                    for (int i = 1; i < cmdArray.Count; i++) {
                        commands += ", " + cmdArray[i];
                    }
                    // Setting the ErrorActionPreference bypasses the security error from Set-ExecutionPolicy. Make sure you can change the Execution Policy!
                    string getHelpScr = CurrDir + Constants.uiTools + Constants.getHelp;
                    string script = String.Format("$ErrorActionPreference = 'SilentlyContinue'; Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force; $ErrorActionPreference = 'Continue'; & \"{1}\" -Commands {0}", commands, getHelpScr);
                    pipe.AddScript(script);

                    // invoke execution on the pipeline (collecting output)
                    Collection<PSObject> PSOutput = new Collection<PSObject>();
                    try {
                        PSOutput = pipe.Invoke();
                    }
                    catch {
                        MessageBox.Show("Failure retrieving Help data!", "Error: PowerShell execution");
                    }
                    List<Help> HelpList = new List<Help>();

                    // loop through each output object item
                    foreach (PSObject item in PSOutput) {
                        string name = "<noName>";
                        string synopsis = "<noSynopsis>";
                        string detail = "<noDetail>";
                        string meta = "<noMeta>";
                        string[] param = new string[0];
                        string role = "<noRole>";
                        string category = "<noCategory>";
                        string formatting = "<noFormat>";
                        // check for a null object
                        if (item != null) {
                            // Generate Help object for each returned PSObject
                            if (item.Properties["Name"].Value != null) {
                                name = item.Properties["Name"].Value.ToString();
                            }
                            if (item.Properties["Synopsis"].Value != null) {
                                synopsis = item.Properties["Synopsis"].Value.ToString();
                            }
                            if (item.Properties["Detail"].Value != null) {
                                string[] allDetail = item.Properties["Detail"].Value.ToString().Split('~');
                                detail = allDetail[0].Trim();
                                if (allDetail.Length == 2) {
                                    meta = allDetail[1].Trim();
                                }
                            }
                            if (item.Properties["Param"].Value != null) {
                                param = item.Properties["Param"].Value.ToString().Split('~');

                                // Append the 't' or 'f' to the parameter to point out if it's mandatory or not
                                string[] paramReq = item.Properties["ParamReq"].Value.ToString().Split('~');
                                for (int i = 0; i < paramReq.Length; i++) {
                                    param[i] += String.Format("~{0}", (paramReq[i])[0]);
                                }

                                // Append the 't' or 'f' to the parameter to point out if it's only single input or multi
                                string[] single = item.Properties["Single"].Value.ToString().Split('~');
                                for (int i = 0; i < single.Length; i++) {
                                    param[i] += String.Format("~{0}", (single[i])[0]);
                                }
                            }
                            if (item.Properties["Role"].Value != null) {
                                role = item.Properties["Role"].Value.ToString();

                                // Search for any parameters mentioned here that should be hidden from the UI
                                string[] miscInfo = role.Split('~');
                                foreach (string misc in miscInfo) {
                                    if (misc.StartsWith("h_")) {    // marks a parameter to be hidden (h_param)
                                        string hideParam = misc.Substring(2, (misc.Length - 2));
                                        for (int i = 0; i < param.Length; i++) {
                                            // if a match is found in the parameters, "~h" will be appended to that parameter
                                            if (param[i].StartsWith(String.Format("{0}~", hideParam))) {
                                                param[i] += "~h";
                                            }
                                        }
                                    }
                                    else if (misc.StartsWith("s_")) {
                                        formatting = misc.Substring(2, (misc.Length - 2));
                                    }
                                }
                            }
                            if (item.Properties["Category"].Value != null) {
                                category = item.Properties["Category"].Value.ToString();
                            }
                            HelpList.Add(new Help(name, synopsis, detail, meta, param, role, category, formatting));
                        }
                    }
                    // check the error stream
                    if (pipe.Streams.Error.Count > 0) {
                        string name = "Error";
                        string synopsis = "<noSynopsis>";
                        string detail = "<noDetail>";
                        string meta = "<noMeta>";
                        Collection<ErrorRecord> errors = pipe.Streams.Error.ReadAll();
                        string[] param = new string[errors.Count];
                        foreach (ErrorRecord error in errors) {
                            int i = 0;
                           param[i] = String.Format("{0}\n>{1}\n>{2}\n>{3}", error.Exception.GetType(), error.Exception.Message, error.Exception.Source, error.Exception.TargetSite);
                            i++;
                        }
                        string role = "<noRole>";
                        string category = "<noCategory>";
                        string formatting = "<noFormat>";
                        HelpList.Add(new Help(name, synopsis, detail, meta, param, role, category, formatting));
                    }
                    // check the verbose stream
                    if (pipe.Streams.Verbose.Count > 0) {
                        // 
                    }

                    return HelpList;
                }
            }
            else {
                return new List<Help>();
            }
        }
    }

    // Classes for binding elements of the Help objects to the combo, script, description, and author boxes
    public class Category {
        public string Name { get; set; }
        public List<HelpObj> Objects { get; set; }
    }

    public class HelpObj {
        public string Title { get; set; }
        public string Description { get; set; }
        public string Meta { get; set; }
        public string FullName { get; set; }
        public List<ParamObj> Args { get; set; }
        public string Formatting { get; set; }
    }
}
