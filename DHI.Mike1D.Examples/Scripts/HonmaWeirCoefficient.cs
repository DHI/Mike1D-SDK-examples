using System;
using System.Collections.Generic;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.StructureModule;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script class that can modify the Honma Weir coefficient 
  /// based on an upstream water level and a lookup into a table
  /// </summary>
  public class HonmaWeirCoefficient
  {
    /// <summary>
    /// Data necessary for each Honma weir
    /// </summary>
    class HonmaWeirInfo
    {
      /// <summary> Weir to update </summary>
      public HonmaWeir Weir;
      /// <summary> Table containing water levels and weir coefficients </summary>
      public XYTable Coefficient;
      /// <summary> Getting water level upstream of structure </summary>
      public DDoubleGetter WaterLevelGetter;
    }

    /// <summary>
    /// All Honma weirs that are active
    /// </summary>
    private readonly List<HonmaWeirInfo> _weirCoefficients = new List<HonmaWeirInfo>();

    /// <summary>
    /// Controller
    /// </summary>
    IMike1DController _controller;

    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void Initialize(Mike1DData mike1DData)
    {
      // Define which weirs that should be active, and define their table
      Dictionary<string, XYTable> weirIdTable = new Dictionary<string, XYTable>(StringComparer.OrdinalIgnoreCase);

      // For two weirs, provide table wl->weir-Coefficient. This data could be read from a file or similar.
      weirIdTable.Add("weir loop2", new XYTable(new[] { 0.1, 0.5, 1, 5, 10, }, new[] { 1.1, 1.2, 1.5, 1.6, 1.65 }));
      weirIdTable.Add("weir loop3", new XYTable(new[] { 0.1, 0.5, 1, 5, 10, }, new[] { 0.1, 0.2, 0.5, 0.6, 0.65 }));

      // Match structures and coefficient tables.
      foreach (IStructure structure in mike1DData.Network.StructureCollection.Structures)
      {
        HonmaWeir honmaWeir = structure as HonmaWeir;
        XYTable table;
        if (honmaWeir != null && weirIdTable.TryGetValue(structure.ID, out table))
        {
          _weirCoefficients.Add(new HonmaWeirInfo() { Weir = honmaWeir, Coefficient = table });
        }
      }
    }

    /// <summary>
    /// Method that is invoked when controller is created
    /// </summary>
    [Script]
    public void ControllerCreated(IMike1DController controller)
    {
      _controller = controller;
      _controller.ControllerEvent += Controller_ControllerEvent;
    }

    /// <summary>
    /// Method that is invoked when controller changes state
    /// </summary>
    private void Controller_ControllerEvent(object sender, ControllerEventArgs e)
    {
      // When model is prepared, we can connect to the HD Module
      if (e.State == ControllerState.Prepared)
      {
        // We are done with the ControllerEvent, so unregister
        _controller.ControllerEvent -= Controller_ControllerEvent;

        // Register for event triggered every time step, where the weir coefficient will be updated
        _controller.EngineNet.PostTimeStepEvent += UpdateWeirCoefficients;

        // Find upstream H gridpoint, where to extract the water level.
        ProxyUtil proxy = new ProxyUtil(_controller.EngineNet);
        for (int i = 0; i < _weirCoefficients.Count; i++)
        {
          HonmaWeir weir = _weirCoefficients[i].Weir;

          // Find location of weir, and upstream reach and grid point - for a water level getter we need the H grid point
          // This is chainage-upstream and not flow-upstream.
          EngineReach engineReach = _controller.EngineNet.FindReach(weir.Location);
          if (engineReach == null) continue; // throw error?
          GridPoint gp = engineReach.GetClosestUpstreamGridPoint(weir.Location.Chainage, gpm => gpm is HGridPoint, false);
          if (gp == null) continue; // throw error?

          // Extract a getter which will return the current water level
          _weirCoefficients[i].WaterLevelGetter = proxy.GetterUnboxed(engineReach, gp.PointIndex, Quantity.Create(PredefinedQuantity.WaterLevel));
        }
      }
    }

    /// <summary>
    /// In PostTimeStepEvent event, update the weir coefficient
    /// Note that this could even be made time dependent.
    /// </summary>
    private void UpdateWeirCoefficients(DateTime time)
    {
      for (int i = 0; i < _weirCoefficients.Count; i++)
      {
        HonmaWeir weir = _weirCoefficients[i].Weir;
        XYTable table = _weirCoefficients[i].Coefficient;
        DDoubleGetter getter = _weirCoefficients[i].WaterLevelGetter;

        // Update weir coefficient
        if (getter != null)
        {
          double upstreamWaterLevel = getter();
          double depthOverCrest = upstreamWaterLevel - weir.CrestLevel;
          weir.WeirCoefficient = table.YFromX(depthOverCrest, ExtrapolationTypes.Nearest);
        }
      }
    }
  }
}
