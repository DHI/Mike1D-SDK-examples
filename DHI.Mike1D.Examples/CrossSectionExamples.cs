using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DHI.Mike1D.CrossSectionModule;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;

namespace DHI.Mike1D.Examples
{
  /// <summary>
  /// Examples of handling cross sections
  /// </summary>
  public class CrossSectionExamples
  {

    /// <summary>
    /// Static constructor, setting up search paths for MIKE assemblies
    /// </summary>
    static CrossSectionExamples()
    {
      // The setup method will make your application find the MIKE assemblies at runtime.
      // The first call of the setup method takes precedense. Any subsequent calls will be ignored.
      // It must be called BEFORE any method using MIKE libraries is called, i.e. it is not sufficient
      // to call it as the first thing in that method using the MIKE libraries. Often this can be achieved
      // by having this code in the static constructor.
      // This is not required by plugins and scripts, only by standalone applications using MIKE 1D components
      //if (!DHI.Mike.Install.MikeImport.Setup(17, DHI.Mike.Install.MikeProducts.Mike1D))
      //  throw new Exception("Could not find a MIKE installation");
    }
    
    #region Navigation

    /// <summary>
    /// Example of how to navigate and find cross sections.
    /// <para>
    /// There are two ways to get cross sections: Find or Get
    /// </para>
    /// <para>
    /// Find will search the cross section data object for a cross
    /// section at the specified location. If no cross section is 
    /// found, null is returned.
    /// </para>
    /// <para>
    /// Get will search the cross section data object for a cross
    /// section at the specified location. If no cross section is 
    /// found, it will interpolate/extrapolate if possible, and only 
    /// if that interpolation/extrapolation is not possible, null
    /// is returned. The get methods are used by the engine.
    /// </para>
    /// <para>
    /// Users that want to update or in other ways process the cross
    /// sections should always use the Find methods.
    /// </para>
    /// </summary>
    /// <param name="xns11Filepath">Filepath to xns11 file</param>
    public static void Navigation(string xns11Filepath)
    {
      // Load cross section data
      Diagnostics diagnostics = new Diagnostics("Errors");
      CrossSectionDataFactory csDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = csDataFactory.Open(Connection.Create(xns11Filepath), diagnostics);

      ICrossSection crossSection;

      // Find a cross section on a given location
      crossSection = csData.FindCrossSection(new Location("LINDSKOV", 1370), "TOPO-95");
      if (crossSection == null) throw new Exception("Could not find cross section at location");

      // Find a cross section closest to a given location
      crossSection = csData.FindClosestCrossSection(new Location("LINDSKOV", 1250), "TOPO-95");
      if (crossSection == null || crossSection.Location.Chainage != 1172) throw new Exception("Expected cross section at chainage 1172");

      // Find all cross sections inside a given location span
      IList<ICrossSection> css = csData.FindCrossSectionsForLocationSpan(new LocationSpan("LINDSKOV", 1000, 2000), "TOPO-95", true);
      if (css.Count != 6) throw new Exception("Expected 6 cross sections");

      // Find cross sections that are neighbours to a given locatin.
      ICrossSection csAfter;
      ICrossSection csBefore;
      csData.FindNeighborCrossSections(new Location("LINDSKOV", 1250), "TOPO-95", true, out csBefore, out csAfter);
      if (csBefore == null || csBefore.Location.Chainage != 1172) throw new Exception("Expected cross section at chainage 1172");
      if (csAfter == null || csAfter.Location.Chainage != 1370) throw new Exception("Expected cross section at chainage 1370");

    }

    /// <summary>
    /// Example of how to loop over all cross sections
    /// </summary>
    /// <param name="csData">Cross section data object</param>
    public static void LoopAllCrossSections(CrossSectionData csData)
    {
      // Loop over all cross sections
      foreach (ICrossSection cs in csData)
      {
        // Check if cross section has raw data:
        XSBaseRaw xsBaseRaw = cs.BaseCrossSection as XSBaseRaw;
        if (xsBaseRaw == null)
          continue; // It dit not have raw data
        
        // Add additional 0.4 to all z values in the raw points
        foreach (ICrossSectionPoint xsPoint in xsBaseRaw.Points)
        {
          xsPoint.Z += 0.4;
        }

        // Update all markers to defaul
        xsBaseRaw.UpdateMarkersToDefaults(true, true, true);

        // Calculates the processed levels, storage areas, radii, etc, ie, fill in all 
        // ProcessedXXX properties.
        xsBaseRaw.CalculateProcessedData();
      }

    }

    #endregion

    #region AddCrossSection


