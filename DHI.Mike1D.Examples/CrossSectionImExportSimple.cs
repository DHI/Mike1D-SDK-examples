using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using DHI.Mike1D.CrossSectionModule;
using DHI.Mike1D.Generic;

namespace DHI.Mike1D.Examples
{

  /// <summary>
  /// Exports from xns11 to text, and imports the same file again. Only raw data is im/exported.
  /// <para>
  /// File format is as follows: Each cross section has a info-line, 
  /// followed by [number-of-raw-points] lines with raw (x,z) coordinates of 
  /// open cross sections.
  /// </para>
  /// <para>
  /// Cross section info-line:
  /// [mode],"[branch-name]",[chainage],"[topo-id]",[datum],[number-of-raw-points]
  /// </para>
  /// <para>
  /// The [mode] is exported with a value of 0. It is used when importing again,
  /// if the value is zero, nothing is done. If the value is 1, the 
  /// cross section at the location is updated, or if no cross section
  /// is already at that location, a new one is created.
  /// </para>
  /// </summary>
  /// <example>
  /// Below content in the text file will add a new cross section at "CALI", 100, topo-id "1989",
  /// containing 13 raw points.
  /// <code>
  /// 1,"CALI",100,"1989",0,13
  /// 0,61.4500007629395
  /// 100,60.4900016784668
  /// 200,55.6199989318848
  /// 300,55.189998626709
  /// 400,55.8699989318848
  /// 500,55.939998626709
  /// 600,55.5800018310547
  /// 700,55.5200004577637
  /// 800,55.7700004577637
  /// 900,58.7900009155273
  /// 1000,60.8199996948242
  /// 1100,60.3499984741211
  /// 1160,60.6699981689453
  /// </code>
  /// </example>
  class CrossSectionImExportSimple
  {

    /// <summary>
    /// Static constructor, setting up search paths for MIKE assemblies
    /// </summary>
    static CrossSectionImExportSimple()
    {
      // The setup method will make your application find the MIKE assemblies at runtime.
      // The first call of the setup method takes precedense. Any subsequent calls will be ignored.
      // It must be called BEFORE any method using MIKE libraries is called, i.e. it is not sufficient
      // to call it as the first thing in that method using the MIKE libraries. Often this can be achieved
      // by having this code in the static constructor.
      // This is not required by plugins and scripts, only by standalone applications using MIKE 1D components
      if (!DHI.Mike.Install.MikeImport.Setup(19, DHI.Mike.Install.MikeProducts.Mike1D))
        throw new Exception("Could not find a MIKE installation");
    }


    /// <summary>
    /// Main method used for parsing command line arguments, if build as an executable.
    /// </summary>
    static void Main(string[] args)
    {
      if (args.Length < 3)
      {
        Usage();
        return;
      }

      if (StringComparer.OrdinalIgnoreCase.Equals(args[0], "export"))
      {
        if (!args[1].EndsWith(".xns11", StringComparison.OrdinalIgnoreCase))
        {
          Console.Out.WriteLine("File to export is not an xns11 file " + args[2]);
          Console.Out.WriteLine("");
          Usage();
          return;
        }
        Export(args[1], args[2]);
      }

      else if (StringComparer.OrdinalIgnoreCase.Equals(args[0], "import"))
      {
        if (!args[2].EndsWith(".xns11", StringComparison.OrdinalIgnoreCase))
        {
          Console.Out.WriteLine("File to load from is not an xns11 file: "+args[2]);
          Console.Out.WriteLine("");
          Usage();
          return;
        }
        if (args.Length > 3 && !args[3].EndsWith(".xns11", StringComparison.OrdinalIgnoreCase))
        {
          Console.Out.WriteLine("File to save to is not an xns11 file: " + args[3]);
          Console.Out.WriteLine("");
          Usage();
          return;
        }

        if (args.Length == 3)
          Import(args[1], args[2]);           // overwrite, if existing
        else
          Import(args[1], args[2], args[3]);  // create new file

      }

      else
      {
        Usage();
      }
    }

    /// <summary>
    /// Print out message on how to use this tool
    /// </summary>
    static void Usage()
    {
      Console.Out.WriteLine("Export cross section (xns11) file to text, or import text file to cross section (xns11) file.");
      Console.Out.WriteLine("");
      Console.Out.WriteLine("When importing, if file to import to already exists, cross sections in that file are updated");
      Console.Out.WriteLine("");
      Console.Out.WriteLine("Usage:");
      Console.Out.WriteLine("    xns11ImExport export [xns11-file] [output txt file]");
      Console.Out.WriteLine("    xns11ImExport import [input txt file] [xns11-file to load] [xns11-file to save to]");
      Console.Out.WriteLine("");
      Console.Out.WriteLine("if [xns11-file to load] and [xns11-file to save to] are the same,");
      Console.Out.WriteLine("or [xns11-file to save to] is omitted, the  [xns11-file to load] is overwritten");
      Console.Out.WriteLine("");
      Console.Out.WriteLine("Examples:");
      Console.Out.WriteLine("    xns11ImExport export cali.xns11 my_text_file.txt");
      Console.Out.WriteLine("    xns11ImExport import my_text_file.txt cali.xns11 cali-modified.xns11");
      Console.Out.WriteLine("    xns11ImExport import my_text_file.txt cali.xns11");
    }

