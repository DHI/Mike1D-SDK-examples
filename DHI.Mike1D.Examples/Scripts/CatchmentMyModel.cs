using System;
using System.Collections.Generic;
using DHI.Generic.MikeZero;
using DHI.Mike1D.BoundaryModule;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;


/*
 * This script file contains two classes:
 *
 * - CatchmentMyModelScript: Sets up user written catchment model and adds it to the simulation
 *
 * - CatcmentMyModel: A user written catchment model, somewhat similar to the Kinematic Wave catchment model.
 */

namespace DHI.Mike1D.Examples.Scripts
{


  /// <summary>
  /// Script class. Methods marked with the <code>[script]</code>
  /// attribute will be executed when the MIKE 1D engine is running
  /// </summary>
  public class CatchmentMyModelScript
  {
    // Conversion from mm/h to m/s
    const double mmph2mps = 1 / 3.6 * 1e-6;

    /// <summary>
    /// Add additional outputs to RR result file.
    /// That can ease debugging results while developing the model.
    /// </summary>
    [Script(Order = 1)]
    public void AddOutputs(Mike1DData mike1DData)
    {
      mike1DData.ResultSpecifications[0].What.Add(Quantity.Create(PredefinedQuantity.TotalInfiltration));
      mike1DData.ResultSpecifications[0].What.Add(CatchmentMyModel.DepthQuantity);
      mike1DData.ResultSpecifications[0].What.Add(CatchmentMyModel.WLossQuantity);
      mike1DData.ResultSpecifications[0].What.Add(CatchmentMyModel.SLossQuantity);

      // Add similar quantities for Kinematic Wave catchment - for comparison
      mike1DData.ResultSpecifications[0].What.Add(RRQuantities.Create(PredefinedQuantityRR.KinematicWaveDepth, CatchmentKinematicWave.SurfaceType.PerviousMedium));
      mike1DData.ResultSpecifications[0].What.Add(RRQuantities.Create(PredefinedQuantityRR.KinematicWaveStorageLoss, CatchmentKinematicWave.SurfaceType.PerviousMedium));
      mike1DData.ResultSpecifications[0].What.Add(RRQuantities.Create(PredefinedQuantityRR.KinematicWaveWettingLoss, CatchmentKinematicWave.SurfaceType.PerviousMedium));
    }

    /// <summary>
    /// Setup and add two catchments of the new user defined type (<see cref="CatchmentMyModel"/>)
    /// to the simulation
    /// </summary>
    [Script]
    public void AddMyCatchments(Mike1DData mike1DData)
    {
      // Create and configure two catchments
      List<CatchmentMyModel> myModels = new List<CatchmentMyModel>();
      var c1 = new CatchmentMyModel("C1") { Area =   750, ManningM = 65, Slope = 0.01, Width = 50, };
      var c2 = new CatchmentMyModel("C2") { Area = 10000, ManningM = 65, Slope = 0.03, Width = 100, };

      c1.Surface.WettingCapacity = 0.05e-3;
      c2.Surface.WettingCapacity = 0.05e-3;
      c1.Surface.StorageCapacity = 2.0e-3;
      c2.Surface.StorageCapacity = 2.0e-3;
      c1.Surface.Infiltration = new Horton() { F0 = 2 * mmph2mps, Fc = 0.5 * mmph2mps, Kwet = 0.0015, Kdry = 3.0e-5 };
      c2.Surface.Infiltration = new Horton() { F0 = 2 * mmph2mps, Fc = 0.5 * mmph2mps, Kwet = 0.0015, Kdry = 3.0e-5 };

      myModels.Add(c1);
      myModels.Add(c2);

      foreach (var myModel in myModels)
      {
        myModel.TimeStep = TimeSpan.FromSeconds(60);
        mike1DData.RainfallRunoffData.Catchments.Add(myModel);
      }
    }

