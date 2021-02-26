using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DHI.Generic.MikeZero;
using DHI.Mike1D.BoundaryModule;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.NetworkDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace DHI.Mike1D.Examples.Scripts
{

  /// <summary>
  /// Script file to support reading of SWMM5 Interface Files and use as
  /// Catchment Runoff data input to a MIKE 1D model.
  /// </summary>
  public class SWMM5InfFileBridgeSetup
  {
    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script(Order = 1)]
    public void SetupSWMM5Bridge(Mike1DData mike1DData, IDiagnostics diagnostics)
    {
      // Register SWMM5 Interface File bridge to MIKE 1D ResultData.
      // This will associate all ResultData files with the .txt extension with the SWMM5 Interface File reading
      ResultBridgeFactories.AddFactory("txt", new SWMM5IntFileBridgeFactory(true));

      // Update catchment connections, if specified
      if (mike1DData.AdditionalData.TryGetValue("CrfCatchmentConnections", false))
      {
        // Clear all existing catchment connections
        mike1DData.Network.CatchmentConnections.Clear();

        foreach (CatchmentResultFile crFile in mike1DData.BoundaryData.CatchmentResultFiles)
        {
          if (crFile.FilePath.ExtensionIs(".txt")) // Only for SWMM5 interface files
          {
            UpdateCatchmentConnections(mike1DData, crFile.FilePath, diagnostics);
          }
        }

        // For backwards compatibility (prior to release 2020)
        if (mike1DData.RainfallRunoffResultDataFilePath != null &&
            mike1DData.RainfallRunoffResultDataFilePath.ExtensionIs(".txt")) // Only for SWMM5 interface files
        {
          UpdateCatchmentConnections(mike1DData, mike1DData.RainfallRunoffResultDataFilePath, diagnostics);
        }
      }


      // Save to .res1d - set to true, if you want this to happen
      if (false)
      {
        foreach (CatchmentResultFile crFile in mike1DData.BoundaryData.CatchmentResultFiles)
        {
          if (crFile.FilePath.ExtensionIs(".txt")) // Only for SWMM5 interface files
          {
            ConvertToRes1D(crFile.FilePath.FullFilePath);
          }
        }

        // For backwards compatibility (prior to release 2020)
        if (mike1DData.RainfallRunoffResultDataFilePath != null &&
            mike1DData.RainfallRunoffResultDataFilePath.ExtensionIs(".txt")) // Only for SWMM5 interface files
        {
          ConvertToRes1D(mike1DData.RainfallRunoffResultDataFilePath.FullFilePath);
        }
      }


    }

    /// <summary>
    /// This method will add catchment connections to nodes with
    /// the same id as the catchment id, for catchments in the <paramref name="rrFile"/>
    /// </summary>
    private static void UpdateCatchmentConnections(Mike1DData mike1DData, IFilePath rrFile, IDiagnostics diagnostics)
    {
      // Make a map of all node id's - for fast searching on node id, used to only add connections to existing nodes
      var nodeIdDictionary = new HashSet<string>(mike1DData.Network.Nodes.Select(n => n.Id), StringComparer.OrdinalIgnoreCase);

      // Assume rrFile path is relative to setup file.
      rrFile.BaseFilePath = mike1DData.Connection.FilePath;

      // load rrFile - but only header, not data.
      IResultData resultData = new ResultData();
      resultData.Connection = Connection.Create(rrFile);
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.LoadHeader(resultDiagnostics);

      // Create new CatchmentConnections
      int countCreated = 0;
      int countSkipped = 0;
      foreach (IRes1DCatchment res1DCatchment in resultData.Catchments)
      {
        // Only add if node with this ID exists
        if (nodeIdDictionary.Contains(res1DCatchment.ID))
        {
          CatchmentConnection catchmentConnection = new CatchmentConnection
          {
            Fraction    = 1,
            CatchmentId = res1DCatchment.ID,
            NodeId      = res1DCatchment.ID
          };
          mike1DData.Network.CatchmentConnections.Add(catchmentConnection);
          countCreated++;
        }
        else
        {
          diagnostics.Warning(new DiagnosticItem(string.Format("Catchment-Node with ID {0} not found, catchment is ignored", res1DCatchment.ID)));
          countSkipped++;
        }
      }

      string msgs = string.Format("Updating Catchment Connections {0}, skipped {1} - for file: {2}", countCreated, countSkipped, rrFile.Path);
      Console.Out.WriteLine(msgs);
      diagnostics.Info(msgs);
    }

    /// <summary>
    /// Method to convert any supported result data file (CRF/PRF/SWMM5) to res1d.
    /// </summary>
    private static void ConvertToRes1D(string filename)
    {
      // load result file
      IResultData resultData = new ResultData();
      resultData.Connection = Connection.Create(filename);
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.Load(resultDiagnostics);

      // Save to res1d
      resultData.Connection.BridgeName         = "res1d";
      resultData.Connection.FilePath.Extension = "res1d";
      resultData.Save();
    }
  }

  /// <summary>
  /// Factory for creating SWMM5 Interface File Bridge on the fly
  /// </summary>
  public class SWMM5InterfaceFileBridgeFactory : IResultBridgeFactory
  {
    // Flag indicating whether to store as RR (catchment data) or HD (node data)
    private bool _asRR;

    /// <summary>
    /// Constructor, specifying whether to store as RR or HD
    /// </summary>
    public SWMM5InterfaceFileBridgeFactory(bool asRR)
    {
      _asRR = asRR;
    }

    /// <summary>
    /// Create and return the <see cref="SWMM5IntFileBridge"/>
    /// </summary>
    public IResultBridge Create(IResultData resultData)
    {
      return new SWMM5IntFileBridge(resultData, _asRR);
    }
  }


  /// <summary>
  /// Class for reading SWMM5 Interface File data and populate a ResultData object.
  /// <para>
  /// The SWMM5 Interface File contains outlet node flows.
  /// </para>
  /// <para>
  /// The <code>asRR</code> argument to the constructor flags if
  /// data is stored as catchment <see cref="PredefinedQuantity.TotalRunOff"/>
  /// or node <see cref="PredefinedQuantity.DischargeOutflow"/>.
  /// </para>
  /// <para>
  /// The "usual" integration in MIKE 1D is to convert the flow
  /// to runoff, and attach to MIKE 1D as catchment runoff file.
  /// There can be issues with the catchment connections and
  /// these must be regenerated.
  /// </para>
  /// <para>
  /// There is currently not that much error reporting.
  /// </para>
  /// </summary>
  public class SWMM5IntFileBridge : IResultReadBridge
  {
    /// <summary>
    /// Connection specifying the file to read from/write to
    /// </summary>
    public IConnection Connection { get; set; }

    /// <summary>
    /// Number of time steps
    /// </summary>
    public int NumberOfTimeSteps { get { return _resultData.TimesList.Count; } }

    /// <summary>
    /// Filter support is not implemented
    /// </summary>
    public IFilter Filter { get; set; }

    public event Action<int> TimeStepReadEvent;

    // Flag indicating whether to store as RR or HD
    private bool _asRR;

    // ResultData object to populate
    private ResultData _resultData;

    // StreamReader to read data from
    private StreamReader _reader;
    // Number of line most recently read
    private int _lineNumber;

    // Diagnostics object to report errors to.
    private IDiagnostics _diagnostics;

    /// <summary>
    /// Default constructor
    /// </summary>
    public SWMM5IntFileBridge(IResultData resultData, bool asRR)
    {
      _asRR = asRR;
      if (!(resultData is ResultData))
        throw new ArgumentException("SWMM5InfFileBridge requires a ResultData object");
      _resultData = resultData as ResultData;
    }

    /// <summary>
    /// Connect to storage
    /// </summary>
    public void Connect(IDiagnostics diagnostics)
    {
      _diagnostics = diagnostics;
      OpenFile();
    }

    /// <summary>
    /// Disconnect from storage
    /// </summary>
    public void Finish()
    {
      if (_reader != null)
        _reader.Close();
      _reader = null;
    }

    /// <summary>
    /// Read header and data
    /// </summary>
    public void Read(IDiagnostics diagnostics)
    {
      _diagnostics = diagnostics;
      bool closeFile = OpenFile();
      ReadHeader(diagnostics);
      ReadData(diagnostics);
      if (closeFile)
        Finish();
    }

    /// <summary>
    /// Read file header
    /// </summary>
    public void ReadHeader(IDiagnostics diagnostics)
    {
      _diagnostics = diagnostics;
      OpenFile();

      string line;

      // SWMM5 Interface File
      line = ReadLine();
      if (!line.Equals("SWMM5 Interface File", StringComparison.OrdinalIgnoreCase))
        _diagnostics.Error(new DiagnosticItem(string.Format("Could no read SWMM5 Interface File: {0}: This files seems not to be a SWMM5 Interface File (missing initial header line)", Connection.FilePath.Path)));

      // This is from the 1st line of the SWMM5 Model in the Title/Notes Section of the Data
      line = ReadLine();

      // reporting time step in sec
      line = ReadLine();
      double timeStepSec = ParseDouble(line.Split('-')[0].Trim());

      // Quantities/constituents header line
      // number of constituents as listed below:
      line = ReadLine();
      int numConstituents = ParseInt(line.Split('-')[0].Trim());

      // The first one will always be flow, the following ones will be pollutants.
      List<Quantity> quantities = new List<Quantity>(numConstituents);
      for (int i = 0; i < numConstituents; i++)
      {
        line = ReadLine();

        // In case there is a space in the pollutant name, we cannot use the line.split(' ') directly
        string[] parts = new string[2];
        int splitIndex = line.LastIndexOf(' ');
        parts[0] = line.Substring(0, splitIndex).Trim();
        parts[1] = line.Substring(splitIndex).Trim();

        Quantity quantity;
        if (parts[0].Equals("FLOW", StringComparison.OrdinalIgnoreCase))
        {
          if (_asRR)
            // This is actually the outlet node flow - but we are using it as runoff here
            quantity = new Quantity("TotalRunOff", "Total Runoff", eumItem.eumIDischarge);
          else
            quantity = Quantity.Create(PredefinedQuantity.DischargeOutflow);
        }
        else
        {
          quantity = new Quantity(parts[0], parts[0], eumItem.eumIConcentration);
        }

        if (parts[1] == "MGD") quantity.EumQuantity.Unit = eumUnit.eumUMgalPerDay;
        if (parts[1] == "CFS") quantity.EumQuantity.Unit = eumUnit.eumUft3PerSec;
        if (parts[1] == "GPM") quantity.EumQuantity.Unit = eumUnit.eumUGalPerMin;
        if (parts[1] == "MGD") quantity.EumQuantity.Unit = eumUnit.eumUMgalPerDay;
        if (parts[1] == "CMS") quantity.EumQuantity.Unit = eumUnit.eumUm3PerSec;
        if (parts[1] == "LPS") quantity.EumQuantity.Unit = eumUnit.eumUliterPerSec;
        if (parts[1] == "LPD") quantity.EumQuantity.Unit = eumUnit.eumUliterPerDay;

        if (parts[1] == "MG/L") quantity.EumQuantity.Unit = eumUnit.eumUmilliGramPerL;
        if (parts[1] == "UG/L") quantity.EumQuantity.Unit = eumUnit.eumUmicroGramPerL;
        if (parts[1] == "#/L") {quantity.EumQuantity.Unit = eumUnit.eumUperLiter; quantity.EumQuantity.Item = eumItem.eumIBacteriaConc; } // could also be eumItem.eumICountsPerLiter

        quantities.Add(quantity);
      }

      // Catchments header line
      // number of nodes as listed below:
      line = ReadLine();
      int numNodes = ParseInt(line.Split('-')[0].Trim());

      for (int i = 0; i < numNodes; i++)
      {
        line = ReadLine();
        string nodeId = line.Trim();

        IRes1DDataSet dataSet;
        ItemTypeGroup itemTypeGroup;
        if (_asRR)
        {
          Res1DCatchment catchment = new Res1DCatchment
          {
            Id = nodeId,
            Shape = ElementSetDefinition.CreateIdElementSet(nodeId),
          };
          _resultData.Catchments.Add(catchment);
          dataSet = catchment;
          itemTypeGroup = ItemTypeGroup.CatchmentItem;
        }
        else
        {
          Res1DNode node = new Res1DOutlet()
          {
            Id = nodeId,
          };
          _resultData.Nodes.Add(node);
          dataSet = node;
          itemTypeGroup = ItemTypeGroup.NodeItem;
        }

        for (int j = 0; j < quantities.Count; j++)
        {
          DataItem dataItem = new DataItem(false)
          {
            ItemTypeGroup = itemTypeGroup,
            NumberWithinGroup = i,
            Quantity = quantities[j]
          };
          dataSet.DataItems.Add(dataItem);
        }
      }
    }

    /// <summary>
    /// Read file data
    /// </summary>
    public void ReadData(IDiagnostics diagnostics)
    {
      _diagnostics = diagnostics;

      string line;
      // Data header line
      // Node             Year Mon Day Hr  Min Sec FLOW      
      line = ReadLine();

      char[] sepSpace = { ' ' };
      while (!_reader.EndOfStream)
      {
        // Read next time step
        // TODO: Question: For a time step:
        // - Will they ever NOT be in order?
        // - Can the time differ?

        for (int i = 0; i < _resultData.Catchments.Count; i++)
        {
          if (_reader.EndOfStream)
          {
            throw new EndOfStreamException(string.Format("Could not read SWMM5 Interface File: {0}: Got EOF prematurely at line {1}", Connection.FilePath.Path, _lineNumber));
          }

          IRes1DDataSet dataSet;
          if (_asRR)
            dataSet = _resultData.Catchments[i];
          else
            dataSet = _resultData.Nodes[i];

          line = ReadLine();
          string[] parts = line.Split(sepSpace, StringSplitOptions.RemoveEmptyEntries);
          if (parts.Length < 7 + dataSet.DataItems.Count)
          {
            throw new InvalidDataException(string.Format("Could not read SWMM5 Interface File: {0}: Lined ended prematurely: {1}", Connection.FilePath.Path, _lineNumber));
          }

          if (i == 0)
          {
            DateTime dateTime = ParseDateTime(parts, 1);
            _resultData.TimesList.Add(dateTime);
          }

          if (parts[0] != dataSet.Id)
          {
            throw new InvalidDataException(string.Format("Could no read SWMM5 Interface File: {0}: Mismatching catchment names at line {1}", Connection.FilePath.Path, _lineNumber));
          }

          for (int j = 0; j < dataSet.DataItems.Count; j++)
          {
            dataSet.DataItems[j].TimeData.Add((float)ParseDouble(parts[7 + j]));
          }
        }
      }
      _resultData.StartTime = _resultData.TimesList[0];
      _resultData.EndTime = _resultData.TimesList[_resultData.TimesList.Count - 1];

      //Console.Out.WriteLine("Read {0} timesteps", _resultData.TimesList.Count);
    }

    private bool OpenFile()
    {
      if (_reader != null)
        return false;

      if (!Connection.FilePath.HasPath)
        throw new Exception("FilePath not defined");

      if (!File.Exists(Connection.FilePath.FullFilePath))
        throw new FileNotFoundException("SWMM5 information file could not be found \"" + Connection.FilePath.FullFilePath + "\"", Connection.FilePath.FullFilePath);

      _reader = new StreamReader(Connection.FilePath.FullFilePath);
      _lineNumber = 0;
      return true;
    }

    private string ReadLine()
    {
      if (_reader.EndOfStream)
        throw new EndOfStreamException(string.Format("Could not read SWMM5 Interface File: {0}: Got EOF prematurely at line {1}", Connection.FilePath.Path, _lineNumber));
      _lineNumber++;
      return _reader.ReadLine();
    }

    private double ParseDouble(string str)
    {
      double val;
      if (double.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
        return val;

      throw new InvalidDataException(string.Format("Could no read SWMM5 Interface File: {0}: Error parsing double value at line {1}", Connection.FilePath.Path, _lineNumber));
    }

    private int ParseInt(string str)
    {
      int val;
      if (int.TryParse(str, NumberStyles.Any, CultureInfo.InvariantCulture, out val))
        return val;

      throw new InvalidDataException(string.Format("Could no read SWMM5 Interface File: {0}: Error parsing integer value at line {1}", Connection.FilePath.Path, _lineNumber));
    }

    private DateTime ParseDateTime(string[] str, int startIndex)
    {
      int year  = ParseInt(str[startIndex]);
      int month = ParseInt(str[startIndex+1]);
      int day   = ParseInt(str[startIndex+2]);
      int hour  = ParseInt(str[startIndex+3]);
      int min   = ParseInt(str[startIndex+4]);
      int sec   = ParseInt(str[startIndex+5]);

      return new DateTime(year, month, day, hour, min, sec);
    }

  }

}
