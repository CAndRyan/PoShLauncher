using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management.Automation;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;

namespace PSLauncher.CodeBehind {
    public class Param {
        string CurrDir;
        List<ParamVal> ParamValidation { get; set; }

        // Method to construct a Param object
        public Param(string currDir) {
            this.CurrDir = currDir;
            string pathVal = currDir + Constants.config + Constants.paramVal;

            // Set up parameter validation info (file existence checked in Load class)
            StreamReader file = new StreamReader(pathVal);
            List<ParamVal> paramVal = new List<ParamVal>();
            string line;
            while ((line = file.ReadLine()) != null) {
                string[] info = line.Split(',');
                paramVal.Add(new ParamVal(info[0].Trim(), info[1].Trim(), info[2].Trim()));
            }
            file.Close();

            this.ParamValidation = paramVal;
        }

        // A method to take in the entries of each input box and the help object for the command being called
        // It validates the entries and generates a string to append as the arguments to the command
        public string CreateParamList(HelpObj cmd, string[] inputs) {
            string paramString = "";    // paramString gets built as valid parameters are found to exist
            string invalidMsg = "i";
            bool valid = true;
            
            // Start the paramString for a script file (.ps1)
            if (cmd.FullName.EndsWith(".ps1")) {
                paramString = String.Format("'{0}' ", cmd.FullName); // originally started, -file 
            }
            else {
                paramString = String.Format("& '{0}' ", cmd.FullName);
            }

            // Loop through all arguments and passed inputs to determine validity and build the parameter string
            for (int i = 0; i < 4; i++) {
                if (cmd.Args[i].Label == "") {  // break the loop when an empty ParamObj is encountered
                    i = 4;
                }
                else {
                    // Check validity of input(s) if a validation script is provided for the parameter
                    if (inputs[i].Trim() != "") {
                        for (int j = 0; j < ParamValidation.Count; j++) {
                            if (Regex.IsMatch(cmd.Args[i].Name, ParamValidation[j].Match, RegexOptions.IgnoreCase)) {
                                // Establish list of values to test (in case multiple inputs are supported
                                List<string> testInput = new List<string>();
                                bool inputChanged = false;
                                if (inputs[i].Contains(",")) {
                                    if (cmd.Args[i].Single == "f") {
                                        string[] argu = inputs[i].Split(',');
                                        foreach (string a in argu) {
                                            testInput.Add(a.Trim());
                                        }
                                    }
                                    else {
                                        testInput.Add(inputs[i].Trim());
                                    }
                                }
                                else {
                                    testInput.Add(inputs[i].Trim());
                                }

                                for (int l = 0; l < testInput.Count; l++) {
                                    // Make the powershell calls to validate input
                                    using (PowerShell pipe = PowerShell.Create()) {
                                        string uitPath = CurrDir + Constants.uiTools;
                                        string scriptString = String.Format("$ErrorActionPreference = 'SilentlyContinue'; Set-ExecutionPolicy -ExecutionPolicy Bypass -Scope Process -Force; $ErrorActionPreference = 'Continue'; & \"{0}{1}\" -{2} '{3}'", uitPath, ParamValidation[j].Script, ParamValidation[j].Param, testInput[l]);
                                        pipe.AddScript(scriptString);

                                        try {
                                            Collection<PSObject> psReturn = pipe.Invoke();

                                            // Handle the returned objects (strings)
                                            if (psReturn.Count > 1) {
                                                valid = false;
                                                MessageBox.Show("Validation script returned multiple values. It should only return a single string.", "Error: Validation");
                                            }
                                            else if (psReturn.Count == 0) {
                                                valid = false;
                                                MessageBox.Show("Validation script didn't return any values. It should return a string.", "Error: Validation");
                                            }
                                            else {
                                                string[] validation = psReturn[0].BaseObject.ToString().Split('~');
                                                if (validation[0] == "v") {
                                                    // Change the input if a search was conducted and returned a new value
                                                    if (testInput[l] != validation[1]) {
                                                        testInput[l] = validation[1];
                                                        inputChanged = true;
                                                    }
                                                }
                                                else if (validation[0] == "i") {
                                                    //valid = false;
                                                    testInput[l] = "";
                                                    inputChanged = true;

                                                    string errString = validation[1];
                                                    for (int k = 2; k < validation.Length; k++) {
                                                        errString += String.Format("\n\t{0}", validation[k]);
                                                    }

                                                    invalidMsg += String.Format("~{0}", errString);
                                                }
                                                else {
                                                    valid = false;
                                                    MessageBox.Show("Validation script returned invalid data.", "Error: Validation");
                                                }
                                            }

                                            // Display any errors returned from the powershell command
                                            if (pipe.Streams.Error.Count > 0) {
                                                Collection<ErrorRecord> errors = pipe.Streams.Error.ReadAll();
                                                string errorString = "";
                                                foreach (ErrorRecord er in errors) {
                                                    errorString += String.Format("{0}\n>{1}\n>{2}\n>{3}\n\n", er.Exception.GetType(), er.Exception.Message, er.Exception.Source, er.Exception.TargetSite);
                                                }
                                                MessageBox.Show(errorString, "Error: Input Validation Script");
                                            }
                                        }
                                        catch (Exception exep) {
                                            MessageBox.Show(String.Format("{0}\n\n{1}", exep.GetType(), exep.Message), "Error: Launching Validation Script");
                                        }
                                    }
                                }

                                // If any changes were found for the inputs based on searches, update them
                                if (inputChanged) {
                                    //inputs[i] = testInput[0];
                                    bool firstAdded = false;
                                    for (int l = 0; l < testInput.Count; l++) {     // rebuild complete input string
                                        if (testInput[l] != "") {
                                            if (!firstAdded) {
                                                inputs[i] = testInput[l];
                                                firstAdded = true;
                                            }
                                            else {
                                                inputs[i] += String.Format(",{0}", testInput[l]);
                                            }
                                        }
                                    }

                                    // In case the returned values were all invalid, reset the input
                                    if (!firstAdded) {
                                        inputs[i] = "";
                                        valid = false;
                                    }
                                }
                            }
                        }
                    }

                    // Further validation checks and build the functions output
                    if (cmd.Args[i].Mandatory == "t") {
                        if (inputs[i] == "") {      // no input but parameter is required...
                            invalidMsg += String.Format("~{0} is required!", cmd.Args[i].Name);
                            valid = false;
                        }
                        else {      // input found for a mandatory parameter
                            // Check if the argument allows multiple inputs and break on commas
                            string arg = "";
                            if (inputs[i].Contains(",")) {
                                if (cmd.Args[i].Single == "f") {
                                    string[] argu = inputs[i].Split(',');
                                    arg = String.Format("\'{0}\'", argu[0].Trim());
                                    for (int j = 1; j < argu.Length; j++) {
                                        arg += String.Format(", \'{0}\'", argu[j].Trim());
                                    }
                                }
                                else {
                                    arg = String.Format("\'{0}\'", inputs[i]);
                                }
                            }
                            else {
                                arg = String.Format("\'{0}\'", inputs[i]);
                            }

                            paramString += String.Format("-{0} {1} ", cmd.Args[i].Name, arg);
                        }
                    }
                    else {
                        if (inputs[i] != "") {      // non-empty input for a non-mandatory parameter
                            // Check if the argument allows multiple inputs and break on commas
                            string arg = "";
                            if (inputs[i].Contains(",")) {
                                if (cmd.Args[i].Single == "f") {
                                    string[] argu = inputs[i].Split(',');
                                    arg = String.Format("\'{0}\'", argu[0].Trim());
                                    for (int j = 1; j < argu.Length; j++) {
                                        arg += String.Format(", \'{0}\'", argu[j].Trim());
                                    }
                                }
                                else {
                                    arg = String.Format("\'{0}\'", inputs[i]);
                                }
                            }
                            else {
                                arg = String.Format("\'{0}\'", inputs[i]);
                            }

                            paramString += String.Format("-{0} {1} ", cmd.Args[i].Name, arg);
                        }
                    }
                }
            }

            // Check final validity and return either paramString or invalidMsg
            if (valid) {
                return String.Format("v~{0}", paramString);
            }
            else {  // return the invalid message for display if the entered values were invalid
                return invalidMsg;
            }
        }

