using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace Scripts
{
  /// <summary>
  /// Enables simple statistics and accumulated time series calculations.
  /// </summary>
  public class SimpleStatistics
  {
    private ResultSpecification _resultSpec;
    private Mike1DData _mike1DData;

    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void Initialize(Mike1DData mike1DData)
    {
      _mike1DData = mike1DData;

      AddAccumulatedTimeSeries();
      AddStatistics();
    }

    private void AddAccumulatedTimeSeries()
    {
      // Find HD result specification
      _resultSpec = _mike1DData.ResultSpecifications.Find(x => x.ID == "DefaultHDResults");
      // Accumulated values of discharge
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.TimeIntegrate));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.TimeIntegratePositive));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.TimeIntegrateNegative));
    }

    private void AddStatistics()
    {
      // Add simple statistics for default HD results
      _resultSpec = _mike1DData.ResultSpecifications.Find(x => x.ID == "DefaultHDResults");
      _resultSpec = CreateStatisticsResultSpecification(_resultSpec, ResultTypes.HD);
      _mike1DData.ResultSpecifications.Add(_resultSpec);

      // Water level
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.WaterLevel, DerivedQuantityType.Max));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.WaterLevel, DerivedQuantityType.MaxTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.WaterLevel, DerivedQuantityType.MinTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.WaterLevel, DerivedQuantityType.Min));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.WaterLevel, DerivedQuantityType.Average));

      // Discharge
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.TimeIntegrate));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.Max));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.MaxTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.MinTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.Min));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.Discharge, DerivedQuantityType.Average));

      // Flow velocity
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.FlowVelocity, DerivedQuantityType.Max));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.FlowVelocity, DerivedQuantityType.MaxTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.FlowVelocity, DerivedQuantityType.MinTime));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.FlowVelocity, DerivedQuantityType.Min));
      AddQuantity(DerivedQuantity.Create(PredefinedQuantity.FlowVelocity, DerivedQuantityType.Average));
    }

    private ResultSpecification CreateStatisticsResultSpecification(ResultSpecification resultSpec, ResultTypes resultType)
    {
      var fileName = resultSpec.Connection.FilePath.Path.Replace(".res1d", "Stat.res1d");

      var conn = new Connection
      {
        BridgeName = "res1d",
        FilePath = {Path = fileName}
      };

      var statisticsResultSpec = new ResultSpecification
      {
        ID = "HDStatistics",
        StoringFrequency = 1,
        StoringFrequencyType = StoringFrequencyUnitTypes.PerTimeStep,
        Connection = conn,
        Mode = ResultSpecification.FileMode.Overwrite,
        ResultType = resultType,
        StartTime = _mike1DData.SimulationEnd,
        EndTime = _mike1DData.SimulationEnd
      };

      return statisticsResultSpec;
    }

    private void AddQuantity(IQuantity quantity)
    {
      _resultSpec.What.Add(quantity);
      _mike1DData.HDParameters.AdditionalOutput.Quantities.Add(quantity);
    }

  }

}