    /// <summary>
    /// Export from xns11 to text file
    /// </summary>
    public static void Export(string xns11FileName, string txtFileName)
    {
      // Open cross section file
      CrossSectionDataFactory crossSectionDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = crossSectionDataFactory.Open(Connection.Create(xns11FileName), null);

      // Open text file to write to
      StreamWriter writer = new StreamWriter(txtFileName);
      
      // Loop over all cross sections
      foreach (ICrossSection crossSection in csData)
      {
        // Export only those with raw data.
        // Any other filtering can be added here.
        if (!(crossSection.BaseCrossSection is XSBaseRaw))
          continue;

        XSBaseRaw xsBase = crossSection.BaseCrossSection as XSBaseRaw;
        
        // Write cross section info-line.
        // First value in inf- line is a "what-to-do when importing" flag. 
        // 0: Do nothing
        // 1: Update existing/create new if not existing
        writer.WriteLine("{0},{1},{2},{3},{4},{5}",
          "1",
          crossSection.Location.ID.EnQuote(),
          crossSection.Location.Chainage.ToString("R", CultureInfo.InvariantCulture),
          crossSection.TopoID.EnQuote(),
          crossSection.Location.Z.ToString("R", CultureInfo.InvariantCulture),
          xsBase.Points.Count);

        // Write coordinates of all raw points
        for (int i = 0; i < xsBase.Points.Count; i++)
        {
          ICrossSectionPoint p = xsBase.Points[i];
          writer.WriteLine("{0},{1}", 
            p.X.ToString("R", CultureInfo.InvariantCulture), 
            p.Z.ToString("R", CultureInfo.InvariantCulture));
        }
      }
      writer.Close();
    }

    /// <summary>
    /// Import from text file to xns11 file
    /// </summary>
    /// <param name="txtFileName">Path and name of text file to import</param>
    /// <param name="xns11FileName">Path and name of xns11 file to create or update</param>
    /// <param name="xns11NewFileName">Path and name of xns11 file to write to</param>
    public static void Import(string txtFileName, string xns11FileName, string xns11NewFileName = null)
    {
      StreamReader reader = new StreamReader(txtFileName);

      CrossSectionDataFactory crossSectionDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData;
      if (File.Exists(xns11FileName))
        csData = crossSectionDataFactory.Open(Connection.Create(xns11FileName), null);
      else
        csData = new CrossSectionData();
      if (string.IsNullOrEmpty(xns11NewFileName))
        xns11NewFileName = xns11FileName;

      string line;

      // Read cross section info-line
      while ((line = reader.ReadLine()) != null)
      {
        // Split cross section info-line
        string[] split = line.SplitQuoted(',', '"');

        // extract info from info-line
        Location location = new Location(split[1], double.Parse(split[2],CultureInfo.InvariantCulture));
        string topoId = split[3];
        double datum = double.Parse(split[4], CultureInfo.InvariantCulture);
        int numRawPoints = int.Parse(split[5]);
        
        // Check if this cross section is to be processed
        if (split[0] == "1")
        {
          ICrossSection cs = csData.FindCrossSection(location, topoId);

          CrossSectionPointList points = new CrossSectionPointList();

          // Read raw points
          for (int i = 0; i < numRawPoints; i++)
          {
            line = reader.ReadLine();
            if (line == null) // end-of-file
              throw new EndOfStreamException("File ended prematurely");

            string[] coords = line.Split(',');

            double x = double.Parse(coords[0], CultureInfo.InvariantCulture);
            double z = double.Parse(coords[1], CultureInfo.InvariantCulture);
            points.Add(new CrossSectionPoint(x, z));
          }

          // Check if cross section already exists
          if (cs != null)
          {
            // Check if it is a cross section with raw points
            XSBaseRaw xsBaseRaw = cs.BaseCrossSection as XSBaseRaw;
            if (xsBaseRaw == null)
              throw new Exception("Cannot modify raw points of a cross section without raw points: "+location.ToString()+", "+topoId);

            // replace datum (in case datum changes)
            cs.Location.Z = datum;
            // Replace points
            xsBaseRaw.Points = points;
            // Set default markers
            xsBaseRaw.UpdateMarkersToDefaults(true, true, true);
            // Recalculate processed data
            xsBaseRaw.ProcessingLevelsSpecs.NoOfLevels = 50;
            xsBaseRaw.CalculateProcessedData();
          }
          else
          {
            // Create a new cross section
            CrossSectionFactory builder = new CrossSectionFactory();
            builder.BuildOpen("");

            // Defines the location of the current cross section. The Z-coordinate
            // for an open cross section with raw data is a Z-offset, and usually zero. 
            builder.SetLocation(new ZLocation(location.ID, location.Chainage) { Z = datum });

            // Set raw points and default markers
            builder.SetRawPoints(points);
            builder.SetDefaultMarkers();

            // Define resistance properties as relative
            FlowResistance flowResistance = new FlowResistance();
            flowResistance.Formulation = ResistanceFormulation.Relative;
            flowResistance.ResistanceDistribution = ResistanceDistribution.Uniform;
            flowResistance.ResistanceValue = 1;
            builder.SetFlowResistance(flowResistance);
            builder.SetRadiusType(RadiusType.ResistanceRadius);

            // Get the cross section
            CrossSectionLocated csLocated = builder.GetCrossSection();
            // Set topo-id 
            csLocated.TopoID = topoId;

            // now, calculate processed data
            csLocated.BaseCrossSection.ProcessingLevelsSpecs.Option = ProcessingOption.AutomaticLevels;
            csLocated.BaseCrossSection.ProcessingLevelsSpecs.NoOfLevels = 0;
            csLocated.BaseCrossSection.CalculateProcessedData();

            // Store cross section in database
            csData.Add(csLocated);

          }
        }
        else // this cross section should not be processed
        {
          // Skip line containing raw points
          for (int i = 0; i < numRawPoints; i++)
          {
            line = reader.ReadLine();
            if (line == null) // end-of-file
              throw new EndOfStreamException("File ended prematurely");
          }
        }
      }
      csData.Connection = Connection.Create(xns11NewFileName);
      CrossSectionDataFactory.Save(csData);
    }

  }


