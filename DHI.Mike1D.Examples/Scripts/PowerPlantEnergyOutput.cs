using System;
using System.Collections.Generic;
using System.Linq;
using DHI.Generic.MikeZero;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Engine.ModuleData;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;
using DHI.Mike1D.StructureModule;
using DDoubleGetter = System.Func<double>;

namespace DHI.Mike1D.Examples.Scripts
{

  /// <summary>
  /// Calculates PowerPlant Power and Energy and puts results in
  /// the default HD result file.
  /// </summary>
  public class PowerPlantEnergyOutput
  {
    // Id's of structures that are PowerPlants
    private string[] _powerPlantStructureIds = new string[]
                                              {
                                                "SimpleSO",
                                                "SimpleSO5",
                                              };

    // Common efficiency table for all PowerPlants
    private XYTable _efficiencyTable = new XYTable(
      new[] {0, 0.1, 1.0, 5.0, 100},      // dH values
      new[] {0, 0.3, 0.5, 0.7, 0.95});    // Efficiency

    private IMike1DController _controller;
    private List<PowerPlantData> _powerPlants;

    /// <summary>
    /// Method that is invoked when controller is created.
    /// </summary>
    [Script]
    public void ControllerCreated(IMike1DController controller)
    {
      _controller                 =  controller;
      _controller.ControllerEvent += ControllerOnControllerEvent;
    }

    /// <summary>
    /// Method that is invoked whenever the controller changes state.
    /// When the controller is initialized, we can setup the PowerPlant
    /// calculations
    /// </summary>
    private void ControllerOnControllerEvent(object sender, ControllerEventArgs e)
    {
      if (e.State == ControllerState.Initialized)
      {
        Initialize();
      }
    }

    private void Initialize()
    {
      _powerPlants = new List<PowerPlantData>();

      // Proxy, used for getting access to engine variables
      ProxyUtil proxy = new ProxyUtil(_controller.EngineNet);

      // Quantities
      Quantity powerQuantity = new Quantity("EnergyPower", "Energy Power", eumItem.eumIPower, eumUnit.eumUkwatt);
      Quantity energyQuantity = new Quantity("EnergyProduced", "Energy produced", eumItem.eumIenergy, eumUnit.eumUKiloWattHour);

      // Add quantities to default HD results
      ResultSpecification hdResults = _controller.Mike1DData.ResultSpecifications.Find(rs => rs.ID == "DefaultHDResults");
      hdResults.What.Add(powerQuantity);
      hdResults.What.Add(energyQuantity);

      // Setup all powerplants
      foreach (string powerPlantStructureId in _powerPlantStructureIds)
      {
        // Find structure with Id - returns null if not found
        IStructure structure = _controller.Mike1DData.Network.StructureCollection.Structures.FirstOrDefault(
          s => StringComparer.OrdinalIgnoreCase.Equals(s.ID, powerPlantStructureId));

        // Only applicable for Discharge structures
        DischargeStructure dischargeStructure = structure as DischargeStructure;

        // null if structure was not an IDischargeStructure, or powerPlantStructureId was not found
        if (dischargeStructure == null) continue;

        // Find reach and grid point of structure
        ILocation   location    = dischargeStructure.Location;
        EngineReach engineReach = _controller.EngineNet.FindReach(location);
        GridPoint   gridPoint   = engineReach.GetClosestGridPoint<GridPoint>(dischargeStructure.Location);

        // Getters for waterlevels upstream and downstream of structure.
        DDoubleGetter upWl = proxy.GetterUnboxed(engineReach, gridPoint.PointIndex - 1, Quantity.Create(PredefinedQuantity.WaterLevel));
        DDoubleGetter doWl = proxy.GetterUnboxed(engineReach, gridPoint.PointIndex + 1, Quantity.Create(PredefinedQuantity.WaterLevel));

        // Data for this reach. Id of structure is added to description of quantity
        var powerDataReach  = CreateEngineDataReach(CreateIdQuantity(powerQuantity,  structure.ID), engineReach, gridPoint);
        var energyDataReach = CreateEngineDataReach(CreateIdQuantity(energyQuantity, structure.ID), engineReach, gridPoint);

        PowerPlantData powerPlantData = new PowerPlantData();
        powerPlantData.Structure  = dischargeStructure;
        powerPlantData.UpWl       = upWl;
        powerPlantData.DoWl       = doWl;
        powerPlantData.PowerData  = powerDataReach;
        powerPlantData.EnergyData = energyDataReach;

        _powerPlants.Add(powerPlantData);
      }

      _controller.EngineNet.PostTimeStepEvent += PostTimestep;
    }

    /// <summary>
    /// Creates an <see cref="IEngineDataItem{T}"/> that stores one value on the gridpoint
    /// where the structure is located.
    /// Adds this <see cref="IEngineDataItem{T}"/> to the <see cref="DataModule"/> for supporting outputting.
    /// Returns an <see cref="IEngineDataReach{T}"/> holding the value to update every timestep.
    /// </summary>
    private EngineDataReach<double> CreateEngineDataReach(Quantity quantity, EngineReach engineReach, GridPoint gridPoint)
    {
      // Grid point index where PowerPlant data belongs - required to get output right
      int[] indexList = new int[] { gridPoint.PointIndex };

      // EngineDataItem that stores data for a single PowerPlant and transfers data to result file
      var engineData = new EngineDataItemAll<double>(_controller.EngineNet, quantity)
                       {
                         ReachesData = new IEngineDataReach<double>[_controller.EngineNet.Reaches.Count]
                       };
      // Add data items to data module
      _controller.EngineNet.DataModule.AddDataItem(engineData);

      // Data for this reach, containing one value at the structure grid point
      var engineDataReach = new EngineDataReach<double>()
                            {
                              Values    = new double[indexList.Length],
                              IndexList = indexList,
                            };

      // Register reach data with dataitem
      engineData.ReachesData[engineReach.ReachListIndex] = engineDataReach;

      return engineDataReach;
    }

    private static Quantity CreateIdQuantity(Quantity quantity, string id)
    {
      Quantity idQuantity = new Quantity(quantity.Id,
                                         quantity.Description + ": " + id,
                                         quantity.EumQuantity.Item,
                                         quantity.EumQuantity.Unit);
      return idQuantity;
    }

    /// <summary>
    /// Method that is called whenever a timestep has finished
    /// </summary>
    private void PostTimestep(DateTime time)
    {
      // Water density in kg/m3
      const double rho = 1000;
      // Time step in hours
      double dtHours = _controller.EngineNet.EngineTime.DtSpan.TotalHours;

      foreach (PowerPlantData powerPlantData in _powerPlants)
      {
        double dh = powerPlantData.UpWl() - powerPlantData.DoWl();
        double Q  = powerPlantData.Structure.Discharge;

        if (dh < 0 || Q < 0) continue; // invalid data

        double efficiency = _efficiencyTable.YFromX(dh, ExtrapolationTypes.Nearest);
        // Power in kWatt
        double kpower      = 0.001 * dh * Q * efficiency * 9.81 * rho;

        powerPlantData. PowerData[0]  = kpower;
        powerPlantData.EnergyData[0] += kpower * dtHours;
      }
    }

    /// <summary>
    /// Helper class, storing data for calculating power and energy.
    /// </summary>
    class PowerPlantData
    {
      public IDischargeStructure Structure;
      public DDoubleGetter UpWl;
      public DDoubleGetter DoWl;
      public EngineDataReach<double> PowerData;
      public EngineDataReach<double> EnergyData;
    }

  }





}