    /// <summary>
    /// Example of how to load an xns11 file, add a cross section and save the 
    /// file again, adding a "-csAdd" to the end of the new filename.
    /// </summary>
    /// <param name="xns11Filepath">Filepath to xns11 file</param>
    public static void AddCrossSectionAndSave(string xns11Filepath)
    {
      // Load cross section data
      Diagnostics diagnostics = new Diagnostics("Errors");
      CrossSectionDataFactory csDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = csDataFactory.Open(Connection.Create(xns11Filepath), diagnostics);

      // Add cross section
      AddCrossSection(csData);

      // Save the cross section as a new file name
      csData.Connection.FilePath.FileNameWithoutExtension += "-csAdd";
      CrossSectionDataFactory.Save(csData);
    }


    /// <summary>
    /// Example of how to add a cross section after loading a complete 
    /// setup, and running the simulation with the new cross section
    /// included. It is not necessary to save the modified cross sections
    /// to file before running.
    /// </summary>
    /// <param name="setupFilepath">A .sim11 setup path</param>
    public static void AddCrossSectionAndRun(string setupFilepath)
    {
      Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
      IMike1DController controller = null;

      try
      {
        // creates a new Mike 1D controller and load the setup
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");
        controller = controllerFactory.OpenAndCreate(Connection.Create(setupFilepath), diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Loading errors, aborting");

        // Add an additional cross section
        AddCrossSection(controller.Mike1DData.CrossSections);

        // Change the output file name, so we can compare results with/without change
        controller.Mike1DData.ResultSpecifications[0].Connection.FilePath.FileNameWithoutExtension += "-csAdd";

        IDiagnostics validated = controller.Validate();
        if (validated.ErrorCountRecursive > 0)
          throw new Exception("Validation errors, aborting");

        // run the simulation with the new cross section included
        controller.Initialize(diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Initialization errors, aborting");

        controller.Prepare();
        controller.Run();
        controller.Finish();

      }
      catch (Exception e)
      {
        // Write exception to log file
        if (controllerFactory.LogFileWriter != null)
          controllerFactory.LogFileWriter.ExceptionToLogFile(e);
        // Call Finish, which should make sure to close down properly, and release any licences.
        if (controller != null)
        {
          try { controller.Finish(); }
          catch { }
        }
        // Rethrow exception
        throw;
      }
    }


    /// <summary>
    /// Adds a new cross section to the CrossSectionData object
    /// </summary>
    /// <param name="csData">Cross section data object</param>
    public static void AddCrossSection(CrossSectionData csData)
    {
      // Note: Raw data must be ordered from left to right in order for hydraulic radius to be processed correctly

      // Creates a class representing a cross section with raw data attached.
      CrossSectionFactory builder = new CrossSectionFactory();
      builder.BuildOpen("");

      // Defines the location of the current cross section. The Z-coordinate
      // is the bottom level of the cross section (unless defined by the
      // raw data (the open cross sections)).
      builder.SetLocation(new ZLocation("river B", 500) { Z = 0 });

      // Create a number of points
      CrossSectionPointList points = new CrossSectionPointList();
      points.Add(new CrossSectionPoint(-1.0, 2.0));
      points.Add(new CrossSectionPoint(0.0, 1.0));
      points.Add(new CrossSectionPoint(0.0, 0.0));
      points.Add(new CrossSectionPoint(1.0, 0.0));
      points.Add(new CrossSectionPoint(1.0, 1.0));
      points.Add(new CrossSectionPoint(2.0, 2.0));
      points.Add(new CrossSectionPoint(3.0, 2.0)); // dummy point, outside right levee bank marker
      // Sets the markers at left/right side and lowest point.
      builder.SetRawPoints(points);
      builder.SetLeftLeveeBank(points[0]);
      builder.SetLowestPoint(points[3]);
      builder.SetRightLeveeBank(points[5]);

      // Set flow resistance
      FlowResistance flowResistance = new FlowResistance();
      flowResistance.ResistanceDistribution = ResistanceDistribution.Uniform;
      flowResistance.ResistanceValue = 1;
      flowResistance.Formulation = ResistanceFormulation.Relative;
      builder.SetFlowResistance(flowResistance);
      builder.SetRadiusType(RadiusType.ResistanceRadius);

      // Get cross section from builder
      CrossSectionLocated cs = builder.GetCrossSection();
      cs.TopoID = "1";

      // Calculates the processed levels, storage areas, radii, etc, ie, fill in all 
      // ProcessedXXX properties.
      cs.BaseCrossSection.CalculateProcessedData();

      // Validates the data. The constraints are that the levels and the areas after sorting
      // must be monotonically increasing.
      IDiagnostics diagnostics = cs.Validate();
      if (diagnostics.ErrorCountRecursive > 0)
      {
        throw new Exception(String.Format("Number of errors: {0}", diagnostics.Errors.Count));
      }

      // Add the cross section
      csData.Add(cs);

    }

    #endregion

    #region ModifyCrossSectionRaw

    /// <summary>
    /// Load an xns11 file, add a cross section and save the file again, 
    /// adding a "-csModRaw" to the end of the filename.
    /// </summary>
    /// <param name="xns11Filepath">Filepath to xns11 file from the CrossSection example data</param>
    public static void ModifyCrossSectionRawAndSave(string xns11Filepath)
    {
      // Load cross section data
      Diagnostics diagnostics = new Diagnostics("Errors");
      CrossSectionDataFactory csDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = csDataFactory.Open(Connection.Create(xns11Filepath), diagnostics);

      // Modify cross section
      ModifyCrossSectionRaw(csData);

      // Save the cross section as a new file name
      csData.Connection.FilePath.FileNameWithoutExtension += "-csModRaw";
      CrossSectionDataFactory.Save(csData);

    }

    /// <summary>
    /// Modify cross section
    /// </summary>
    /// <param name="csData">Cross section data from the CrossSection example data</param>
    public static void ModifyCrossSectionRaw(CrossSectionData csData)
    {
      ICrossSection crossSection = csData.FindCrossSection(new Location("river B", 1000), "1");
      if (crossSection == null)
        throw new Exception("Cross section not found at location");

      // This is a cross section with raw data
      XSBaseRaw xsBaseRaw = crossSection.BaseCrossSection as XSBaseRaw;
      if (xsBaseRaw == null)
        throw new Exception("Not a cross section with raw data");

      // This cross section has 3 points.
      // Update the first two points
      xsBaseRaw.Points[1].X = 0.5;
      xsBaseRaw.Points[1].Z = 0.5;
      xsBaseRaw.Points[2] = new CrossSectionPoint(0.5, 0.0);
      // Add two additional points
      xsBaseRaw.Points.Add(new CrossSectionPoint(1.0, 0.2));
      xsBaseRaw.Points.Add(new CrossSectionPoint(1.0, 1.0));

      // Update marker to default
      xsBaseRaw.UpdateMarkersToDefaults(true, true, true);

      // Calculates the processed levels, storage areas, radii, etc, ie, fill in all 
      // ProcessedXXX properties.
      xsBaseRaw.CalculateProcessedData();

      // Validates the data. The constraints are that the levels and the areas after sorting
      // must be monotonically increasing.
      IDiagnostics diagnostics = xsBaseRaw.Validate();

      if (diagnostics.ErrorCountRecursive > 0)
      {
        throw new Exception(String.Format("Number of errors: {0}", diagnostics.Errors.Count));
      }

    }

    #endregion

    #region ModifyCrossSectionProcessed

    /// <summary>
    /// Load an xns11 file, add a cross section and save the file again, 
    /// adding a "-csModPro" to the end of the filename.
    /// </summary>
    /// <param name="xns11Filepath">Filepath to xns11 file from the CrossSection example data</param>
    public static void ModifyCrossSectionProcessedAndSave(string xns11Filepath)
    {
      // Load cross section data
      Diagnostics diagnostics = new Diagnostics("Errors");
      CrossSectionDataFactory csDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = csDataFactory.Open(Connection.Create(xns11Filepath), diagnostics);

      // Modify cross section
      ModifyCrossSectionProcessed(csData);

      // Save the cross section as a new file name
      csData.Connection.FilePath.FileNameWithoutExtension += "-csModPro";
      CrossSectionDataFactory.Save(csData);

    }

    /// <summary>
    /// Modify cross section
    /// </summary>
    /// <param name="csData">Cross section data from the CrossSection example data</param>
    public static void ModifyCrossSectionProcessed(CrossSectionData csData)
    {
      ICrossSection crossSection = csData.FindCrossSection(new Location("river B", 1000), "1");
      if (crossSection == null)
        throw new Exception("Cross section not found at location");

      // This is a cross section with raw data
      XSBase xsBase = crossSection.BaseCrossSection;
      if (xsBase == null)
        throw new Exception("Not a cross section with processed data");

      for (int i = 0; i < xsBase.NumberOfProcessedLevels; i++)
      {
        xsBase.ProcessedResistanceFactors[i] *= 1.1;
      }

      // Do not call CalculateProcessedData in this case, because that will reset the
      // processed data. Instead set the protected flag, such that data is not updated
      // automatically
      xsBase.ProcessedDataProtected = true;

    }

    #endregion

  }
}