    /// <summary>
    /// Add two similar Kinematic Wave catchments, just for comparison.
    /// If the comparison is not required, just delete this method.
    /// </summary>
    [Script]
    public void AddKWCatchments(Mike1DData mike1DData)
    {
      // Conversion from mm/h to m/s

      List<CatchmentKinematicWave> myModels = new List<CatchmentKinematicWave>();
      var c1 = new CatchmentKinematicWave("KW1") { Area =   750, Slope = 0.01, Length = 750.0/50, };
      var c2 = new CatchmentKinematicWave("KW2") { Area = 10000, Slope = 0.03, Length = 10000.0/100, };

      foreach (var c in c1.Surfaces) { c.AreaFraction = 0; }
      foreach (var c in c2.Surfaces) { c.AreaFraction = 0; }
      c1.Surfaces[3].AreaFraction = 1.0;
      c2.Surfaces[3].AreaFraction = 1.0;
      c1.Surfaces[3].ManningM = 65;
      c2.Surfaces[3].ManningM = 65;
      c1.Surfaces[3].WettingCapacity = 0.05e-3;
      c2.Surfaces[3].WettingCapacity = 0.05e-3;
      c1.Surfaces[3].StorageCapacity = 2.0e-3;
      c2.Surfaces[3].StorageCapacity = 2.0e-3;
      c1.Surfaces[3].Infiltration = new Horton() { F0 = 2 * mmph2mps, Fc = 0.5 * mmph2mps, Kwet = 0.0015, Kdry = 3.0e-5 };
      c2.Surfaces[3].Infiltration = new Horton() { F0 = 2 * mmph2mps, Fc = 0.5 * mmph2mps, Kwet = 0.0015, Kdry = 3.0e-5 };

      myModels.Add(c1);
      myModels.Add(c2);

      foreach (var myModel in myModels)
      {
        myModel.TimeStep = TimeSpan.FromSeconds(60);
        mike1DData.RainfallRunoffData.Catchments.Add(myModel);
      }
    }
  }


  /// <summary>
  /// Custom/user defined catchment model.
  /// <para>
  /// This catchment model does similar calculations as the Kinematic Wave (Model B)
  /// catchment model. This assumes a catchment with a fairly small and even surface, e.g. constant slope,
  /// as e.g. a fairly flat grass area or paved area.
  /// </para>
  /// <para>
  /// For storage and loss calculations, it uses the CatchmentSurface class, which handles
  ///  - Rain and evaporation.
  ///  - Wetting Loss: The first few drops of rain that sticks to the surface that they land on.
  ///  - Storage Loss: Depth of water that does not run off, due to depressions and other obstacles.
  ///  - Depth: Depth of water that can move and run off
  ///  - Infiltration. Horton or Green-Ampt infiltration is implemented.
  /// </para>
  /// <para>
  /// Routing is based on the Mannings equation, assuming same flow over the entire catchment area.
  /// </para>
  /// <para>
  /// For every time step, the following methods are called:
  /// <code>
  /// PrepareForTimeStep()
  /// UpdateStorage()
  /// UpdateRouting()
  /// FinalizeTimeStep()
  /// CalculateStatistics()
  /// </code>
  /// </para>
  /// </summary>
  public class CatchmentMyModel : Catchment
  {
    public static IQuantity DepthQuantity = new Quantity("CDepth", "Depth of water", eumItem.eumIStorageDepth, eumUnit.eumUmeter);
    public static IQuantity SLossQuantity = new Quantity("CStorageDepth", "Storage Depth", eumItem.eumIStorageDepth, eumUnit.eumUmeter);
    public static IQuantity WLossQuantity = new Quantity("CWettingDepth", "Wetting Depth", eumItem.eumIStorageDepth, eumUnit.eumUmeter);


    /// <summary>
    /// Create new catchment, given its <paramref name="name"/>.
    /// The <see cref="ICatchment.ModelId"/> will equal the name.
    /// </summary>
    public CatchmentMyModel(string name) : base(name)
    {
    }

    /// <summary>
    /// Create new catchment, given its <paramref name="modelId"/> and <paramref name="name"/>.
    /// </summary>
    public CatchmentMyModel(string modelId, string name) : base(modelId, name)
    {
    }

    /// <summary>
    /// The type name of this catchment.
    /// This is a unique name for all catchment model types.
    /// </summary>
    public override string Type()
    {
      return "KWMyWay";
    }


    #region Catchment paramters
    // This region contains setup parameters for this catchment model,
    // i.e. parameters provided initially and used through the simulation.

    /// <summary>
    /// Manning number
    /// Unit: [m^(2/3)/s]
    /// </summary>
    public double ManningM { get; set; }

    /// <summary>
    /// Width of the sub-catchment.
    /// Unit: [m]
    /// </summary>
    public double Width { get; set; }

    /// <summary>
    /// The slope of the catchment/channel.
    /// Unit: [m]
    /// </summary>
    public double Slope { get; set; }


    #endregion

    #region State variables
    // This region contains state variables for this catchment model,
    // i.e. variables that change with each time-step in the model.

    /// <summary>
    /// Catchment surface class, handling wetting loss, storage loss, infiltration and evaporation
    /// </summary>
    public CatchmentSurface Surface
    {
      get { return _surface; }
      set { _surface = value; }
    }

    /// <summary>
    /// Catchment surface class, handling wetting loss, storage loss, infiltration and evaporation
    /// </summary>
    private CatchmentSurface _surface = new CatchmentSurface();

    #endregion

    #region Helper variables
    // Various helper variables using during evaluation of the catchment runoff

