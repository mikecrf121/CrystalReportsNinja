using CrystalDecisions.CrystalReports.Engine;
using CrystalDecisions.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using Newtonsoft.Json;

namespace CrystalReportsNinja
{
    public class ReportProcessor
    {
        private string _sourceFilename;
        private string _outputFilename;
        private string _outputFormat;
        private bool _printToPrinter;
        private string _defaultParamsOutputPath;

        private ReportDocument _reportDoc;
        private LogWriter _logger;

        public ArgumentContainer ReportArguments { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public ReportProcessor()
        {
            ReportArguments = new ArgumentContainer();

            _reportDoc = new ReportDocument();
        }

        // This is Used for the Default Params Json Object
        public class JsonParamDefaults
        {
            public string ParamName { get; set; }
            public string ParamDefault { get; set; }

        }

        /// <summary>
        /// Load external Crystal Report file into Report Document
        /// </summary>
        private void LoadReport()
        {
            _sourceFilename = ReportArguments.ReportPath.Trim();
            if (_sourceFilename == null || _sourceFilename == string.Empty)
            {
                throw new Exception("Invalid Crystal Reports file");
            }

            if (_sourceFilename.LastIndexOf(".rpt") == -1)
                throw new Exception("Invalid Crystal Reports file");

            _reportDoc.Load(_sourceFilename, OpenReportMethod.OpenReportByDefault);
            _logger.Write(string.Format("Report loaded successfully"));
            Console.WriteLine("Report loaded successfully");
        }

        /// <summary>
        /// Match User input parameter values with Report parameters
        /// </summary>
        private void ProcessParameters()
        {
            List<JsonParamDefaults> _data = new List<JsonParamDefaults>();
            if (_reportDoc.DataDefinition.ParameterFields.Count > 0)
            {
                ParameterCore paraCore = new ParameterCore(ReportArguments.LogFileName, ReportArguments);
                paraCore.ProcessRawParameters();

                foreach (ParameterFieldDefinition _ParameterFieldDefinition in _reportDoc.DataDefinition.ParameterFields)
                {
                    if (!_ParameterFieldDefinition.IsLinked())
                    {
                        ParameterValues values = paraCore.GetParameterValues(_ParameterFieldDefinition);
                        _ParameterFieldDefinition.ApplyCurrentValues(values);
                    }
                }
            }
            ///Where im going to be gathering the daefult params from the .rpt file 
            ///and adding them to a text file in the form of json
            /// added December 2019, very useful for anyone needing these values
            ///
            var paramDefs = _reportDoc.DataDefinition.ParameterFields;
            for (int i = 0; i < paramDefs.Count; i++)
            {
                ParameterCore paraCore = new ParameterCore(ReportArguments.LogFileName, ReportArguments);
                paraCore.ProcessRawParameters();
                ParameterValues values = paraCore.GetParameterValues(paramDefs[i]);
                // System.Console.WriteLine("values has been set for index " + i);
                //*********************************************************************
                // Where im utilizing the the loop for all params to get names and all
                // DEFAULT PARAM values defined within the actual .rpt file
                //*********************************************************************

                var nm = paramDefs[i].Name.ToString();
                //Console.WriteLine("Parameter name: " + nm);
                var count = nm.Count(f => f == '.');
                var foundInList = _data.Any(param => param.ParamName == nm);
                if (count == 0 && !foundInList)
                {
                    // default param value
                    if (paramDefs[i] != null && paramDefs[i].DefaultValues != null && paramDefs[i].DefaultValues.Count > 0)
                    {
                        // System.Console.WriteLine("inside if check for index " + i);
                        var te1 = (paramDefs[i].DefaultValues[0] as CrystalDecisions.Shared.ParameterDiscreteValue).Value;
                        // System.Console.WriteLine("te1 set for index " + i);
                        // param name associated with default param
                        var n = paramDefs[i].Name;
                        if (n.Contains("@"))
                        {
                            n = n.Substring(1);
                        };
                        // System.Console.WriteLine("n set for index " + i);
                        // adding to object             
                        _data.Add(new JsonParamDefaults()
                        {
                            ParamName = n,
                            ParamDefault = te1.ToString()
                        });

                    }
              
                }
            }

            //converting object to serialized json format
            string json = JsonConvert.SerializeObject(_data.ToArray());
            //write string to file
            //System.Console.WriteLine("WRITING JSON TO TEST FILE......");
            _defaultParamsOutputPath = ReportArguments.ParamDefaultsOutputPath;

            //This is where the JSON data is being written to a text file for param defaults
            System.IO.File.WriteAllText(@_defaultParamsOutputPath, json);





        }

        /// <summary>
        /// Validate configurations related to program output.
        /// </summary>
        /// <remarks>
        /// Program output can be TWO forms
        /// 1. Export as a file
        /// 2. Print to printer
        /// </remarks>
        private void ValidateOutputConfigurations()
        {
            _outputFilename = ReportArguments.OutputPath;
            _outputFormat = ReportArguments.OutputFormat;
            _printToPrinter = ReportArguments.PrintOutput;

            bool specifiedFileName = _outputFilename != null ? true : false;
            bool specifiedFormat = _outputFormat != null ? true : false;

            if (!_printToPrinter)
            {
                string fileExt = "";

                //default set to text file
                if (!specifiedFileName && !specifiedFormat)
                    _outputFormat = "txt";

                // Use output format to set output file name extension
                if (specifiedFormat)
                {
                    if (_outputFormat.ToUpper() == "XLSDATA")
                        fileExt = "xls";
                    else if (_outputFormat.ToUpper() == "TAB")
                        fileExt = "txt";
                    else if (_outputFormat.ToUpper() == "ERTF")
                        fileExt = "rtf";
                    else
                        fileExt = _outputFormat;
                }

                // Use output file name extension to set output format
                if (specifiedFileName && !specifiedFormat)
                {
                    int lastIndexDot = _outputFilename.LastIndexOf(".");
                    fileExt = _outputFilename.Substring(lastIndexDot + 1, 3); //what if file ext has 4 char

                    //ensure filename extension has 3 char after the dot (.)
                    if ((_outputFilename.Length == lastIndexDot + 4) && (fileExt.ToUpper() == "RTF" || fileExt.ToUpper() == "TXT" || fileExt.ToUpper() == "CSV" || fileExt.ToUpper() == "PDF" || fileExt.ToUpper() == "RPT" || fileExt.ToUpper() == "DOC" || fileExt.ToUpper() == "XLS" || fileExt.ToUpper() == "XML" || fileExt.ToUpper() == "HTM"))
                        _outputFormat = _outputFilename.Substring(lastIndexDot + 1, 3);
                }

                if (specifiedFileName && specifiedFormat)
                {
                    int lastIndexDot = _outputFilename.LastIndexOf(".");
                    if (fileExt != _outputFilename.Substring(lastIndexDot + 1, 3)) //what if file ext has 4 char
                    {
                        _outputFilename = string.Format("{0}.{1}", _outputFilename, fileExt);
                    }
                }

                if (!specifiedFileName)
                    _outputFilename = String.Format("{0}-{1}.{2}", _sourceFilename.Substring(0, _sourceFilename.LastIndexOf(".rpt")), DateTime.Now.ToString("yyyyMMddHHmmss"), fileExt);

                _logger.Write(string.Format("Output Filename : {0}", _outputFilename));
                _logger.Write(string.Format("Output format : {0}", _outputFormat));
            }
        }

        /// <summary>
        /// Perform Login to database tables
        /// </summary>
        private void PerformDBLogin()
        {
            bool toRefresh = ReportArguments.Refresh;

            var server = ReportArguments.ServerName;
            var database = ReportArguments.DatabaseName;
            var username = ReportArguments.UserName;
            var password = ReportArguments.Password;

            if (toRefresh)
            {
                TableLogOnInfo logonInfo = new TableLogOnInfo();
                foreach (Table table in _reportDoc.Database.Tables)
                {
                    if (server != null)
                        logonInfo.ConnectionInfo.ServerName = server;

                    if (database != null)
                        logonInfo.ConnectionInfo.DatabaseName = database;

                    if (username == null && password == null)
                        logonInfo.ConnectionInfo.IntegratedSecurity = true;
                    else
                    {
                        if (username != null && username.Length > 0)
                            logonInfo.ConnectionInfo.UserID = username;

                        if (password == null) //to support blank password
                            logonInfo.ConnectionInfo.Password = "";
                        else
                            logonInfo.ConnectionInfo.Password = password;
                    }
                    table.ApplyLogOnInfo(logonInfo);
                }
                Console.WriteLine("Database Login done");
            }
        }

        /// <summary>
        /// Set export file type or printer to Report Document object.
        /// </summary>
        private void ApplyReportOutput()
        {
            if (_printToPrinter)
            {
                var printerName = ReportArguments.PrinterName != null ? ReportArguments.PrinterName.Trim() : "";

                if (printerName.Length > 0)
                {
                    _reportDoc.PrintOptions.PrinterName = printerName;
                }
                else
                {
                    System.Drawing.Printing.PrinterSettings prinSet = new System.Drawing.Printing.PrinterSettings();

                    if (prinSet.PrinterName.Trim().Length > 0)
                        _reportDoc.PrintOptions.PrinterName = prinSet.PrinterName;
                    else
                        throw new Exception("No printer name is specified");
                }
            }
            else
            {
                if (_outputFormat.ToUpper() == "RTF")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.RichText;
                else if (_outputFormat.ToUpper() == "TXT")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.Text;
                else if (_outputFormat.ToUpper() == "TAB")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.TabSeperatedText;
                else if (_outputFormat.ToUpper() == "CSV")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.CharacterSeparatedValues;
                else if (_outputFormat.ToUpper() == "PDF")
                {
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.PortableDocFormat;

                    var grpCnt = _reportDoc.DataDefinition.Groups.Count;

                    if (grpCnt > 0)
                        _reportDoc.ExportOptions.ExportFormatOptions = new PdfFormatOptions { CreateBookmarksFromGroupTree = true };
                }
                else if (_outputFormat.ToUpper() == "RPT")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.CrystalReport;
                else if (_outputFormat.ToUpper() == "DOC")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.WordForWindows;
                else if (_outputFormat.ToUpper() == "XLS")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.Excel;
                else if (_outputFormat.ToUpper() == "XLSDATA")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.ExcelRecord;
                else if (_outputFormat.ToUpper() == "XLSX")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.ExcelWorkbook;
                else if (_outputFormat.ToUpper() == "ERTF")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.EditableRTF;
                else if (_outputFormat.ToUpper() == "XML")
                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.Xml;
                else if (_outputFormat.ToUpper() == "HTM")
                {
                    HTMLFormatOptions htmlFormatOptions = new HTMLFormatOptions();

                    if (_outputFilename.LastIndexOf("\\") > 0) //if absolute output path is specified
                        htmlFormatOptions.HTMLBaseFolderName = _outputFilename.Substring(0, _outputFilename.LastIndexOf("\\"));

                    htmlFormatOptions.HTMLFileName = _outputFilename;
                    htmlFormatOptions.HTMLEnableSeparatedPages = false;
                    htmlFormatOptions.HTMLHasPageNavigator = true;
                    htmlFormatOptions.FirstPageNumber = 1;

                    _reportDoc.ExportOptions.ExportFormatType = ExportFormatType.HTML40;
                    _reportDoc.ExportOptions.FormatOptions = htmlFormatOptions;
                }
            }
        }

        /// <summary>
        /// Refresh Crystal Report if no input of parameters
        /// </summary>
        private void PerformRefresh()
        {
            bool toRefresh = ReportArguments.Refresh;
            bool noParameter = (_reportDoc.ParameterFields.Count == 0) ? true : false;

            if (toRefresh && noParameter)
                _reportDoc.Refresh();
        }

        /// <summary>
        /// Print or Export Crystal Report
        /// </summary>
        private void PerformOutput()
        {
            if (_printToPrinter)
            {
                var copy = ReportArguments.PrintCopy;
                _reportDoc.PrintToPrinter(copy, true, 0, 0);
                _logger.Write(string.Format("Report printed to : {0}", _reportDoc.PrintOptions.PrinterName));
            }
            else
            {
                _reportDoc.ExportOptions.ExportDestinationType = ExportDestinationType.DiskFile;

                DiskFileDestinationOptions diskOptions = new DiskFileDestinationOptions();
                _reportDoc.ExportOptions.DestinationOptions = diskOptions;
                diskOptions.DiskFileName = _outputFilename;

                _reportDoc.Export();
                _logger.Write(string.Format("Report exported to : {0}", _outputFilename));

                if (ReportArguments.EmailOutput)
                {
                    using (MailMessage _MailMessage = new MailMessage())
                    {
                        _MailMessage.Attachments.Add(new Attachment(_outputFilename));
                        _MailMessage.From = new MailAddress(ReportArguments.MailFrom);
                        _MailMessage.Subject = ReportArguments.EmailSubject;
                        _MailMessage.To.Add(ReportArguments.MailTo);

                        SmtpClient smtpClient = new SmtpClient();
                        smtpClient.Host = ReportArguments.SmtpServer;
                        smtpClient.UseDefaultCredentials = true;
                        smtpClient.Send(_MailMessage);
                    }

                    if (!ReportArguments.EmailKeepFile)
                    { File.Delete(_outputFilename); }
                }
            }
            Console.WriteLine("Completed");
        }

        /// <summary>
        /// Run the Crystal Reports Exporting or Printing process.
        /// </summary>
        public void Run()
        {
            try
            {
                _logger = new LogWriter(ReportArguments.LogFileName, ReportArguments.EnableLogToConsole);

                LoadReport();
                ValidateOutputConfigurations();

                PerformDBLogin();
                ApplyReportOutput();
                ProcessParameters();

                PerformRefresh();
                PerformOutput();
            }
            catch (Exception ex)
            {
                _logger.Write(string.Format("Exception: {0}", ex.Message));
                _logger.Write(string.Format("Inner Exception: {0}", ex.InnerException));

                throw ex;
            }
            finally
            {
                _reportDoc.Close();
            }
        }
    }
}
