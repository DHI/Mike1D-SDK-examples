using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace Scripts
{
  /// <summary>
  /// A script, which allows to perform Long Term Statistics (LTS) computations for ordinary HD simulation.
  /// <para>
  /// The idea is that one prepares an LTS simulation, which has LTS result specifications.
  /// Then by removing the use of Mike1DControllerLTS (set the flag <see cref="Mike1DData.UseHDLongTermStatistics"/> to false)
  /// a conventional Mike1DController is used, but with LTSModule present, which performs statistics calculations on
  /// ordinary HD simulation results.
  /// </para>
  /// <para>
  /// The LTSModule will be enabled if there are LTS result specifications in Mike1DData.
  /// </para>
  /// </summary>
  public class LongTermStatisticsControllerRemover
  {
    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void Initialize(Mike1DData mike1DData)
    {
      mike1DData.UseHDLongTermStatistics = false;

      // Add LTS result specifications to ordinary MIKE 1D result specifications
      foreach (var resultSpecificationLTS in mike1DData.LongTermStatistics.ResultSpecifications)
        mike1DData.ResultSpecifications.Add(resultSpecificationLTS);
    }
  }
}