    /// <summary> Effective time step in [s] </summary>
    private double _effectiveTimeStepSeconds;
    /// <summary> Actual rainfall. Unit: [m/s]</summary>
    private double _actualRainfall;
    /// <summary> Potential evaporation. Actual evaporation is limited to water on surface. Unit: [m/s] </summary>
    private double _potentialEvaporation;

    #endregion


    ///<summary>
    /// Initialize Rainfall Runoff model. Sets up static data.
    ///</summary>
    public override void Initialize(IDiagnostics diagnostics)
    {
      // The result output system is based on "offers" and value-getters (delegates),
      // and here we set up which variables are available for output
      // in the result files. Every variable to output must have a matching 
      // quantity and delegate, and the then output system handles the rest. 

      // A catchment must offer TotalRunOff and NetRainfall.
      // The rest is for convenience and result verifications.

      _offers = new List<IQuantity>();
      _offerDelegates = new List<Func<double>>();

      _offers.Add(Quantity.Create(PredefinedQuantity.TotalRunOff));
      _offerDelegates.Add(() => (_runoff + _additionalFlow));

      _offers.Add(Quantity.Create(PredefinedQuantity.NetRainfall));
      _offerDelegates.Add(() => (_actualRainfall - _potentialEvaporation));

      _offers.Add(Quantity.Create(PredefinedQuantity.ActualEvaporation));
      _offerDelegates.Add(() => _surface.EvapActual);

      _offers.Add(Quantity.Create(PredefinedQuantity.ActualRainfall));
      _offerDelegates.Add(() => _actualRainfall);

      _offers.Add(Quantity.Create(PredefinedQuantity.TotalInfiltration));
      _offerDelegates.Add(() => _surface.InfiltrationActual);

      _offers.Add(DepthQuantity);
      _offerDelegates.Add(() => _surface.Depth);

      _offers.Add(WLossQuantity);
      _offerDelegates.Add(() => _surface.WettingLoss);

      _offers.Add(SLossQuantity);
      _offerDelegates.Add(() => _surface.StorageLoss);
    }

    #region Handling catchment boundaries/sources, typically rain and evaporation
    // Boundaries like rain and evaporation are specified in the BoundaryModule,
    // and then applied to each catchment. The following methods handle this for rain
    // and evaporation. For rain and evaporation, field variables are available
    // in the Catchment base class. If additional types of boundaries are required
    // in your model, the boundary field variable must be added here, as e.g.
    //    protected IBoundarySource _boundarySourceSunFactor;


    /// <summary>
    /// Get a list of boundary types required by this catchment
    /// </summary>
    public override IList<CatchmentSourceBoundaryTypes> GetRequiredTypes()
    {
      var res = new List<CatchmentSourceBoundaryTypes>();

      // Check if boundaries have already been assigned first.
      if (_boundarySourceRainfall == null)
        res.Add(CatchmentSourceBoundaryTypes.Rainfall);
      if (_boundarySourceEvaporation == null)
        res.Add(CatchmentSourceBoundaryTypes.Evaporation);

      return res;
    }

    /// <summary>
    /// Apply Catchment boundaries to this catchment
    /// </summary>
    public override void ApplyBoundary(CatchmentSourceBoundaryTypes catchmentSourceBoundaryType, IBoundarySource catchmentSourceBoundary)
    {
      switch (catchmentSourceBoundaryType)
      {
        case CatchmentSourceBoundaryTypes.Rainfall:
          _boundarySourceRainfall = catchmentSourceBoundary;
          break;
        case CatchmentSourceBoundaryTypes.Evaporation:
          _boundarySourceEvaporation = catchmentSourceBoundary;
          break;
        default:
          throw new ArgumentOutOfRangeException();
      }
    }

    /// <summary>
    /// Apply global boundaries to this catchment
    /// </summary>
    public override void ApplyBoundary(GlobalSourceBoundaryTypes type, GlobalGeoLocatedSource geoLocatedSource)
    {
      switch (type)
      {
        case GlobalSourceBoundaryTypes.Rainfall:
          _boundarySourceRainfall = geoLocatedSource;
          break;
        case GlobalSourceBoundaryTypes.Evaporation:
          _boundarySourceEvaporation = geoLocatedSource;
          break;
        default:
          throw new ArgumentOutOfRangeException("type");
      }
    }

    #endregion


    #region Time stepping routines

    /// <summary>
    /// Prepares catchment for the next time step.
    /// </summary>
    protected override void PrepareForTimeStep()
    {
      // Update time for this time step. If timestep is dynamic, be sure to 
      // calculate _effectiveTimeStep before calling UpdateTime
      UpdateTime();
      _effectiveTimeStepSeconds = _effectiveTimeStep.TotalSeconds;

      // Transfer current state to Old
      _surface.DepthOld = _surface.Depth;
    }