  internal static class StringExtensions
  {

    /// <summary>
    /// Method to en-quote a string, i.e. handling if a string has a " character,
    /// replacing it with two "".
    /// </summary>
    public static string EnQuote(this string str)
    {
      // Replace single quote with double quote
      return "\""+str.Replace("\"", "\"\"")+ "\"";
    }

    /// <summary>
    /// Method to split a string that may contain quote characters for substrings.
    /// </summary>
    /// <param name="input">Input string</param>
    /// <param name="separator">Separator character</param>
    /// <param name="quotechar">String quote character</param>
    /// <param name="options">Flag to omit empty substrings</param>
    /// <returns>Array of substrings</returns>
    public static string[] SplitQuoted(this string input, char separator, char quotechar, StringSplitOptions options = StringSplitOptions.None)
    {
      List<string> subStrings = new List<string>();
      StringBuilder sb = new StringBuilder();
      bool quotedString = false;              // substring is a quoted string
      bool insideQuote = false;               // inside quote
      char prevC = separator;
      foreach (char c in input)
      {
        if (!quotedString)
        {
          if (c == quotechar && prevC == separator)
          {
            // The only way to start a quoted string is to have a separator followed by a quotechar
            quotedString = true;
            insideQuote = true;
          }
          else if (c == separator)
          {
            // we have a split-separator, prepare for new sub-string
            if (options != StringSplitOptions.RemoveEmptyEntries || sb.Length > 0)
              subStrings.Add(sb.ToString());
            sb.Clear();
          }
          else
          {
            // Append character
            sb.Append(c);
          }
        }

        else // Quoted string value
        {
          if (!insideQuote && c == separator)
          {
            // Quote has stopped and we have a split-separator, prepare for new sub-string
            if (options != StringSplitOptions.RemoveEmptyEntries || sb.Length > 0)
              subStrings.Add(sb.ToString());
            sb.Clear();
            quotedString = false;
          }

          else if (c == quotechar) // Found another quotechar
          {
            // Flip insideQuote flag
            insideQuote = !insideQuote;
            // If insodeQuote is true, we have met a double quote, so add one
            if (insideQuote)
              sb.Append(c);
          }

          else
          {
            if (insideQuote)
              sb.Append(c);
            else
            {
              // Found a case like ',"some Text"outside,'
              throw new Exception("Found character in quoted position outside qoutes");
            }
          }
        }
        prevC = c;
      }
      // Add final string to substrings
      if (options != StringSplitOptions.RemoveEmptyEntries || sb.Length > 0)
        subStrings.Add(sb.ToString());
      return subStrings.ToArray();
    }
  }

}
