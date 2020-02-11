using System;
using System.Collections.Generic;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Engine.ModuleData;
using DHI.Mike1D.Engine.ModuleHD;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace Scripts
{
  /// <summary>
  /// Modifies bed resistance values.
  /// </summary>
  public class BedResistanceToolboxRunner
  {
    private EngineNet _engineNet;
    private List<BedResistanceCalculatorBase> _bedResistanceCalculators = new List<BedResistanceCalculatorBase>();

    /// <summary>
    /// Method called when IMike1DController is available.
    /// </summary>
    [Script]
    public void Initialize(IMike1DController controller)
    {
      controller.ControllerEvent += ControllerOnControllerEvent;
    }

    private void ControllerOnControllerEvent(object sender, ControllerEventArgs e)
    {
      if (e.State != ControllerState.Prepared)
        return;

      var controller = (IMike1DController)sender;
      _engineNet = controller.EngineNet;

      CreateBedResistanceCalculators();
      InitializeBedResistanceCalculators();

      _engineNet.PostTimeStepEvent += ChangeBedResistance;
    }

    /// <summary>
    /// Populates the list of the bed resistance calculators.
    /// </summary>
    private void CreateBedResistanceCalculators()
    {
      // To add 1/M=a V^b bed resistance velocity V dependence with
      //
      // a = 0.05
      // b = 1.0
      // minimum resistance M_min = 10
      // maximum resistance M_max = 40
      //
      // on a reach with name MyReach from chainage 1000 to 2000 add a line:
      // -----------------------
      // AddFormula(FormulaType.Velocity, 0.01, 0.01, 10, 40, "MyReach", 1000, 2000);
      // -----------------------

      // To apply the same formula on the whole reach use a simplified method:
      // -----------------------
      // AddFormula(FormulaType.Velocity, 0.01, 0.01, 10, 40, "MyReach");
      // -----------------------

      // If the formula needs to be applied globally then add a line:
      // -----------------------
      // AddFormula(FormulaType.Velocity, 0.01, 0.01, 10, 40);
      // -----------------------

      // The velocity dependence of bed resistance using a velocity-resistance table:
      // can be done using:
      // -----------------------
      // double[] velocityValues = { 0.0, 1.0, 2.0 };
      // double[] resistanceValues = { 15.0, 20.0, 40.0 };
      // AddTable(velocityValues, resistanceValues, "MyReach", 1000, 2000);
      // -----------------------
    }

    private void AddFormula(FormulaType formulaType, double coefficientA, double coefficientB, double minimumResistance, double maximumResistance,
                            string reachId=null, double startChainage=Constants.DOUBLE_DELETE_VALUE, double endChainage=Constants.DOUBLE_DELETE_VALUE)
    {
      var formula = GetFormulaCalculator(formulaType);
      formula.SetCoefficients(coefficientA, coefficientB, minimumResistance, maximumResistance);
      if (reachId != null)
        formula.Add(reachId, startChainage, endChainage);
      _bedResistanceCalculators.Add(formula);
    }

    private void AddTable(double[] velocityValues, double[] resistanceValues,
                          string reachId=null, double startChainage=Constants.DOUBLE_DELETE_VALUE, double endChainage=Constants.DOUBLE_DELETE_VALUE)
    {
      var tableBedResistance = new TableBedResistance(_engineNet);
      tableBedResistance.SetTable(velocityValues, resistanceValues);
      if (reachId != null)
        tableBedResistance.Add(reachId, startChainage, endChainage);
      _bedResistanceCalculators.Add(tableBedResistance);
    }

    private BedResistanceFormulaBase GetFormulaCalculator(FormulaType formulaType)
    {
      switch (formulaType)
      {
        case FormulaType.VelocityHydraulicRadius:
          return new FormulaVelocityHydraulicRadius(_engineNet);
        case FormulaType.HydraulicDepth:
          return new FormulaHydraulicDepth(_engineNet);
        case FormulaType.Velocity:
          return new FormulaVelocity(_engineNet);
        default:
          throw new ArgumentOutOfRangeException("Not supported bed resistance formula type.");
      }
    }

    private void InitializeBedResistanceCalculators()
    {
      foreach (var calculator in _bedResistanceCalculators)
        calculator.Initialize();
    }

    private void ChangeBedResistance(DateTime time)
    {
      foreach (var bedResistanceCalculator in _bedResistanceCalculators)
        bedResistanceCalculator.ChangeBedResistance(time);
    }

  }

  /// <summary>
  /// Enumeration for supported bed resistance formula types
  /// </summary>
  public enum FormulaType
  {
    VelocityHydraulicRadius,
    HydraulicDepth,
    Velocity,
  }

  #region Abstract base bed resistance calculators

  /// <summary>
  /// Helper class to store reach grid points for which bed resistance is calculated.
  /// If bed resistance is calculated for whole reach then GridPoints stores all the H points.
  /// </summary>
  public class BedResistanceLocation
  {
    public IHDReach Reach;
    public List<IHDHGridPoint> GridPoints = new List<IHDHGridPoint>();
  }

  /// <summary>
  /// Base class for bed resistance calculations.
  /// </summary>
  public abstract class BedResistanceCalculatorBase
  {
    protected List<LocationSpan> _locationSpans = new List<LocationSpan>();
    protected List<BedResistanceLocation> _locations = new List<BedResistanceLocation>();
    protected EngineNet _engineNet;
    protected EngineDataItemAll<double> _velocityDataItem;

    protected double _waterLevel;
    protected double _hydraulicRadius;
    protected double _hydraulicDepth;
    protected double _velocity;

    public double MinVelocity = 1e-6;
    public double MinHydraulicRadius = 0.001;
    public double MinHydraulicDepth = 0;

    /// <summary>
    /// Provides Manning's M bed resistance value.
    /// </summary>
    protected abstract double BedResistanceFormula();

    /// <summary>
    /// Determines the state variables needed for calculation of bed resistance.
    /// </summary>
    protected abstract void DetermineStateVariables(IHDReach hdReach, IHDHGridPoint hdhGridPoint);

    public BedResistanceCalculatorBase(EngineNet engineNet)
    {
      _engineNet = engineNet;
    }

    public void Add(string reachId)
    {
      var locationSpan = new LocationSpan() { ID = reachId};
      _locationSpans.Add(locationSpan);
    }

    public void Add(string reachId, double startChainage, double endChainage)
    {
      var locationSpan = new LocationSpan(reachId, startChainage, endChainage);
      _locationSpans.Add(locationSpan);
    }

    public void Initialize()
    {
      _velocityDataItem = _engineNet.HDModule.DataItems.Velocity(true, true);

      if (_locationSpans.Count == 0)
        UseAllNetwork();
      else
        PopulateLocations(_locationSpans);

      SetResistanceToConstant();

      // Make sure that initial values are determined correctly.
      ChangeBedResistance(_engineNet.EngineTime.TimeN);
    }

    private void UseAllNetwork()
    {
      var locationSpan = new LocationSpan();
      foreach (var reach in _engineNet.Reaches)
        AddLocation(reach, locationSpan);
    }

    private void PopulateLocations(List<LocationSpan> locationSpans)
    {
      foreach (var locationSpan in locationSpans)
      {
        var reachName = locationSpan.ID;
        var reaches = _engineNet.FindAllReaches(reachName);
        if (reaches == null)
          throw new Mike1DException("Cannot find a reach with ID " + reachName + " for bed resistance toolbox calculations");

        foreach (var engineReach in reaches)
          AddLocation(engineReach, locationSpan);
      }
    }

    private void AddLocation(EngineReach engineReach, LocationSpan locationsSpan)
    {
      var hdReach = _engineNet.HDModule.GetReach(engineReach);
      if (hdReach == null)
        return;

      var useAllReach = locationsSpan.StartChainage == Constants.DOUBLE_DELETE_VALUE
                     || locationsSpan.EndChainage == Constants.DOUBLE_DELETE_VALUE;

      var location = new BedResistanceLocation() { Reach = hdReach };
      // Bed resistance toolbox modifies the resistance only on the H grid points.
      foreach (var hdGridPoint in hdReach.GridPoints)
      {
        var hdhGridPoint = hdGridPoint as IHDHGridPoint;
        if (hdhGridPoint == null)
          continue;

        if (!locationsSpan.ContainsChainage(hdhGridPoint.GridPoint.Location.Chainage) && !useAllReach)
          continue;

        location.GridPoints.Add(hdhGridPoint);
      }

      if (location.GridPoints.Count > 0)
        _locations.Add(location);
    }

    private void SetResistanceToConstant()
    {
      foreach (var location in _locations)
      {
        foreach (var hdhGridPoint in location.GridPoints)
        {
          var baseCrossSection = hdhGridPoint.GridPoint.EngineCrossSection.BaseCrossSection;
          baseCrossSection.ModifiedFormulation = ResistanceFormulation.Manning_M;

          var flowResistance = baseCrossSection.FlowResistance;
          flowResistance.ResistanceDistribution = ResistanceDistribution.Constant;
          flowResistance.Formulation = ResistanceFormulation.Manning_M;
        }
      }
    }

    public void ChangeBedResistance(DateTime time)
    {
      foreach (var location in _locations)
      {
        foreach (var hdhGridPoint in location.GridPoints)
        {
          DetermineStateVariables(location.Reach, hdhGridPoint);

          var engineCrossSection = hdhGridPoint.GridPoint.EngineCrossSection;
          var flowResistance = engineCrossSection.BaseCrossSection.FlowResistance;
          flowResistance.ResistanceValue = BedResistanceFormula();
        }
      }
    }

    protected void DetermineVelocity(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      var reachIndex = hdReach.EngineReach.ReachListIndex;
      var velocityCalculator = _velocityDataItem.ReachesData[reachIndex];

      var gridPointIndex = hdhGridPoint.GridPoint.PointIndex;
      _velocity = Math.Max(Math.Abs(velocityCalculator.GetValue(gridPointIndex)), MinVelocity);
    }

    protected void DetermineHydraulicRadius(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      _waterLevel = hdhGridPoint.WaterLevelNp1;
      var engineCrossSection = hdhGridPoint.GridPoint.EngineCrossSection;
      _hydraulicRadius = Math.Max(engineCrossSection.GetHydraulicRadius(_waterLevel), MinHydraulicRadius);
    }

    protected void DetermineHydraulicDepth(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      _waterLevel = hdhGridPoint.WaterLevelNp1;
      var engineCrossSection = hdhGridPoint.GridPoint.EngineCrossSection;
      var area = engineCrossSection.GetArea(_waterLevel);
      var width = engineCrossSection.GetStorageWidth(_waterLevel);
      _hydraulicDepth = Math.Max(area/width, MinHydraulicDepth);
    }

  }

  /// <summary>
  /// Base class for bed resistance calculations using formulas with
  /// CoefficientA (a) and CoefficientB (b).
  /// </summary>
  public abstract class BedResistanceFormulaBase : BedResistanceCalculatorBase
  {
    public double CoefficientA { get; set; }
    public double CoefficientB { get; set; }
    public double MinimumResistance { get; set; }
    public double MaximumResistance { get; set; }

    public BedResistanceFormulaBase(EngineNet engineNet) : base(engineNet)
    {
      CoefficientA = 0.0;
      CoefficientB = 1.0;
      MinimumResistance = 0.0;
      MaximumResistance = Double.MaxValue;
    }

    public void SetCoefficients(double coefficientA, double coefficientB, double minimumResistance, double maximumResistance)
    {
      CoefficientA = coefficientA;
      CoefficientB = coefficientB;
      MinimumResistance = minimumResistance;
      MaximumResistance = maximumResistance;
    }

    public double LimitToMinOrMax(double bedResistance)
    {
      return Math.Max(Math.Min(bedResistance, MaximumResistance), MinimumResistance);
    }
  }

  #endregion

  #region Particular bed resistance calculator implementations

  public class FormulaVelocityHydraulicRadius : BedResistanceFormulaBase
  {
    public FormulaVelocityHydraulicRadius(EngineNet engineNet) : base(engineNet)
    {
    }

    protected override double BedResistanceFormula()
    {
      var manningN = CoefficientA * Math.Log(_velocity * _hydraulicRadius) + CoefficientB;
      return LimitToMinOrMax(1/manningN);
    }

    protected override void DetermineStateVariables(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      DetermineVelocity(hdReach, hdhGridPoint);
      DetermineHydraulicRadius(hdReach, hdhGridPoint);
    }
  }

  public class FormulaHydraulicDepth : BedResistanceFormulaBase
  {
    public FormulaHydraulicDepth(EngineNet engineNet) : base(engineNet)
    {
    }

    protected override double BedResistanceFormula()
    {
      var manningN = CoefficientA * Math.Pow(_hydraulicDepth, CoefficientB);
      return LimitToMinOrMax(1/manningN);
    }

    protected override void DetermineStateVariables(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      DetermineHydraulicDepth(hdReach, hdhGridPoint);
    }
  }

  public class FormulaVelocity : BedResistanceFormulaBase
  {
    public FormulaVelocity(EngineNet engineNet) : base(engineNet)
    {
    }

    protected override double BedResistanceFormula()
    {
      var manningN = CoefficientA * Math.Pow(_velocity, CoefficientB);
      return LimitToMinOrMax(1/manningN);
    }

    protected override void DetermineStateVariables(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      DetermineVelocity(hdReach, hdhGridPoint);
    }
  }

  public class TableBedResistance : BedResistanceCalculatorBase
  {
    public IXYTable VelocityResistanceTable { get; set; }

    public TableBedResistance(EngineNet engineNet) : base(engineNet)
    {
    }

    public void SetTable(double[] velocityValues, double[] resistanceValues)
    {
      VelocityResistanceTable = new XYTable(velocityValues, resistanceValues);
    }

    protected override double BedResistanceFormula()
    {
      return VelocityResistanceTable.YFromX(_velocity);
    }

    protected override void DetermineStateVariables(IHDReach hdReach, IHDHGridPoint hdhGridPoint)
    {
      DetermineVelocity(hdReach, hdhGridPoint);
    }
  }

  #endregion

}
