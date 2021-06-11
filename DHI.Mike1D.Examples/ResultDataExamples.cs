using System;
using System.Collections.Generic;
using DHI.Mike1D.Generic;
using DHI.Mike1D.ResultDataAccess;
using NUnit.Framework;

namespace DHI.Mike1D.Examples
{
  /// <summary>
  /// Examples on how to read results using the ResultData object.
  /// </summary>
  public class ResultDataExamples
  {

    /// <summary>
    /// Static constructor, setting up search paths for MIKE assemblies
    /// </summary>
    static ResultDataExamples()
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
    /// Example stepping through the structure of the 
    /// ResultData object
    /// </summary>
    /// <param name="resultFilepath">Path to a result file</param>
    public static void FirstExample(string resultFilepath)
    {
      // load a result file
      IResultData resultData = new ResultData();
      resultData.Connection = Connection.Create(resultFilepath);
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.Load(resultDiagnostics);

      if (resultDiagnostics.ErrorCountRecursive > 0)
      {
        // Do some error reporting
        throw new Exception("File could not be loaded");
      }

      // Time definition of the time step stored
      int numtimesteps = resultData.NumberOfTimeSteps;
      DateTime startTime = resultData.StartTime;
      DateTime endTime = resultData.EndTime;
      // times for all timesteps
      IListDateTimes times = resultData.TimesList;

      // Results are stored in DataItem’s, where one DataItem contains e.g. water level.
      // Nodes, Reaches, Catchments and GlobalData of the resultData object will have a 
      // number of data items.
      // A data item will store data for a number of elements and for a number
      // of time steps. 
      // In reaches there will be more than one "element", since a reach will store 
      // data for several grid points. Nodes and catchments will always only have 
      // one element per data item

      IRes1DNodes nodes = resultData.Nodes;
      IRes1DReaches reaches = resultData.Reaches;
      IRes1DCatchments catchments = resultData.Catchments;
      IRes1DGlobalData globals = resultData.GlobalData;

      // Generic loop over catchments/nodes/reaches (just replace catchments with nodes)
      foreach (IRes1DDataSet dataSet in catchments)
      {
        // Each dataset has a number of data items
        IDataItems dataItems = dataSet.DataItems;

        // Loop over all data items
        foreach (IDataItem dataItem in dataItems)
        {
          // A dataitem contains data for one quantity
          IQuantity quantity = dataItem.Quantity;

          // You can check the type of quantity in the data item
          if (quantity.Equals(Quantity.Create(PredefinedQuantity.WaterLevel)))
          {
          }
        }
      }

      // The example will now show how to extract data from a reach.

      // Get the first reach in the list of reaches
      IRes1DReach reach = reaches[0];

      // Extract first data item for reach (there will be more than one)
      // For a HD result file, this will be a water level.
      IDataItem wlDataItem = reach.DataItems[0];

      // Take data for all elements (subset of grid points) for the initial time step
      // For water level quantity: One value for each H-grid point
      float[] wlValues = wlDataItem.TimeData.GetValues(0);

      // For a reach, if the IndexList is defined (not null), this
      // gives the relation to the grid point where the values in 
      // a data item belongs to.
      // For each value in the data item, it indicates the index of the grid point 
      // it belongs to, i.e. the size matches the number of elements in the data item.
      // i.g. for water level: 0, 2, 4, 6, 8,...
      // and  for discharge  : 1, 3, 5, 7,...
      // The IndexList can be null, in which case information on the geometry of the
      // data in the data item exist in the element set
      int[] indexList = wlDataItem.IndexList;

      // Loop over all elements (subset of grid points) in the data item
      for (int i = 0; i < wlDataItem.NumberOfElements; i++)
      {
        // Time series values for this element (grid point)
        float[] wlDataForElement = wlDataItem.CreateTimeSeriesData(i);

        // Check if there is a grid-point relation
        if (indexList != null)
        {
          // Index of grid point in list of grid opints
          int gridPointIndex = wlDataItem.IndexList[i];

          // Grid point where values belong to.
          IRes1DGridPoint gridPoint = reach.GridPoints[gridPointIndex];

          // More detailed information exist in the derived grid point classes
          if (gridPoint is IRes1DHGridPoint)
          {
            IRes1DHGridPoint hGridPoint = gridPoint as IRes1DHGridPoint;

            // An H-grid point also has a cross section. 
            IRes1DCrossSection crossSection = hGridPoint.CrossSection;

            // More detailed information on the cross section exist in the derived types
            if (crossSection is IRes1DOpenCrossSection)
            {
              // The open cross section has all the raw points and cross section markers
              IRes1DOpenCrossSection openXs = crossSection as IRes1DOpenCrossSection;
              IRes1DCrossSectionPoint lowestXsPoint = openXs.Points[openXs.LowestPoint];
            }
          }
        }
      }

    }


    /// <summary>
    /// Example modifying data in a specific reach and a specific node
    /// </summary>
    [Test]
    public void ModifyReachValues()
    {
      string resultFilepath = System.IO.Path.Combine(ExampleBase.ExampleRoot, @"Results\vida96-3.res1d");

      // load a result file
      IResultData resultData = new ResultData();
      resultData.Connection = Connection.Create(resultFilepath);
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.Load(resultDiagnostics);

      if (resultDiagnostics.ErrorCountRecursive > 0)
      {
        // Do some error reporting
        throw new Exception("File could not be loaded");
      }

      Quantity wlQuantity = Quantity.Create(PredefinedQuantity.WaterLevel);

      // The ResultDataSearch helps doing searching of results efficiently
      ResultDataSearch searcher = new ResultDataSearch(resultData);

      // Find all branches named "VIDAA-NED" - there are three, due to junction splits
      IList<IRes1DReach> reaches = searcher.FindReaches("VIDAA-NED");

      foreach (IRes1DReach reach in reaches)
      {
        // Find the water level data item, and update the water level with 0.2 between
        // chainage 7000 and 1000 for all time steps

        IDataItem wlData = reach.GetDataItem(wlQuantity);

        for (int i = 0; i < wlData.NumberOfElements; i++)
        {
          // Index of grid point where element value i belongs to
          int igp = wlData.IndexList[i];
          // Chainage of grid point where element value i belongs to
          double gpChainage = reach.GridPoints[igp].Chainage;
          if (7000 < gpChainage && gpChainage < 10000)
          {
            // Update for all time steps
            for (int j = 0; j < wlData.NumberOfTimeSteps; j++)
            {
              float val = wlData.TimeData.GetValue(j, i);
              val += 0.2f;
              wlData.TimeData.SetValue(j, i, val);
            }
          }
        }

        // Also check if the end-node is on this span
        if (7000 < reach.LocationSpan.EndChainage && reach.LocationSpan.EndChainage < 10000)
        {
          IDataItem wlNodeData = resultData.Nodes[reach.EndNodeIndex].GetDataItem(wlQuantity);
          // Update for all time steps
          for (int j = 0; j < wlNodeData.NumberOfTimeSteps; j++)
          {
            // In a node there is only one element value, so i = 0
            float val = wlNodeData.TimeData.GetValue(j, 0);
            val += 0.2f;
            wlNodeData.TimeData.SetValue(j, 0, val);
          }
        }
      }

      // Save file with a new name
      resultData.Connection.FilePath.FileNameWithoutExtension += "-testModify";
      resultData.Save();

    }
  }
}