    /// <summary>
    /// Updates the storage for the catchment.
    /// </summary>
    protected override void UpdateStorage()
    {
      UpdateForcings();

      // Storage calculations is handled by the _surface class
      _surface.PerformTimeStep(_effectiveTimeStepSeconds, _actualRainfall, _potentialEvaporation);
    }

    /// <summary>
    /// This routine updates the boundary/forcing values of rainfall, evaporation etc
    /// </summary>
    internal void UpdateForcings()
    {
      if (_boundarySourceRainfall != null)
        _actualRainfall = _boundarySourceRainfall.GetAccumulatedValue(_timeOld, _timeNew) / (_effectiveTimeStep.TotalSeconds);

      if (_boundarySourceEvaporation != null)
        // GetAccumulatedValue returns a negative value for evaporation boundary
        _potentialEvaporation = -_boundarySourceEvaporation.GetAccumulatedValue(_timeOld, _timeNew) /
                                (_effectiveTimeStep.TotalSeconds);
      else
        // Apply a constant evaporation
        _potentialEvaporation = 5e-5 / 3600.0; // 1.2 mm / day

      // Only allow evaporation in dry periods
      if (_actualRainfall > 0)
        _potentialEvaporation = 0;
    }


    /// <summary>
    /// This routine updates the routing and calculates the runoff.
    /// <para>
    /// Based on state of storages and previously routed water,
    /// calculate the routing of water from the storages and
    /// out of the catchment.
    /// </para>
    /// <para>
    /// This example Implements a simple kinematic wave routing approach:
    /// Q = M * A * R^(2/3) * sqrt(I)
    ///   = M * W*D * D^(2/3) * sqrt(I)
    ///   = M * W * D^(5/3) * sqrt(I)
    /// where D is the depth of the storage.
    /// </para>
    /// </summary>
    protected override void UpdateRouting()
    {
      // Exponent for runoff calculations
      const double exponent = 1.6667;

      // Routing must be based on the amount of water before rain and evaporation
      // from this time step is added, i.e. DepthOld
      double depth = _surface.DepthOld;
      double power = System.Math.Pow(depth, exponent);
      double kinematicFactor = ManningM * Width * System.Math.Sqrt(Slope);
      _runoff = kinematicFactor * power; // [m^(4/3)/s] * [m^(5/3)]

      // The Depth has rain and evaporation added for this time step, and we
      // need to remove the runoff from the depth.
      _surface.Depth -= _runoff * _effectiveTimeStepSeconds / _area;

      if (_surface.Depth < 0.0)
      {
        // correction: the (negative) depth is subtracted from the runoff
        _runoff += _surface.Depth * _area / _effectiveTimeStepSeconds;
        _surface.Depth = 0.0;
      }
    }

    /// <summary>
    /// In case any calculations are required after UpdateRouting
    /// </summary>
    protected override void FinalizeTimeStep()
    {
    }

    #endregion

    /// <summary>
    /// Volume of water stored in catchment.
    /// </summary>
    public override double VolumeInCatchment()
    {
      double volume = _area * (_surface.Depth + _surface.WettingLoss + _surface.StorageLoss);
      return volume;
    }

    /// <summary>
    /// In order to get HTML summary right, some statistics must be calculated
    /// </summary>
    protected override void CalculateStatistics()
    {
      double dt = (_timeNew - _timeOld).TotalSeconds;

      double flow = _runoff + _additionalFlow;

      // Figure out min/max flow and its time
      if (flow < _minimumFlow)
      {
        _minimumFlow = flow;
        _timeOfMinimumFlow = _timeNew;
      }
      if (flow > _maximumFlow)
      {
        _maximumFlow = flow;
        _timeOfMaximumFlow = _timeNew;
      }

      // Runoff flow is in [m^3/s]
      double totalRunoffVolume = dt * flow;
      _totalRunoffVolume += totalRunoffVolume;

      // Infiltration and evaporation is in [m/s]
      double totalLossVolume = dt * (_surface.InfiltrationActual + _surface.EvapActual) * _area;
      _totalLossVolume += totalLossVolume;

      // Rainfall is in [m/s]
      double totalRainfallVolume = dt * _actualRainfall * _area;
      _totalRainfallVolume += totalRainfallVolume;

      // AdditionalFlow is constant and given in [m3/s]
      double totalAdditionalInflowVolume = dt * _additionalFlow;
      _totalAdditionalInflowVolume += totalAdditionalInflowVolume;

      if (_yearlyStatistics != null)
      {
        int year = _timeNew.Year;
        RRYearlyStat yearlyStat = GetYearlyStat(year);
        yearlyStat.TotalRunoff += totalRunoffVolume;
        yearlyStat.Losses += totalLossVolume;
        yearlyStat.Inflow += totalRainfallVolume + totalAdditionalInflowVolume;
      }
    }
  }

}
