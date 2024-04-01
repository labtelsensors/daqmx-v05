/******************************************************************************
*
* Example program:
*   ContAcqVoltageSamples_IntClk_ToFile
*
* Category:
*   AI
*
* Description:
*   This example demonstrates how to acquire, write to file, and load from disk
*   a continuous amount of analog input data using the DAQ device's internal
*   clock.
*
* Instructions for running:
*   1.  Select the physical channels corresponding to where your signals are
*       input on the DAQ device.
*   2.  Enter the minimum and maximum voltage range.Note: For better accuracy,
*       try to match the input range to the expected voltage levels of the
*       measured signals.
*   3.  Set the rate of the acquisition and number of samples.
*   4.  Choose an output file format, either text or binary.
*   5.  Select the output filename.
*   6.  Start the acquisition.
*   7.  Select the file format of the file you want to load data from, either
*       text or binary.
*   8.  Select the input filename.
*   9.  Click the Read button to read the data from disk and display it.
*
* Steps:
*   1.  Create a new analog input task.
*   2.  Create the analog input voltage channels.
*   3.  Configure the timing for the acquisition.  In this example we use the
*       DAQ device's internal clock to take a continuous number of samples.
*   4.  Open the output file for writing.
*   5.  Create a AnalogMultiChannelReader and associate it with the task by
*       using the task's stream. Call
*       AnalogMultiChannelReader.BeginBeginReadMultiSample to install a callback
*       and begin the asynchronous read operation.
*   6.  Inside the callback, call AnalogMultiChannelReader.EndReadMultiSample to
*       retrieve the data from the read operation.  
*   7.  Call AnalogMultiChannelReader.BeginBeginReadMultiSample again inside the
*       callback to perform another read operation.
*   8.  Dispose the Task object to clean-up any resources associated with the
*       task.
*   9.  Close the output file.
*   10. Open the input file for reading.
*   11. Read and display the data.
*   12. Handle any DaqExceptions, if they occur.
*
*   Note: This example sets SynchronizeCallback to true. If SynchronizeCallback
*   is set to false, then you must give special consideration to safely dispose
*   the task and to update the UI from the callback. If SynchronizeCallback is
*   set to false, the callback executes on the worker thread and not on the main
*   UI thread. You can only update a UI component on the thread on which it was
*   created. Refer to the How to: Safely Dispose Task When Using Asynchronous
*   Callbacks topic in the NI-DAQmx .NET help for more information.
*
* I/O Connections Overview:
*   Make sure your signal input terminals match the physical I/O control.  In
*   the default case (differential channel ai0), wire the positive lead for your
*   signal to the ACH0 pin on your DAQ device and wire the negative lead for
*   your signal to the ACH8 pin.  For more information on the input and output
*   terminals for your device, open the NI-DAQmx Help and refer to the NI-DAQmx
*   Device Terminals and Device Considerations books in the table of contents.
*
* Microsoft Windows Vista User Account Control
*   Running certain applications on Microsoft Windows Vista requires
*   administrator privileges, 
*   because the application name contains keywords such as setup, update, or
*   install. To avoid this problem, 
*   you must add an additional manifest to the application that specifies the
*   privileges required to run 
*   the application. Some Measurement Studio NI-DAQmx examples for Visual Studio
*   include these keywords. 
*   Therefore, all examples for Visual Studio are shipped with an additional
*   manifest file that you must 
*   embed in the example executable. The manifest file is named
*   [ExampleName].exe.manifest, where [ExampleName] 
*   is the NI-provided example name. For information on how to embed the manifest
*   file, refer to http://msdn2.microsoft.com/en-us/library/bb756929.aspx.Note: 
*   The manifest file is not provided with examples for Visual Studio .NET 2003.
*
******************************************************************************/

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Windows.Forms.DataVisualization.Charting;
using System.IO.Ports;
using System.Threading;


using NationalInstruments.DAQmx;

namespace NationalInstruments.Examples.ContAcqVoltageSamples_IntClk_ToFile
{
    /// <summary>
    /// Summary description for MainForm.
    /// </summary>
    public class MainForm : System.Windows.Forms.Form
    {
        private Task AITask;
        private Task runningAITask;
        private AnalogMultiChannelReader analogInReader;        
        private AsyncCallback analogCallback;

        private Task DOTask;
        private Task runningDOTask;
        private DigitalSingleChannelWriter digitalOutWriter;
        private bool[] DOData;
        private Int16 defaultChnn = 0;
        private Int16 switchChnn = 1;

        private double[,] data;
        private DataColumn[] dataColumn = null;
        private ArrayList savedData;
        private ArrayList savedSensorData;
        private ArrayList daqTimestamp;
        private ArrayList sensorTimestamp;
        private StreamWriter fileStreamWriter;
        private BinaryWriter fileBinaryWriter;
        private int tableMaximumRows = 17;
        private string fileNameWrite;
        private bool useTextFileWrite;

        private Int32 chartMaxPoints = 200;
        private ulong chartCounter = 0;
        private long xAxisCounter = 0;
        private int chartUpdateRate = 10;
        private double lastMin = Int32.MinValue;
        private double lastMax = Int32.MaxValue;
        double[,] lastData;

        private System.Windows.Forms.GroupBox channelParametersGroupBox;
        private System.Windows.Forms.Label maximumLabel;
        private System.Windows.Forms.Label minimumLabel;
        private System.Windows.Forms.Label physicalChannelLabel;
        private System.Windows.Forms.Label rateLabel;       
        private System.Windows.Forms.Label samplesLabel;
        private System.Windows.Forms.Label channNumObsLabel;

        private System.Windows.Forms.GroupBox timingParametersGroupBox;
        private System.Windows.Forms.GroupBox acquisitionResultGroupBox;
        private System.Windows.Forms.NumericUpDown rateNumeric;
        private System.Windows.Forms.NumericUpDown samplesPerChannelNumeric;
        private System.Windows.Forms.NumericUpDown minimumValueNumeric;
        private System.Windows.Forms.NumericUpDown maximumValueNumeric;
        private System.Windows.Forms.ComboBox physicalChannelComboBox;
        private System.Windows.Forms.SaveFileDialog writeToFileSaveFileDialog;
        private System.Windows.Forms.OpenFileDialog readFromFileOpenFileDialog;
        private System.Windows.Forms.GroupBox writeToFileGroupBox;
        private System.Windows.Forms.ToolTip fileToolTip;
        private System.Windows.Forms.TextBox filePathWriteTextBox;
        private System.Windows.Forms.Button browseWriteButton;
        private System.Windows.Forms.Label filePathWriteLabel;
        private System.Windows.Forms.RadioButton binaryFileWriteRadioButton;
        private System.Windows.Forms.RadioButton textFileWriteRadioButton;
        private System.Windows.Forms.Label fileTypeWriteLabel;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Button startButton;
        private System.ComponentModel.IContainer components;
        private System.Timers.Timer statusCheckTimer;
        private System.Timers.Timer chartTimer;
        private Label ChnnNumObsLabel;
        private String[] physicalChannelNames;
        private String[] physicalDigitalChannelNames;
        private Stopwatch stopwatch;
        private GroupBox digitalOutputGroupBox;
        private Label DOChannel1Label;
        private Label enableSwitchingLabel;
        private ComboBox DOChannel2ComboBox;
        private Label DOChannel2Label;
        private ComboBox DOChannel1ComboBox;
        private CheckBox enableSwitchingCheckBox;
        private NumericUpDown periodNumeric;
        private Label periodLabel;
        private System.Windows.Forms.DataVisualization.Charting.Chart chart;
        private TimeSpan stopwatchCounter;
        private TimeSpan spStopwatchCounter;
        //private System.Windows.Forms.Timer statusCheckTimer;        
        static SerialPort _serialPort;
        static Stopwatch spStopwatch;
        private ComboBox comPortComboBox;
        private CheckBox SerialEnableCheck;
        private GroupBox groupBox1;
        private Label label3;
        private Label label4;
        private CheckBox ToFileEnableCheck;
        private Label label1;

        public MainForm()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();

            //
            // TODO: Add any constructor code after InitializeComponent call
            //
            stopButton.Enabled = false;
            stopwatch = new Stopwatch();

            physicalChannelNames = DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.AI, PhysicalChannelAccess.External);
            String[] chnnNumber = new String[physicalChannelNames.Length];
            for (int i = 0; i < physicalChannelNames.Length; i++)
            {
                chnnNumber[i] = (i+1).ToString();
            }     
            physicalChannelComboBox.Items.AddRange(chnnNumber);
            if (physicalChannelComboBox.Items.Count > 0)
                physicalChannelComboBox.SelectedIndex = 0;