        // Method to create an array of ParamObj's from a HelpObj's param array
        public List<ParamObj> ConvertParam(string[] param) {
            List<ParamObj> paramAry = new List<ParamObj>();
            for (int i = 0; (i < param.Length) && (i < 4); i++) {
                string[] paramSplit = param[i].Split('~');

                // If an extra element, ~h, is included after the mandatory piece, hide this parameter
                bool hide = false;
                if (paramSplit.Length > 3) {
                    if (paramSplit[3] == "h") {
                        hide = true;
                    }
                }

                if (!hide) {
                    string label = paramSplit[0];
                    if (label.Length < 9) {
                        label = label + ":";
                    }
                    else {
                        label = label.Substring(0, 9) + ":";
                    }

                    string man = "n";
                    string sing = "n";
                    if (paramSplit.Length > 1) {
                        man = paramSplit[1];
                        if (paramSplit.Length > 2) {
                            sing = paramSplit[2];
                        }
                    }

                    paramAry.Add(new ParamObj(label, paramSplit[0], man, sing));
                }
                else {
                    paramAry.Add(new ParamObj());
                }
            }

            // Create blank ParamObj's if the number of parameters is less than the max
            if (paramAry.Count < 4) {
                for (int i = paramAry.Count; i < 4; i++) {
                    paramAry.Add(new ParamObj());
                }
            }

            return paramAry;
        }
    }

    public class ParamObj {
        public string Label { get; set; }
        public string Name { get; set; }
        public string Mandatory { get; set; }
        public string Single { get; set; }

        // Constructor for a ParamObj
        public ParamObj(string label, string name, string mandatory, string single) {
            this.Label = label;
            this.Name = name;
            this.Mandatory = mandatory;
            this.Single = single;
        }

        // Base constructor for a blank ParamObj
        public ParamObj() {
            Label = "";
            Name = "";
            Mandatory = "f";
            Single = "t";
        }
    }

    public class ParamVal {
        public string Match { get; set; }
        public string Script { get; set; }
        public string Param { get; set; }

        // Constructor for a ParamVal
        public ParamVal(string match, string script, string param) {
            // Generate a regular expression match string from the provided string (may include wildcards, beginning and end)
            string regexMatch = "^";
            match = match.Trim();
            if (match[0] == '*') {  // Check for wildcard at beginning
                regexMatch += ".*";
                match = match.Substring(1, (match.Length - 1));
            }
            if (match[match.Length - 1] == '*') {    // Check for wildcard at end
                regexMatch += match.Substring(0, (match.Length - 1));
                regexMatch += ".*$";
            }
            else {
                regexMatch += match + "$";
            }

            // Set the values
            this.Match = regexMatch;
            this.Script = script;
            this.Param = param;
        }
    }
}
