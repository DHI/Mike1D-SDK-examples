using System;
using System.Collections.Generic;
using System.Globalization;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.StructureModule;
using IMike1DPlugin = DHI.Mike1D.Plugins.IMike1DPlugin;

namespace DHI.Mike1D.Examples.Plugins
{
  public class HonmaWeirCoefficient : IMike1DPlugin
  {
    class HonmaWeirInfo
    {
      public HonmaWeir Weir;
      public XYTable Coefficient;
      public IDoubleGetter Getter;
    }

    private List<HonmaWeirInfo> _weirCoefficients = new List<HonmaWeirInfo>();

    public void Initialize(IList<Mike1DPluginArgument> arguments, Mike1DData mike1DData)
    {
      // Read arguments from .m1da file. Can also be read from other sources.
      Dictionary<string, XYTable> weirIdTable = new Dictionary<string, XYTable>(StringComparer.OrdinalIgnoreCase);
      foreach (Mike1DPluginArgument arg in arguments)
      {
        if (arg.Key == "Weir")
        {
          // Assuming value contains structure id and then pairs of water depths and weir coefficients, like:
          // weir = 'MyStrucId;0.1;0.1;0.5;0.2;1;0.5;5;0.6;10;0.65'
          string[] split = arg.Value.SplitQuoted(';', '"');
          string weirId = split[0];

          XYTable table = new XYTable((split.Length-1)/2);
          for (int i = 1; i < split.Length; i++)
          {
            double val = double.Parse(split[i], CultureInfo.InvariantCulture);
            if (i%2 == 1)
              table.XValues[(i-1)/2] = val;
            else
              table.YValues[(i-1)/2] = val;
          }
          weirIdTable.Add(weirId, table);
        }
      }

      // Match structures and coefficient tables.
      foreach (IStructure structure in mike1DData.Network.StructureCollection.Structures)
      {
        HonmaWeir honmaWeir = structure as HonmaWeir;
        XYTable table;
        if (honmaWeir != null && weirIdTable.TryGetValue(structure.ID, out table))
        {
          _weirCoefficients.Add(new HonmaWeirInfo(){Weir = honmaWeir, Coefficient = table});
        }
      }

      // We need an event when controller has been prepared, which is a two step procedure:
      // ControllerCreatedEvent and then ControllerEvent
      mike1DData.ControllerCreatedEvent += ControllerCreatedEvent;
    }

    IMike1DController controller;

    private void ControllerCreatedEvent(object sender, ControllerCreatedEventArgs controllerCreatedEventArgs)
    {
      // - and then the ControllerEvent
      controller = controllerCreatedEventArgs.Controller;
      controller.ControllerEvent += Controller_ControllerEvent;
    }

    void Controller_ControllerEvent(object sender, ControllerEventArgs e)
    {
      // When model is prepared, we can connect to the HD Module
      if (e.State == ControllerState.Prepared)
      {
        // We are done with the ControllerEvent, so unregister
        controller.ControllerEvent -= Controller_ControllerEvent;

        // Register for event triggered every time step, where the weir coefficient will be updated
        controller.EngineNet.PostTimeStepEvent += UpdateWeirCoefficients;

        // Find upstream H gridpoint, where to extract the water level.
        ProxyUtil proxy = new ProxyUtil(controller.EngineNet);
        for (int i = 0; i < _weirCoefficients.Count; i++)
        {
          HonmaWeir weir = _weirCoefficients[i].Weir;
          
          // Find location of weir, and upstream reach and grid point - for a water level getter we need the H grid point
          // This is chainage-upstream and not flow-upstream.
          EngineReach engineReach = controller.EngineNet.FindReach(weir.Location);
          if (engineReach == null) continue; // throw error?
          GridPoint gp = engineReach.GetClosestUpstreamGridPoint(weir.Location.Chainage, gpm => gpm is HGridPoint, false);
          if (gp == null) continue; // throw error?

          // Extract a getter which will return the current water level
          _weirCoefficients[i].Getter = proxy.Getter(engineReach, gp.PointIndex, Quantity.Create(PredefinedQuantity.WaterLevel));
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
        IDoubleGetter getter = _weirCoefficients[i].Getter;

        // Update weir coefficient
        if (getter != null)
        {
          double upstreamWaterLevel = getter.GetValue();
          double depthOverCrest = upstreamWaterLevel-weir.CrestLevel;
          weir.WeirCoefficient = table.YFromX(depthOverCrest, ExtrapolationTypes.Nearest);
        }
      }
    }

    #region Deprecated implementation

    public void Initialize(IList<Mike1DPluginArgument> arguments)
    {
    }

    public void OnSetupLoaded(Mike1DData mike1DData)
    {
    }

    #endregion


  }
}