            physicalDigitalChannelNames = DaqSystem.Local.GetPhysicalChannels(PhysicalChannelTypes.DOLine, PhysicalChannelAccess.External);
            chnnNumber = new String[physicalDigitalChannelNames.Length];
            for (int i = 0; i < physicalDigitalChannelNames.Length; i++)
            {
                chnnNumber[i] = physicalDigitalChannelNames[i];
            }         
            
            DOChannel1ComboBox.Items.AddRange(chnnNumber);
            if (DOChannel1ComboBox.Items.Count > 0)
                DOChannel1ComboBox.SelectedIndex = 0;

            DOChannel2ComboBox.Items.AddRange(chnnNumber);
            if (DOChannel2ComboBox.Items.Count > 0)
                DOChannel2ComboBox.SelectedIndex = 1;

            foreach (string s in SerialPort.GetPortNames())
            {
                comPortComboBox.Items.Add(s);
            }
            comPortComboBox.SelectedIndex = 0;

            InitializeChart();
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose( bool disposing )
        {
            if( disposing )
            {
                if (components != null) 
                {
                    components.Dispose();
                }
                if (AITask != null)
                {
                    runningAITask = null;
                    AITask.Dispose();
                }
            }
            base.Dispose( disposing );
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataVisualization.Charting.ChartArea chartArea1 = new System.Windows.Forms.DataVisualization.Charting.ChartArea();
            System.Windows.Forms.DataVisualization.Charting.Legend legend1 = new System.Windows.Forms.DataVisualization.Charting.Legend();
            System.Windows.Forms.DataVisualization.Charting.Series series1 = new System.Windows.Forms.DataVisualization.Charting.Series();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.channelParametersGroupBox = new System.Windows.Forms.GroupBox();
            this.physicalChannelComboBox = new System.Windows.Forms.ComboBox();
            this.minimumValueNumeric = new System.Windows.Forms.NumericUpDown();
            this.maximumValueNumeric = new System.Windows.Forms.NumericUpDown();
            this.maximumLabel = new System.Windows.Forms.Label();
            this.minimumLabel = new System.Windows.Forms.Label();
            this.physicalChannelLabel = new System.Windows.Forms.Label();
            this.timingParametersGroupBox = new System.Windows.Forms.GroupBox();
            this.rateNumeric = new System.Windows.Forms.NumericUpDown();
            this.samplesLabel = new System.Windows.Forms.Label();
            this.rateLabel = new System.Windows.Forms.Label();
            this.samplesPerChannelNumeric = new System.Windows.Forms.NumericUpDown();
            this.channNumObsLabel = new System.Windows.Forms.Label();
            this.acquisitionResultGroupBox = new System.Windows.Forms.GroupBox();
            this.chart = new System.Windows.Forms.DataVisualization.Charting.Chart();
            this.filePathWriteTextBox = new System.Windows.Forms.TextBox();
            this.writeToFileSaveFileDialog = new System.Windows.Forms.SaveFileDialog();
            this.readFromFileOpenFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.fileToolTip = new System.Windows.Forms.ToolTip(this.components);
            this.writeToFileGroupBox = new System.Windows.Forms.GroupBox();
            this.ToFileEnableCheck = new System.Windows.Forms.CheckBox();
            this.stopButton = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.startButton = new System.Windows.Forms.Button();
            this.browseWriteButton = new System.Windows.Forms.Button();
            this.filePathWriteLabel = new System.Windows.Forms.Label();
            this.binaryFileWriteRadioButton = new System.Windows.Forms.RadioButton();
            this.textFileWriteRadioButton = new System.Windows.Forms.RadioButton();
            this.fileTypeWriteLabel = new System.Windows.Forms.Label();
            this.ChnnNumObsLabel = new System.Windows.Forms.Label();
            this.digitalOutputGroupBox = new System.Windows.Forms.GroupBox();
            this.periodNumeric = new System.Windows.Forms.NumericUpDown();
            this.enableSwitchingCheckBox = new System.Windows.Forms.CheckBox();
            this.periodLabel = new System.Windows.Forms.Label();
            this.DOChannel2ComboBox = new System.Windows.Forms.ComboBox();
            this.DOChannel2Label = new System.Windows.Forms.Label();
            this.DOChannel1ComboBox = new System.Windows.Forms.ComboBox();
            this.DOChannel1Label = new System.Windows.Forms.Label();
            this.enableSwitchingLabel = new System.Windows.Forms.Label();
            this.comPortComboBox = new System.Windows.Forms.ComboBox();
            this.SerialEnableCheck = new System.Windows.Forms.CheckBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.channelParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).BeginInit();
            this.timingParametersGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).BeginInit();
            this.acquisitionResultGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.chart)).BeginInit();
            this.writeToFileGroupBox.SuspendLayout();
            this.digitalOutputGroupBox.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.periodNumeric)).BeginInit();
            this.groupBox1.SuspendLayout();
            this.SuspendLayout();
            // 
            // channelParametersGroupBox
            // 
            this.channelParametersGroupBox.Controls.Add(this.physicalChannelComboBox);
            this.channelParametersGroupBox.Controls.Add(this.minimumValueNumeric);
            this.channelParametersGroupBox.Controls.Add(this.maximumValueNumeric);
            this.channelParametersGroupBox.Controls.Add(this.maximumLabel);
            this.channelParametersGroupBox.Controls.Add(this.minimumLabel);
            this.channelParametersGroupBox.Controls.Add(this.physicalChannelLabel);
            this.channelParametersGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.channelParametersGroupBox.Location = new System.Drawing.Point(8, 8);
            this.channelParametersGroupBox.Name = "channelParametersGroupBox";
            this.channelParametersGroupBox.Size = new System.Drawing.Size(224, 120);
            this.channelParametersGroupBox.TabIndex = 0;
            this.channelParametersGroupBox.TabStop = false;
            this.channelParametersGroupBox.Text = "Analog Input Channel Parameters";
            // 
            // physicalChannelComboBox
            // 
            this.physicalChannelComboBox.Location = new System.Drawing.Point(141, 26);
            this.physicalChannelComboBox.Name = "physicalChannelComboBox";
            this.physicalChannelComboBox.Size = new System.Drawing.Size(33, 21);
            this.physicalChannelComboBox.TabIndex = 1;
            this.physicalChannelComboBox.Text = "1";
            // 
            // minimumValueNumeric
            // 
            this.minimumValueNumeric.DecimalPlaces = 2;
            this.minimumValueNumeric.Location = new System.Drawing.Point(120, 56);
            this.minimumValueNumeric.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.minimumValueNumeric.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            -2147483648});
            this.minimumValueNumeric.Name = "minimumValueNumeric";
            this.minimumValueNumeric.Size = new System.Drawing.Size(96, 20);
            this.minimumValueNumeric.TabIndex = 3;
            this.minimumValueNumeric.Value = new decimal(new int[] {
            6,
            0,
            0,
            -2147483648});
            this.minimumValueNumeric.ValueChanged += new System.EventHandler(this.minimumValueNumeric_ValueChanged);
            // 
            // maximumValueNumeric
            // 
            this.maximumValueNumeric.DecimalPlaces = 2;
            this.maximumValueNumeric.Location = new System.Drawing.Point(120, 88);
            this.maximumValueNumeric.Maximum = new decimal(new int[] {
            10,
            0,
            0,
            0});
            this.maximumValueNumeric.Minimum = new decimal(new int[] {
            10,
            0,
            0,
            -2147483648});
            this.maximumValueNumeric.Name = "maximumValueNumeric";
            this.maximumValueNumeric.Size = new System.Drawing.Size(96, 20);
            this.maximumValueNumeric.TabIndex = 5;
            this.maximumValueNumeric.Value = new decimal(new int[] {
            6,
            0,
            0,
            0});
            // 
            // maximumLabel
            // 
            this.maximumLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.maximumLabel.Location = new System.Drawing.Point(15, 90);
            this.maximumLabel.Name = "maximumLabel";
            this.maximumLabel.Size = new System.Drawing.Size(112, 16);
            this.maximumLabel.TabIndex = 4;
            this.maximumLabel.Text = "Maximum Value (V):";
            // 
            // minimumLabel
            // 
            this.minimumLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.minimumLabel.Location = new System.Drawing.Point(15, 58);
            this.minimumLabel.Name = "minimumLabel";
            this.minimumLabel.Size = new System.Drawing.Size(104, 15);
            this.minimumLabel.TabIndex = 2;
            this.minimumLabel.Text = "Minimum Value (V):";
            // 
            // physicalChannelLabel
            // 
            this.physicalChannelLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.physicalChannelLabel.Location = new System.Drawing.Point(15, 28);
            this.physicalChannelLabel.Name = "physicalChannelLabel";
            this.physicalChannelLabel.Size = new System.Drawing.Size(124, 19);
            this.physicalChannelLabel.TabIndex = 0;
            this.physicalChannelLabel.Text = "Number of Channels (*):";
            // 
            // timingParametersGroupBox
            // 
            this.timingParametersGroupBox.Controls.Add(this.rateNumeric);
            this.timingParametersGroupBox.Controls.Add(this.samplesLabel);
            this.timingParametersGroupBox.Controls.Add(this.rateLabel);
            this.timingParametersGroupBox.Controls.Add(this.samplesPerChannelNumeric);
            this.timingParametersGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.timingParametersGroupBox.Location = new System.Drawing.Point(8, 140);
            this.timingParametersGroupBox.Name = "timingParametersGroupBox";
            this.timingParametersGroupBox.Size = new System.Drawing.Size(224, 92);
            this.timingParametersGroupBox.TabIndex = 1;
            this.timingParametersGroupBox.TabStop = false;
            this.timingParametersGroupBox.Text = "Timing Parameters";
            // 
            // rateNumeric
            // 
            this.rateNumeric.DecimalPlaces = 2;
            this.rateNumeric.Location = new System.Drawing.Point(120, 56);
            this.rateNumeric.Maximum = new decimal(new int[] {
            2000000,
            0,
            0,
            0});
            this.rateNumeric.Name = "rateNumeric";
            this.rateNumeric.Size = new System.Drawing.Size(96, 20);
            this.rateNumeric.TabIndex = 3;
            this.rateNumeric.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // samplesLabel
            // 
            this.samplesLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.samplesLabel.Location = new System.Drawing.Point(15, 26);
            this.samplesLabel.Name = "samplesLabel";
            this.samplesLabel.Size = new System.Drawing.Size(104, 16);
            this.samplesLabel.TabIndex = 0;
            this.samplesLabel.Text = "Samples/Channel:";
            // 
            // rateLabel
            // 
            this.rateLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.rateLabel.Location = new System.Drawing.Point(15, 58);
            this.rateLabel.Name = "rateLabel";
            this.rateLabel.Size = new System.Drawing.Size(56, 16);
            this.rateLabel.TabIndex = 2;
            this.rateLabel.Text = "Rate (Hz):";
            // 
            // samplesPerChannelNumeric
            // 
            this.samplesPerChannelNumeric.Location = new System.Drawing.Point(120, 24);
            this.samplesPerChannelNumeric.Maximum = new decimal(new int[] {
            2000000,
            0,
            0,
            0});
            this.samplesPerChannelNumeric.Name = "samplesPerChannelNumeric";
            this.samplesPerChannelNumeric.Size = new System.Drawing.Size(96, 20);
            this.samplesPerChannelNumeric.TabIndex = 1;
            this.samplesPerChannelNumeric.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.samplesPerChannelNumeric.ValueChanged += new System.EventHandler(this.samplesPerChannelNumeric_ValueChanged);
            // 
            // channNumObsLabel
            // 
            this.channNumObsLabel.Location = new System.Drawing.Point(0, 0);
            this.channNumObsLabel.Name = "channNumObsLabel";
            this.channNumObsLabel.Size = new System.Drawing.Size(100, 23);
            this.channNumObsLabel.TabIndex = 0;
            // 
            // acquisitionResultGroupBox
            // 
            this.acquisitionResultGroupBox.Controls.Add(this.chart);
            this.acquisitionResultGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.acquisitionResultGroupBox.Location = new System.Drawing.Point(240, 10);
            this.acquisitionResultGroupBox.Name = "acquisitionResultGroupBox";
            this.acquisitionResultGroupBox.Size = new System.Drawing.Size(356, 283);
            this.acquisitionResultGroupBox.TabIndex = 3;
            this.acquisitionResultGroupBox.TabStop = false;
            this.acquisitionResultGroupBox.Text = "Acquisition Results";
            // 
            // chart
            // 
            chartArea1.Name = "ChartArea";
            this.chart.ChartAreas.Add(chartArea1);
            legend1.Name = "Legend";
            this.chart.Legends.Add(legend1);
            this.chart.Location = new System.Drawing.Point(8, 40);
            this.chart.Name = "chart";
            series1.ChartArea = "ChartArea";
            series1.Legend = "Legend";
            series1.Name = "Series";
            this.chart.Series.Add(series1);
            this.chart.Size = new System.Drawing.Size(342, 201);
            this.chart.TabIndex = 0;
            this.chart.Text = "chart";
            this.chart.Click += new System.EventHandler(this.chart_Click);
            // 
            // filePathWriteTextBox
            // 
            this.filePathWriteTextBox.Enabled = false;
            this.filePathWriteTextBox.Location = new System.Drawing.Point(77, 87);
            this.filePathWriteTextBox.Name = "filePathWriteTextBox";
            this.filePathWriteTextBox.ReadOnly = true;
            this.filePathWriteTextBox.Size = new System.Drawing.Size(240, 20);
            this.filePathWriteTextBox.TabIndex = 4;
            this.filePathWriteTextBox.Text = "Choose file location";
            this.filePathWriteTextBox.TextChanged += new System.EventHandler(this.filePathWriteTextBox_TextChanged);
            // 
            // writeToFileSaveFileDialog
            // 
            this.writeToFileSaveFileDialog.CreatePrompt = true;
            this.writeToFileSaveFileDialog.DefaultExt = "txt";
            this.writeToFileSaveFileDialog.FileName = "acquisitionData.txt";
            this.writeToFileSaveFileDialog.Filter = "Text Files|*.txt| All Files|*.*";
            this.writeToFileSaveFileDialog.Title = "Save Acquisition Data To File";
            // 
            // readFromFileOpenFileDialog
            // 
            this.readFromFileOpenFileDialog.DefaultExt = "txt";
            this.readFromFileOpenFileDialog.FileName = "acquisitionData.txt";
            this.readFromFileOpenFileDialog.Filter = "Text Files|*.txt| All Files|*.*";
            this.readFromFileOpenFileDialog.Title = "Open Acquisition Data";
            // 
            // writeToFileGroupBox
            // 
            this.writeToFileGroupBox.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.writeToFileGroupBox.Controls.Add(this.ToFileEnableCheck);
            this.writeToFileGroupBox.Controls.Add(this.stopButton);
            this.writeToFileGroupBox.Controls.Add(this.label1);
            this.writeToFileGroupBox.Controls.Add(this.startButton);
            this.writeToFileGroupBox.Controls.Add(this.browseWriteButton);
            this.writeToFileGroupBox.Controls.Add(this.filePathWriteLabel);
            this.writeToFileGroupBox.Controls.Add(this.binaryFileWriteRadioButton);
            this.writeToFileGroupBox.Controls.Add(this.textFileWriteRadioButton);
            this.writeToFileGroupBox.Controls.Add(this.filePathWriteTextBox);
            this.writeToFileGroupBox.Controls.Add(this.fileTypeWriteLabel);
            this.writeToFileGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.writeToFileGroupBox.Location = new System.Drawing.Point(240, 299);
            this.writeToFileGroupBox.Name = "writeToFileGroupBox";
            this.writeToFileGroupBox.Size = new System.Drawing.Size(359, 198);
            this.writeToFileGroupBox.TabIndex = 2;
            this.writeToFileGroupBox.TabStop = false;
            this.writeToFileGroupBox.Text = "Write To File";
            // 
            // ToFileEnableCheck
            // 
            this.ToFileEnableCheck.AutoSize = true;
            this.ToFileEnableCheck.Location = new System.Drawing.Point(95, 30);
            this.ToFileEnableCheck.Name = "ToFileEnableCheck";
            this.ToFileEnableCheck.Size = new System.Drawing.Size(15, 14);
            this.ToFileEnableCheck.TabIndex = 13;
            this.ToFileEnableCheck.UseVisualStyleBackColor = true;
            this.ToFileEnableCheck.CheckedChanged += new System.EventHandler(this.ToFileEnableCheck_CheckedChanged);
            // 
            // stopButton
            // 
            this.stopButton.Enabled = false;
            this.stopButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.stopButton.Location = new System.Drawing.Point(188, 137);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(80, 23);
            this.stopButton.TabIndex = 7;
            this.stopButton.Text = "Stop";
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // label1
            // 
            this.label1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label1.Location = new System.Drawing.Point(12, 27);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(104, 17);
            this.label1.TabIndex = 12;
            this.label1.Text = "Enable Writting:";
            // 
            // startButton
            // 
            this.startButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.startButton.Location = new System.Drawing.Point(92, 137);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(80, 23);
            this.startButton.TabIndex = 6;
            this.startButton.Text = "Start";
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // browseWriteButton
            // 
            this.browseWriteButton.Enabled = false;
            this.browseWriteButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.browseWriteButton.Location = new System.Drawing.Point(322, 85);
            this.browseWriteButton.Name = "browseWriteButton";
            this.browseWriteButton.Size = new System.Drawing.Size(24, 23);
            this.browseWriteButton.TabIndex = 5;
            this.browseWriteButton.Text = "...";
            this.browseWriteButton.Click += new System.EventHandler(this.browseWriteButton_Click);
            // 
            // filePathWriteLabel
            // 
            this.filePathWriteLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.filePathWriteLabel.Location = new System.Drawing.Point(12, 89);
            this.filePathWriteLabel.Name = "filePathWriteLabel";
            this.filePathWriteLabel.Size = new System.Drawing.Size(56, 19);
            this.filePathWriteLabel.TabIndex = 3;
            this.filePathWriteLabel.Text = "File Path:";
            // 
            // binaryFileWriteRadioButton
            // 
            this.binaryFileWriteRadioButton.Enabled = false;
            this.binaryFileWriteRadioButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.binaryFileWriteRadioButton.Location = new System.Drawing.Point(168, 57);
            this.binaryFileWriteRadioButton.Name = "binaryFileWriteRadioButton";
            this.binaryFileWriteRadioButton.Size = new System.Drawing.Size(72, 15);
            this.binaryFileWriteRadioButton.TabIndex = 2;
            this.binaryFileWriteRadioButton.Text = "Binary File";
            this.binaryFileWriteRadioButton.CheckedChanged += new System.EventHandler(this.binaryFileWriteRadioButton_CheckedChanged);
            // 
            // textFileWriteRadioButton
            // 
            this.textFileWriteRadioButton.Checked = true;
            this.textFileWriteRadioButton.Enabled = false;
            this.textFileWriteRadioButton.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.textFileWriteRadioButton.Location = new System.Drawing.Point(96, 57);
            this.textFileWriteRadioButton.Name = "textFileWriteRadioButton";
            this.textFileWriteRadioButton.Size = new System.Drawing.Size(72, 15);
            this.textFileWriteRadioButton.TabIndex = 1;
            this.textFileWriteRadioButton.TabStop = true;
            this.textFileWriteRadioButton.Text = "Text File";
            this.textFileWriteRadioButton.CheckedChanged += new System.EventHandler(this.textFileWriteRadioButton_CheckedChanged);
            // 
            // fileTypeWriteLabel
            // 
            this.fileTypeWriteLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.fileTypeWriteLabel.Location = new System.Drawing.Point(12, 57);
            this.fileTypeWriteLabel.Name = "fileTypeWriteLabel";
            this.fileTypeWriteLabel.Size = new System.Drawing.Size(72, 15);
            this.fileTypeWriteLabel.TabIndex = 0;
            this.fileTypeWriteLabel.Text = "File Type:";
            // 
            // ChnnNumObsLabel
            // 
            this.ChnnNumObsLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.ChnnNumObsLabel.Font = new System.Drawing.Font("Microsoft Sans Serif", 7F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.ChnnNumObsLabel.Location = new System.Drawing.Point(17, 498);
            this.ChnnNumObsLabel.Name = "ChnnNumObsLabel";
            this.ChnnNumObsLabel.Size = new System.Drawing.Size(585, 29);
            this.ChnnNumObsLabel.TabIndex = 9;
            this.ChnnNumObsLabel.Text = "(*) This App monitors the DAQmx\'s  Physical AI Channels AI0 to AIx, where x is th" +
    "e selected Number of Channels minus one. If different AI channels are required, " +
    "please specify directly in code.";
            this.ChnnNumObsLabel.Click += new System.EventHandler(this.ChnnNumObsLabel_Click_1);
            // 
            // digitalOutputGroupBox
            // 
            this.digitalOutputGroupBox.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.digitalOutputGroupBox.Controls.Add(this.periodNumeric);
            this.digitalOutputGroupBox.Controls.Add(this.enableSwitchingCheckBox);
            this.digitalOutputGroupBox.Controls.Add(this.periodLabel);
            this.digitalOutputGroupBox.Controls.Add(this.DOChannel2ComboBox);
            this.digitalOutputGroupBox.Controls.Add(this.DOChannel2Label);
            this.digitalOutputGroupBox.Controls.Add(this.DOChannel1ComboBox);
            this.digitalOutputGroupBox.Controls.Add(this.DOChannel1Label);
            this.digitalOutputGroupBox.Controls.Add(this.enableSwitchingLabel);
            this.digitalOutputGroupBox.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.digitalOutputGroupBox.Location = new System.Drawing.Point(8, 243);
            this.digitalOutputGroupBox.Name = "digitalOutputGroupBox";
            this.digitalOutputGroupBox.Size = new System.Drawing.Size(224, 154);
            this.digitalOutputGroupBox.TabIndex = 8;
            this.digitalOutputGroupBox.TabStop = false;
            this.digitalOutputGroupBox.Text = "Digital Output Channel Switching";
            this.digitalOutputGroupBox.Enter += new System.EventHandler(this.groupBox1_Enter);
            // 
            // periodNumeric
            // 
            this.periodNumeric.DecimalPlaces = 2;
            this.periodNumeric.Location = new System.Drawing.Point(120, 118);
            this.periodNumeric.Maximum = new decimal(new int[] {
            100000,
            0,
            0,
            0});
            this.periodNumeric.Name = "periodNumeric";
            this.periodNumeric.Size = new System.Drawing.Size(96, 20);
            this.periodNumeric.TabIndex = 5;
            this.periodNumeric.Value = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            // 
            // enableSwitchingCheckBox
            // 
            this.enableSwitchingCheckBox.AutoSize = true;
            this.enableSwitchingCheckBox.Location = new System.Drawing.Point(120, 27);
            this.enableSwitchingCheckBox.Name = "enableSwitchingCheckBox";
            this.enableSwitchingCheckBox.Size = new System.Drawing.Size(15, 14);
            this.enableSwitchingCheckBox.TabIndex = 11;
            this.enableSwitchingCheckBox.UseVisualStyleBackColor = true;
            this.enableSwitchingCheckBox.CheckedChanged += new System.EventHandler(this.enableSwitchingCheckBox_CheckedChanged);
            // 
            // periodLabel
            // 
            this.periodLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.periodLabel.Location = new System.Drawing.Point(15, 121);
            this.periodLabel.Name = "periodLabel";
            this.periodLabel.Size = new System.Drawing.Size(104, 18);
            this.periodLabel.TabIndex = 4;
            this.periodLabel.Text = "Period (ms):";
            this.periodLabel.Click += new System.EventHandler(this.label4_Click);
            // 
            // DOChannel2ComboBox
            // 
            this.DOChannel2ComboBox.Location = new System.Drawing.Point(120, 83);
            this.DOChannel2ComboBox.Name = "DOChannel2ComboBox";
            this.DOChannel2ComboBox.Size = new System.Drawing.Size(96, 21);
            this.DOChannel2ComboBox.TabIndex = 10;
            this.DOChannel2ComboBox.Text = "1";
            this.DOChannel2ComboBox.SelectedIndexChanged += new System.EventHandler(this.DOChannel2ComboBox_SelectedIndexChanged);
            // 
            // DOChannel2Label
            // 
            this.DOChannel2Label.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.DOChannel2Label.Location = new System.Drawing.Point(15, 87);
            this.DOChannel2Label.Name = "DOChannel2Label";
            this.DOChannel2Label.Size = new System.Drawing.Size(72, 16);
            this.DOChannel2Label.TabIndex = 5;
            this.DOChannel2Label.Text = "Channel 2:";
            // 
            // DOChannel1ComboBox
            // 
            this.DOChannel1ComboBox.Location = new System.Drawing.Point(120, 50);
            this.DOChannel1ComboBox.Name = "DOChannel1ComboBox";
            this.DOChannel1ComboBox.Size = new System.Drawing.Size(96, 21);
            this.DOChannel1ComboBox.TabIndex = 4;
            this.DOChannel1ComboBox.Text = "1";
            // 
            // DOChannel1Label
            // 
            this.DOChannel1Label.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.DOChannel1Label.Location = new System.Drawing.Point(15, 53);
            this.DOChannel1Label.Name = "DOChannel1Label";
            this.DOChannel1Label.Size = new System.Drawing.Size(72, 16);
            this.DOChannel1Label.TabIndex = 3;
            this.DOChannel1Label.Text = "Channel 1:";
            // 
            // enableSwitchingLabel
            // 
            this.enableSwitchingLabel.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.enableSwitchingLabel.Location = new System.Drawing.Point(15, 24);
            this.enableSwitchingLabel.Name = "enableSwitchingLabel";
            this.enableSwitchingLabel.Size = new System.Drawing.Size(104, 17);
            this.enableSwitchingLabel.TabIndex = 0;
            this.enableSwitchingLabel.Text = "Enable Switching:";
            this.enableSwitchingLabel.Click += new System.EventHandler(this.label2_Click);
            // 
            // comPortComboBox
            // 
            this.comPortComboBox.Location = new System.Drawing.Point(120, 50);
            this.comPortComboBox.Name = "comPortComboBox";
            this.comPortComboBox.Size = new System.Drawing.Size(96, 21);
            this.comPortComboBox.TabIndex = 10;
            this.comPortComboBox.Text = "1";
            this.comPortComboBox.SelectedIndexChanged += new System.EventHandler(this.comPortComboBox_SelectedIndexChanged);
            // 
            // SerialEnableCheck
            // 
            this.SerialEnableCheck.AutoSize = true;
            this.SerialEnableCheck.Location = new System.Drawing.Point(120, 27);
            this.SerialEnableCheck.Name = "SerialEnableCheck";
            this.SerialEnableCheck.Size = new System.Drawing.Size(15, 14);
            this.SerialEnableCheck.TabIndex = 12;
            this.SerialEnableCheck.UseVisualStyleBackColor = true;
            // 
            // groupBox1
            // 
            this.groupBox1.AccessibleRole = System.Windows.Forms.AccessibleRole.None;
            this.groupBox1.Controls.Add(this.label3);
            this.groupBox1.Controls.Add(this.comPortComboBox);
            this.groupBox1.Controls.Add(this.SerialEnableCheck);
            this.groupBox1.Controls.Add(this.label4);
            this.groupBox1.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.groupBox1.Location = new System.Drawing.Point(8, 407);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(224, 86);
            this.groupBox1.TabIndex = 12;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Serial Reader";
            // 
            // label3
            // 
            this.label3.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label3.Location = new System.Drawing.Point(15, 55);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(72, 16);
            this.label3.TabIndex = 3;
            this.label3.Text = "Port Name:";
            // 
            // label4
            // 
            this.label4.FlatStyle = System.Windows.Forms.FlatStyle.System;
            this.label4.Location = new System.Drawing.Point(15, 24);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(104, 17);
            this.label4.TabIndex = 0;
            this.label4.Text = "Enable Reading:";
            // 
            // MainForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(607, 538);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.digitalOutputGroupBox);
            this.Controls.Add(this.ChnnNumObsLabel);
            this.Controls.Add(this.writeToFileGroupBox);
            this.Controls.Add(this.acquisitionResultGroupBox);
            this.Controls.Add(this.timingParametersGroupBox);
            this.Controls.Add(this.channelParametersGroupBox);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Continuous Acquisition of Voltage Samples - Int Clk - Write to File";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.channelParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.minimumValueNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.maximumValueNumeric)).EndInit();
            this.timingParametersGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.rateNumeric)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.samplesPerChannelNumeric)).EndInit();
            this.acquisitionResultGroupBox.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.chart)).EndInit();
            this.writeToFileGroupBox.ResumeLayout(false);
            this.writeToFileGroupBox.PerformLayout();
            this.digitalOutputGroupBox.ResumeLayout(false);
            this.digitalOutputGroupBox.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.periodNumeric)).EndInit();
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() 
        {
            Application.EnableVisualStyles();
            Application.DoEvents();
            Application.Run(new MainForm());
        }

        private void browseWriteButton_Click(object sender, System.EventArgs e)
        {
            if (textFileWriteRadioButton.Checked) 
            {
                useTextFileWrite = true;
                writeToFileSaveFileDialog.DefaultExt = "*.txt";
                writeToFileSaveFileDialog.FileName = "acquisitionData.txt";
                writeToFileSaveFileDialog.Filter = "Text Files|*.txt|All Files|*.*";
            }
            else
            {
                useTextFileWrite = false;
                writeToFileSaveFileDialog.DefaultExt = "*.bin";
                writeToFileSaveFileDialog.FileName = "acquisitionData.bin";
                writeToFileSaveFileDialog.Filter = "Binary Files|*.bin|All Files|*.*";
            }

            // Display Save File Dialog (Windows forms control)
            DialogResult result = writeToFileSaveFileDialog.ShowDialog();

            if (result == DialogResult.OK)
            {
                fileNameWrite = writeToFileSaveFileDialog.FileName;
                filePathWriteTextBox.Text = fileNameWrite;
                fileToolTip.SetToolTip(filePathWriteTextBox, fileNameWrite);
                startButton.Enabled = true;
            }
        }

        private void startButton_Click(object sender, System.EventArgs e)
        {
            if (runningAITask == null)
            {
                try 
                {   
                    if (ToFileEnableCheck.Checked)
                    {
                        // Create a new file for data
                        bool opened = CreateDataFile();
                        if (!opened)
                        {
                            return;
                        }
                    }

                    // Modify the UIfor
                    stopButton.Enabled = true;
                    startButton.Enabled = false;

                    //Create a new task
                    AITask = new Task();
                    DOTask = new Task();

                    //Create a virtual channel
                    String AIchnnName = null;
                    int i = 0;
                    for (i = 0; i < Convert.ToInt32(physicalChannelComboBox.Text); i++)
                    {
                        if (i == 0) AIchnnName = $"{physicalChannelNames[i]}";
                        else AIchnnName = $"{AIchnnName}, {physicalChannelNames[i]}";
                    }

                    String DOchnnName = null;
                    DOchnnName = $"Dev2/port1/line0:3";
                    defaultChnn = Int16.Parse(DOChannel1ComboBox.Text[15].ToString());
                    switchChnn = Int16.Parse(DOChannel2ComboBox.Text[15].ToString());
                    //if (!DOChannel2ComboBox.Enabled) DOchnnName = DOChannel1ComboBox.Text;
                    //else DOchnnName = $"{DOChannel1ComboBox.Text}, {DOChannel2ComboBox.Text}";
                    AITerminalConfiguration conf = AITerminalConfiguration.Rse;

                    // Analog Input Channel
                    /*AITask.AIChannels.CreateVoltageChannel(AIchnnName, "",
                    (AITerminalConfiguration)(-1), Convert.ToDouble(minimumValueNumeric.Value),
                    Convert.ToDouble(maximumValueNumeric.Value), AIVoltageUnits.Volts);*/

                    AITask.AIChannels.CreateVoltageChannel(AIchnnName, "",
                    conf, Convert.ToDouble(minimumValueNumeric.Value),
                    Convert.ToDouble(maximumValueNumeric.Value), AIVoltageUnits.Volts);

                    // Digital Output Channel
                    DOTask.DOChannels.CreateChannel(DOchnnName, "",
                    ChannelLineGrouping.OneChannelForAllLines);

                    //Configure the timing parameters
                    AITask.Timing.ConfigureSampleClock("", Convert.ToDouble(rateNumeric.Value),
                        SampleClockActiveEdge.Rising, SampleQuantityMode.ContinuousSamples, 1000);
                    
                    //Verify the Task
                    AITask.Control(TaskAction.Verify);
                    DOTask.Control(TaskAction.Verify);

                    //Prepare the table and file for Data
                    String[] channelNames = new String[AITask.AIChannels.Count];
                    i = 0;
                    foreach (AIChannel a in AITask.AIChannels)
                    {
                        channelNames[i++] = a.PhysicalName;
                    }

                    // Add the channel names (and any other information) to the file                    
                    int samples = Convert.ToInt32(samplesPerChannelNumeric.Value);
                    if (ToFileEnableCheck.Checked)
                    {
                        PrepareFileForData();
                    }
                    savedData = new ArrayList();
                    for (i = 0; i < AITask.AIChannels.Count; i++)
                    {
                        savedData.Add(new ArrayList());

                    }
                    daqTimestamp = new ArrayList();

                    // Initialize Chart
                    for (i = this.chart.Series.Count; i < AITask.AIChannels.Count; i++)
                    {
                            Series newSerie = new Series();
                            newSerie.ChartArea = "ChartArea";
                            newSerie.Legend = "Legend";
                            newSerie.Name = $"Series,{i}";
                            this.chart.Series.Add(newSerie);
                            this.chart.Series[i].ChartType = SeriesChartType.Spline;
                            this.chart.Series[i].BorderWidth = 2;
                    }

                    // Instantiate the running Tasks
                    runningAITask = AITask;
                    runningDOTask = DOTask;

                    // Initiate the analog Input Reader Callback
                    analogInReader = new AnalogMultiChannelReader(AITask.Stream);
                    // Use SynchronizeCallbacks to specify that the object 
                    // marshals callbacks across threads appropriately.
                    analogInReader.SynchronizeCallbacks = true;
                    if (ToFileEnableCheck.Checked) analogCallback = new AsyncCallback(AnalogInCallbackStoreData);
                    else analogCallback = new AsyncCallback(AnalogInCallback);
                    analogInReader.BeginReadMultiSample(samples, analogCallback, AITask);
                    stopwatch = Stopwatch.StartNew();

                    // Chart Callback
                    chartTimer = new System.Timers.Timer(chartUpdateRate);
                    chartTimer.Elapsed += new System.Timers.ElapsedEventHandler(UpdateChart_Tick);
                    chartTimer.AutoReset = true;
                    chartTimer.Enabled = true;
                    chartTimer.Start();

                    // Initiate the Digital Output Writter Event
                    digitalOutWriter = new DigitalSingleChannelWriter(DOTask.Stream);                    
                    DOData = new bool[4];
                    for (i=0; i < 4; i++)
                    {
                        if (i == defaultChnn) DOData[i] = true;
                        else DOData[i] = false;
                    }
                    digitalOutWriter.WriteSingleSampleMultiLine(true, DOData);
                    
                    if (enableSwitchingCheckBox.Checked)
                    {
                        statusCheckTimer = new System.Timers.Timer(Convert.ToInt32(periodNumeric.Value));
                        statusCheckTimer.Elapsed += statusCheckTimer_Tick;
                        statusCheckTimer.AutoReset = true;
                        statusCheckTimer.Enabled = true;
                    }

                    // Serial Callback
                    if (SerialEnableCheck.Checked)
                    {
                        _serialPort = new SerialPort();
                        sensorTimestamp = new ArrayList();
                        savedSensorData = new ArrayList();
                        for (i = 0; i < 1; i++)
                        {
                            savedSensorData.Add(new ArrayList());
                        }
                        _serialPort.PortName = comPortComboBox.Text;
                        _serialPort.BaudRate = 115200;
                        // Set the read/write timeouts
                        _serialPort.ReadTimeout = 500;
                        _serialPort.WriteTimeout = 500;
                        _serialPort.Open();
                        _serialPort.DataReceived += OnScan;
                    }

                }
                catch (DaqException exception)
                {
                    //Display Errors
                    MessageBox.Show(exception.Message);
                    runningAITask = null;
                    runningDOTask = null;
                    AITask.Dispose();
                    DOTask.Dispose();
                    stopwatch.Stop();
                    stopButton.Enabled = false;
                    startButton.Enabled = true;
                    if (enableSwitchingCheckBox.Checked)
                    {
                        statusCheckTimer.Stop();
                        statusCheckTimer.Dispose();
                    }
                    writeToFileGroupBox.Enabled = true;
                }           
            }
        }

        private void InitializeChart()
        {
            this.chart.ChartAreas[0].BackColor = Color.Transparent;
            this.chart.ChartAreas[0].AxisX.Title = "Time (s)";
            this.chart.ChartAreas[0].AxisY.Title = "Voltage [V]";
            this.chart.ChartAreas[0].Position.Width = 100;
            this.chart.ChartAreas[0].Position.Height = 100;
            this.chart.Legends[0].Enabled = false;
            this.chart.BackColor = Color.Transparent; 
            this.chart.ChartAreas[0].AxisY.IsStartedFromZero = false;
            this.chart.ChartAreas[0].AxisY.LabelStyle.Format = "0.000";
            this.chart.ChartAreas[0].AxisX.LabelStyle.Format = "0.00";
            this.chart.Series[0].ChartType = SeriesChartType.Spline;
            this.chart.Series[0].BorderWidth = 2;

            //this.chart.ChartAreas[0].AxisY.ScaleBreakStyle.Enabled = true;
            //this.chart.ChartAreas[0].AxisY.ScaleBreakStyle.CollapsibleSpaceThreshold = 25;
            //this.chart.ChartAreas[0].AxisY.ScaleBreakStyle.LineColor = Color.Red;  
            //this.chart.ChartAreas[0].AxisY.ScaleBreakStyle.StartFromZero = StartFromZero.Auto;
            //this.chart.ChartAreas[0].AxisY.ScaleBreakStyle.Spacing = 2;

            this.chart.ChartAreas[0].AxisX.MajorGrid.LineColor = Color.DarkGray;
            this.chart.ChartAreas[0].AxisX.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
            this.chart.ChartAreas[0].AxisY.MajorGrid.LineColor = Color.DarkGray;
            this.chart.ChartAreas[0].AxisY.MajorGrid.LineDashStyle = ChartDashStyle.Dash;
        }

        private void UpdateChart_Tick(object sender, System.EventArgs e)
        {
            double[,] myData = data;
            UpdateChart(myData);
        }

        delegate void UpdateChartCallback(double[,] myData);
        private void UpdateChart(double[,] myData)
        {
            // InvokeRequired required compares the thread ID of the
            // calling thread to the thread ID of the creating thread.
            // If these threads are different, it returns true.
            if (this.chart.InvokeRequired)
            {
                UpdateChartCallback d = new UpdateChartCallback(UpdateChart);
                try
                {
                    this.Invoke(d, new object[] { myData });
                }
                catch
                {

                }
            }
            else
            {
                try
                {
                    if (myData != null && !myData.Equals(lastData))
                    {
                        int channelCount = myData.GetLength(0);
                        int dataCount = myData.GetLength(1);
                        double min = Int32.MaxValue;
                        double max = Int32.MinValue;
                        double minCandidate = Int32.MaxValue;
                        double maxCandidate = Int32.MinValue;
                        double maxRounded = 0;
                        double minRounded = 0;
                        int interpRate = 1;
                        double xValue = Convert.ToInt32(xAxisCounter) * chartUpdateRate * 0.001;
                        double[] maxOnScreen = new double[channelCount];
                        double[] minOnScreen = new double[channelCount];

                        if (dataCount > chartMaxPoints)
                        {
                            interpRate = Convert.ToInt32(dataCount / chartMaxPoints);
                            dataCount = chartMaxPoints;
                        }
                        Console.WriteLine($"Counter: {chartCounter}");
                        if (chartCounter == 0)
                        {
                           if (this.chart.ChartAreas[0] != null) this.chart.ChartAreas[0].BackColor = Color.LightGray;
                            for (int j = 0; j < channelCount; j++)
                            {
                                for (int i = 0; i < dataCount; i++)
                                {
                                    double interpSample = myData[j, interpRate * i];
                                    if(this.chart.Series[j].Points != null) this.chart.Series[j].Points.AddXY(xValue.ToString("F2"), interpSample);
                                    if (interpSample > maxCandidate) maxCandidate = interpSample;
                                    if (interpSample < minCandidate) minCandidate = interpSample;
                                }

                                //Round the maximum and minimum values
                                maxRounded = Math.Ceiling(((maxCandidate) * 1000)) / 1000;
                                minRounded = (Math.Floor(((minCandidate) * 1000)) / 1000);
                            }
                        }
                        else
                        {
                            for (int j = 0; j < channelCount; j++)
                            {
                                for (int i = 0; i < dataCount; i++)
                                {
                                    double interpSample = myData[j, interpRate * i];
                                    if (Convert.ToInt32(chartCounter) * dataCount >= chartMaxPoints) this.chart.Series[j].Points.RemoveAt(0);
                                    this.chart.Series[j].Points.AddXY(xValue.ToString("F2"), interpSample);
                                    if (interpSample > maxCandidate) maxCandidate = interpSample;
                                    if (interpSample < minCandidate) minCandidate = interpSample;
                                }
                            }

                            for (int j = 0; j < channelCount; j++)
                            {
                                if(this.chart.Series[j].Points != null)
                                {
                                    maxOnScreen[j] = this.chart.Series[j].Points.FindMaxByValue().YValues[0];
                                    minOnScreen[j] = this.chart.Series[j].Points.FindMinByValue().YValues[0];
                                }
                            }

                            //Get the maximum and minimum values on the chart
                            max = maxOnScreen.Max();
                            maxRounded = max;
                            min = minOnScreen.Min();
                            minRounded = min;
                        }

                        //Add a tolerance to  maximum and minimum values
                        if (maxRounded != minRounded)
                        {
                            double dSginal = maxRounded - minRounded;
                            maxRounded = maxRounded + 0.1 * dSginal;
                            minRounded = minRounded - 0.1 * dSginal;
                        } 
                        else
                        {
                            maxRounded = maxRounded + Math.Abs(maxRounded) * 0.1;
                            minRounded = minRounded - Math.Abs(minRounded) * 0.1;
                        }

                        // Change the chat's y-axis
                        if (maxRounded != lastMax)
                        {
                            lastMax = maxRounded;
                            if (this.chart.ChartAreas[0].AxisY != null) this.chart.ChartAreas[0].AxisY.Maximum = maxRounded;
                        }
                        if (minRounded != lastMin)
                        {
                            lastMin = minRounded;
                            if (this.chart.ChartAreas[0].AxisY != null) this.chart.ChartAreas[0].AxisY.Minimum = minRounded;
                        }

                        chartCounter++;
                        //this.chart.ChartAreas[0].AxisY.Interval = (maxRounded - minRounded) / 5;
                        //var s = string.Format("{0:0.00}", maxRounded);
                       // Console.WriteLine(s);
                        //s = string.Format("{0:0.00}", minRounded);
                        //Console.WriteLine(s);
                    }
                    lastData = myData;
                    xAxisCounter++;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.TargetSite.ToString());
                    string elapsedTime = stopwatch.Elapsed.TotalMilliseconds.ToString("F4");
                    CloseFile(elapsedTime);
                    runningAITask = null;
                    runningDOTask = null;
                    AITask.Dispose();
                    DOTask.Dispose();
                    chartTimer.Stop();
                    chartTimer.Dispose();
                    stopButton.Enabled = false;
                    startButton.Enabled = true;
                    if (enableSwitchingCheckBox.Checked)
                    {
                        statusCheckTimer.Stop();
                        statusCheckTimer.Dispose();
                    }
                    stopwatch.Stop();

                }
            }
        }

        private void AnalogInCallback(IAsyncResult ar)
        {
            try
            {
                if(runningAITask != null && runningAITask == ar.AsyncState)
                {
                    //Read the available data from the channels
                    data = analogInReader.EndReadMultiSample(ar);                   

                    analogInReader.BeginReadMultiSample(Convert.ToInt32(samplesPerChannelNumeric.Value),
                        analogCallback, AITask);
                }
            }
            catch(DaqException exception)
            {   
                //Display Errors
                MessageBox.Show(exception.Message);
                runningAITask = null;
                runningDOTask = null;
                AITask.Dispose();
                DOTask.Dispose();
                stopwatch.Stop();
                stopButton.Enabled = false;
                startButton.Enabled = true;
                if (enableSwitchingCheckBox.Checked)
                {
                    statusCheckTimer.Stop();
                    statusCheckTimer.Dispose();
                }
            }
        }

        private void AnalogInCallbackStoreData(IAsyncResult ar)
        {
            try
            {
                if (runningAITask != null && runningAITask == ar.AsyncState)
                {
                    //Read the available data from the channels
                    data = analogInReader.EndReadMultiSample(ar);

                    //Store data
                    stopwatchCounter = stopwatch.Elapsed;

                    LogData(ref savedData, data);
                    LogTimespan(ref daqTimestamp, stopwatchCounter, Convert.ToInt32(samplesPerChannelNumeric.Value));

                    analogInReader.BeginReadMultiSample(Convert.ToInt32(samplesPerChannelNumeric.Value),
                        analogCallback, AITask);
                }
            }
            catch (DaqException exception)
            {
                //Display Errors
                MessageBox.Show(exception.Message);
                runningAITask = null;
                runningDOTask = null;
                AITask.Dispose();
                DOTask.Dispose();
                stopwatch.Stop();
                stopButton.Enabled = false;
                startButton.Enabled = true;
                if (enableSwitchingCheckBox.Checked)
                {
                    statusCheckTimer.Stop();
                    statusCheckTimer.Dispose();
                }
            }
        }

        private void statusCheckTimer_Tick(object sender, System.EventArgs e)
        {
            try
            {
                if (runningDOTask != null)
                {
                    statusCheckTimer.Stop();

                    // Prepare Data
                    DOData[defaultChnn] = !DOData[defaultChnn];
                    DOData[switchChnn] = !DOData[switchChnn];

                    digitalOutWriter.WriteSingleSampleMultiLine(true, DOData);
                    statusCheckTimer.Enabled = true;
                }
            }
            catch (DaqException ex)
            {
                statusCheckTimer.Enabled = false;
                System.Windows.Forms.MessageBox.Show(ex.Message);
                runningAITask = null;
                runningDOTask = null;
                AITask.Dispose();
                DOTask.Dispose();
                stopwatch.Stop();
                statusCheckTimer.Stop();
                statusCheckTimer.Dispose();
                stopButton.Enabled = false;
                startButton.Enabled = true;
            }
        }
        void OnScan(object sender, SerialDataReceivedEventArgs args)
        {
            SerialPort port = sender as SerialPort;
            try
            {
                string message = port.ReadLine();
                string[] splitMessage = message.Split(',');
                if (splitMessage.Length > 0)
                {                        
                    double[,] mtData = new double[splitMessage.Length, 1];
                    for (int i = 0; i < splitMessage.Length; i++)
                    {
                        double result;
                        bool convSuccessful = double.TryParse(splitMessage[i], System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out result);
                        if (convSuccessful)
                        {
                            mtData[i, 0] = result;
                        }
                        else
                        {
                            mtData[i, 0] = 0;
                        }
                            
                    }                        
                    LogData(ref savedSensorData, mtData);
                    spStopwatchCounter = stopwatch.Elapsed;
                    LogTimespan(ref sensorTimestamp, spStopwatchCounter, 0);
                 
                }
                //Console.WriteLine("Thread {0}: Elapsed {1:N4} seconds",
                //Thread.CurrentThread.ManagedThreadId,
                //sw.ElapsedMilliseconds / 1000.0);

                //Console.WriteLine(message);
            }
            catch (TimeoutException) { }
        }



        // Display Port values and prompt user to enter a port.
        public static void GetAvailablePortNames()
        {
            Console.WriteLine("Available Ports:");
            foreach (string s in SerialPort.GetPortNames())
            {
                Console.WriteLine("   {0}", s);
            }
        }

        private void stopButton_Click(object sender, System.EventArgs e)
        {
            if (runningAITask != null)
            {

                stopwatch.Stop();
                //string elapsedTime = String.Format("{0:00}:{1:00}:{2:00}.{3:00}",
                //ts.Hours, ts.Minutes, ts.Seconds, ts.Milliseconds/10);
                //string elapsedTime = stopWatchCounter.TotalMilliseconds.ToString("F4");
                string elapsedTime = stopwatch.Elapsed.TotalMilliseconds.ToString("F4");
                if (ToFileEnableCheck.Checked)
                {
                    CloseFile(elapsedTime);
                }
                chartTimer.Stop();
                chartTimer.Dispose();
                //Clear chart
                for (int i = 0; i < Convert.ToInt32(physicalChannelComboBox.Text); i++)
                {
                    if (i == 0) chart.Series[i].Points.Clear();
                    else chart.Series.RemoveAt(Convert.ToInt32(physicalChannelComboBox.Text)-i);

                }
                chartCounter = 0;
                this.chart.ChartAreas[0].BackColor = Color.Transparent;

                //Dispose of the task
                runningAITask = null;
                AITask.Dispose();
                runningDOTask = null;
                DOTask.Dispose();

                if (SerialEnableCheck.Checked)
                {
                    _serialPort.Close();
                }

                if (enableSwitchingCheckBox.Checked)
                {
                    statusCheckTimer.Stop();
                    statusCheckTimer.Dispose();
                }

                stopButton.Enabled = false;
                startButton.Enabled = true;
                writeToFileGroupBox.Enabled = true;

            }
        }

        private void DisplayData(double[,] sourceArray, ref DataTable dataTable)
        {   
            //Display the first points of the Read/Write in the Datagrid
            try
            {
                int channelCount = sourceArray.GetLength(0);
                int dataCount;
                
                if (sourceArray.GetLength(1) < tableMaximumRows)
                    dataCount = sourceArray.GetLength(1);
                else
                    dataCount = tableMaximumRows;
                
                // Write to Data Table
                for (int i = 0; i < dataCount; i++)             
                {
                    for (int j = 0; j < channelCount; j++)
                    {
                        // Writes data to data table
                        dataTable.Rows[i][j] = sourceArray.GetValue(j, i); 
                    }
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(e.ToString());
                runningAITask = null;
                runningDOTask = null;
                AITask.Dispose();
                DOTask.Dispose();
                stopwatch.Stop();
                stopButton.Enabled = false;
                startButton.Enabled = true;
                writeToFileGroupBox.Enabled = true;
                if (enableSwitchingCheckBox.Checked)
                {
                    statusCheckTimer.Stop();
                    statusCheckTimer.Dispose();
                }
            }
        }

        private void LogData(ref ArrayList whereToSave, double[,] data)
        {
            int channelCount = data.GetLength(0);
            int dataCount = data.GetLength(1);

            for (int i = 0; i < channelCount; i++)
            {
                ArrayList l = whereToSave[i] as ArrayList;

                for (int j = 0; j < dataCount; j++)
                {
                    l.Add(data[i, j]);
                }
            }            

        }

        private void LogTimespan(ref ArrayList whereToSave, TimeSpan data, int n)
        {
            if(n > 1)
            {
                double period = (1/Convert.ToDouble(rateNumeric.Value));
                for (int i = n; i > 0; i--)
                {
                    double dataS = data.TotalMilliseconds - i * period * 1000;
                    whereToSave.Add(dataS);
                }
            }
            else
            {
                double dataS = data.TotalMilliseconds;
                whereToSave.Add(dataS);
            }
        }

        private void CloseFile(string elapsedTime)
        {
            int channelCount = savedData.Count;
            int dataCount = (savedData[0] as ArrayList).Count;
            int totalDataCount = dataCount * channelCount;
            double elapsedTimeNumeric = double.Parse(elapsedTime);
            double estimatedRate = dataCount * 1000 / elapsedTimeNumeric;
            int sensorCount = 0;

            if (SerialEnableCheck.Checked)
            {
                sensorCount = (savedSensorData[0] as ArrayList).Count;
            }

            try
            {
                if (useTextFileWrite)
                {

                    for (int i = 0; i < dataCount; i++)
                    {
                        double s = (double)daqTimestamp[i];
                        fileStreamWriter.Write(s.ToString("e6"));
                        fileStreamWriter.Write("\t");

                        for (int j = 0; j < channelCount; j++)
                        {
                            // Writes data to file
                            ArrayList l = savedData[j] as ArrayList;
                            double dataValue = (double)l[i];
                            fileStreamWriter.Write(dataValue.ToString("e6"));
                            fileStreamWriter.Write("\t"); //seperate the data for each channel
                        }

                        if (SerialEnableCheck.Checked && i <= sensorCount)
                        {
                            s = (double)sensorTimestamp[i];
                            fileStreamWriter.Write(s.ToString("e6"));
                            fileStreamWriter.Write("\t");

                            for (int j = 0; j < savedSensorData.Count; j++)
                            {
                                ArrayList l = savedSensorData[j] as ArrayList;
                                double dataValue = (double)l[i];
                                fileStreamWriter.Write(dataValue.ToString("e6"));
                                fileStreamWriter.Write("\t"); //seperate the data for each channel
                            }
                        }               
                        fileStreamWriter.WriteLine(); //new line of data (start next scan)
                    }
                    fileStreamWriter.WriteLine("\nTotal of Samples: {0}", totalDataCount.ToString());
                    fileStreamWriter.WriteLine("Number of Samples per Channel: {0}", dataCount.ToString());
                    fileStreamWriter.WriteLine("Elapsed Time in ms: {0}", elapsedTime.Replace(",", "."));
                    fileStreamWriter.WriteLine("Sampling Frequency in Hz: {0}", rateNumeric.Value.ToString());
                    fileStreamWriter.Write("Estimated Sampling Frequency in Hz: {0}", estimatedRate.ToString());
                    fileStreamWriter.Close();
                }
                else
                {

                    for (int i = 0; i < dataCount; i++)
                    {
                        for (int j = 0; j < channelCount; j++)
                        {
                            // Writes data to file
                            ArrayList l = savedData[j] as ArrayList;
                            double dataValue = (double)l[i];
                            fileBinaryWriter.Write((double)dataValue);
                        }
                    }
                    fileBinaryWriter.Close();
                }
            }
            catch(Exception e)
            {
                MessageBox.Show(e.TargetSite.ToString());
                runningAITask = null;
                runningDOTask = null;
                AITask.Dispose();
                DOTask.Dispose();
                stopwatch.Stop();
                stopButton.Enabled = false;
                startButton.Enabled = true;
                if (enableSwitchingCheckBox.Checked)
                {
                    statusCheckTimer.Stop();
                    statusCheckTimer.Dispose();
                }
            }
        }

        public void InitializeDataTable(String[] channelNames, ref DataTable data, int smp)
        {
            int numChannels = channelNames.GetLength(0);
            data.Rows.Clear();
            data.Columns.Clear();
            dataColumn = new DataColumn[numChannels];
            int numOfRows;

            if (smp < tableMaximumRows)
                numOfRows = smp;
            else
                numOfRows = tableMaximumRows;

            for (int i = 0; i < numChannels; i++)
            {   
                dataColumn[i] = new DataColumn();
                dataColumn[i].DataType = typeof(double);
                dataColumn[i].ColumnName = channelNames[i];
            }

            data.Columns.AddRange(dataColumn); 

            for (int i = 0; i < numOfRows; i++)             
            {
                object[] rowArr = new object[numChannels];
                data.Rows.Add(rowArr);              
            }
        }

        //Creates a text/binary stream based on the user selections
        private bool CreateDataFile()
        {
            try
            {
                FileStream fs = new FileStream(fileNameWrite, FileMode.Create);
                if (useTextFileWrite)
                {
                    fileStreamWriter = new StreamWriter(fs);
                }
                else
                {
                    fileBinaryWriter = new BinaryWriter(fs);
                }
            }
            catch (System.IO.IOException ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }

            return true;
        }

        // Only used by text files to write the channel name
        // Can expand this for binary too
        private void PrepareFileForData()
        {
            //Prepare file for data (Write out the channel names
            int numChannels = AITask.AIChannels.Count;

            if (useTextFileWrite)
            {
                fileStreamWriter.Write("DAQTimestamp (ms)");
                fileStreamWriter.Write("\t");

                for (int i = 0; i < numChannels; i++)
                {   
                    fileStreamWriter.Write(AITask.AIChannels[i].PhysicalName);
                    fileStreamWriter.Write("\t"); 
                }

                if (SerialEnableCheck.Checked)
                {
                    fileStreamWriter.Write("AccTimestamp (ms)");
                    fileStreamWriter.Write("\t");
                    fileStreamWriter.Write("Acc X (g)");
                    fileStreamWriter.Write("\t");
                    fileStreamWriter.Write("Acc Y (g)");
                    fileStreamWriter.Write("\t");
                    fileStreamWriter.Write("Acc Z (g)");
                    fileStreamWriter.Write("\t");
                }
                fileStreamWriter.WriteLine();

            }
            else
            {
                for (int i = 0; i < numChannels; i++)
                {   
                    fileBinaryWriter.Write(AITask.AIChannels[i].PhysicalName);
                }
                fileBinaryWriter.Write("\r\n");
            }
        }

        private void textFileWriteRadioButton_CheckedChanged(object sender, System.EventArgs e)
        {
            if (textFileWriteRadioButton.Checked)
            {
                useTextFileWrite = true;
            }

        }

        private void binaryFileWriteRadioButton_CheckedChanged(object sender, System.EventArgs e)
        {
            if (binaryFileWriteRadioButton.Checked)
            {
                useTextFileWrite = false;
            }
        }

        private void minimumValueNumeric_ValueChanged(object sender, EventArgs e)
        {

        }

        private void ChnnNumObsLabel_Click(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void enableSwitchingCheckBox_CheckedChanged(object sender, EventArgs e)
        {
            if (!enableSwitchingCheckBox.Checked)
            {
                this.DOChannel2ComboBox.Enabled = false;
                this.periodNumeric.Enabled = false;
            }
            else
            {
                this.DOChannel2ComboBox.Enabled = true;
                this.periodNumeric.Enabled = true;
            }

        }

        private void filePathWriteTextBox_TextChanged(object sender, EventArgs e)
        {

        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void ChnnNumObsLabel_Click_1(object sender, EventArgs e)
        {

        }

        private void chart_Click(object sender, EventArgs e)
        {

        }

        private void DOChannel2ComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void comPortComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ToFileEnableCheck_CheckedChanged(object sender, EventArgs e)
        {
            if (ToFileEnableCheck.Checked)
            {
                textFileWriteRadioButton.Enabled = true;
                binaryFileWriteRadioButton.Enabled = true;
                startButton.Enabled = false;
                filePathWriteTextBox.Enabled = true;
                browseWriteButton.Enabled = true;

            }
            else
            {
                textFileWriteRadioButton.Enabled = false;
                binaryFileWriteRadioButton.Enabled = false;
                startButton.Enabled = true;
                filePathWriteTextBox.Enabled = false;
                browseWriteButton.Enabled = false;
            }


        }

        private void samplesPerChannelNumeric_ValueChanged(object sender, EventArgs e)
        {

        }
    }
}
