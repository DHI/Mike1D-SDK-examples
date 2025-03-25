using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.NetworkDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  public class SetupParameters
  {

    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void SetHDParameter(Mike1DData mike1DData)
    {
      // A subsete of the HD parameters are listed here as examples of what can be set
      // in the MIKE 1D engine. For a complete list, consult the MIKE 1D API documentation.
      // The MIKE 1D API documentation also contains more detailed documentation for
      // some of the parameters.

      // The velocity distribution coefficient used in the convective acceleration
      // term of the momentum equation.
      // Default: 1
      // M11 default = 1, MU default = 1.1
      mike1DData.HDParameters.SolverSettings.Alpha = 1.1;

      // Sink momentum correction term. When set to 1, a sink will also
      // remove a similar amount of momentum, and hence remove strange
      // artifacts around sink locations. Use 0 to disable the term.
      // Default: 1
      // Value must be between zero and one.
      mike1DData.HDParameters.SolverSettings.SinkMomentumCorrection = 0;

      // The network engine iterates on each time step to solve the equation system with coefficients
      // dependent on qauntities evaluated betweeen time step n and time step n+1. 
      // NumberOfTimeStepIterations limits the number of iterations that may be performed.
      // A value of zero means that no iteration takes place, first values at time n+1 is used.
      // Default: 1
      // M11 default 1, MU default 0 (no iteration)
      mike1DData.HDParameters.SolverSettings.NumberOfTimeStepIterations = 1;

      // Flag specifying whether the QZero method is activated.
      // The QZeroMethod makes sure that the flow goes towards zero 
      // when the river/link is drying out.
      // Default: true.
      // M11 default true, MU default false
      mike1DData.HDParameters.SolverSettings.UseQZeroMethod = true;

      // The Delh factor controls several things:
      // - The height of the artificial bottom 'slot', introduced to a 
      //   cross section to handling 'drying out' of the section.
      // - The start depth of the zero flow method.
      // Default: 0.1
      // Unit: [m]
      mike1DData.HDParameters.SolverSettings.Delh = 0.02;

      // Relative version of Delh. For closed cross section
      // the value of delh is the smallest of Delh and
      // DelhRelative * cross section height.
      // Default value: 0.01
      mike1DData.HDParameters.SolverSettings.DelhRelative = 0.02;

      // Minimum water depth in a node/gridpoint. After every timestep
      // the water level is corrected to be at least the minimum water depth.
      //
      // For closed cross sections (circular, egg-shaped and o-shaped (MU circular types), rectangular etc.)
      // the minimum water depth is the smallest of this value and 
      // MinWaterDepthRelative x cross section height (diameter).
      // This must be less than Delh.
      // In M11 type setups this is set negative -1, effectively disabling this parameter 
      // and letting the slot functionality fully control the minimum water depth, and 
      // allowing water levels in the slot area.
      // M11 default: -1, MU default: 0.005
      mike1DData.HDParameters.SolverSettings.MinWaterDepth = 0.002;

      // Relative version of MinWaterDepth, in case of closed cross sections
      // the actual minimum water depth is the smallest of the MinWaterDepth and
      // MinWaterDepthRelative * cross section height
      // This must be less than DelhRelative.
      // Default value: 0.005
      mike1DData.HDParameters.SolverSettings.MinWaterDepthRelative = 0.002;

      // Time in seconds for non-return valves to fully open after having closed.
      // Default value: 60
      mike1DData.HDParameters.SolverSettings.NonReturnValveReopenTimeInSeconds = 10;


    }

    /// <summary>
    /// Script to update maximum number of grid points
    /// in a link to no more than 7 grid points.
    /// 
    /// This only works if there are reach-global cross sections
    /// (urban like setup) or there are very few cross sections.
    /// When cross sections are specified along the reach,
    /// there will always be grid points at cross section locations,
    /// regardless of the value of MaximumDx.
    /// </summary>
    [Script]
    public void UpdateMaxDx(Mike1DData mike1DData)
    {
      int maxNumGridPoints = 7;

      foreach (IReach reach in mike1DData.Network.Reaches)
      {
        if (System.Math.Round(reach.LocationSpan.Length() / reach.MaximumDx) > maxNumGridPoints)
          // Add 1e-3 to avoid rounding error issues.
          reach.MaximumDx = reach.LocationSpan.Length() / maxNumGridPoints + 1e-3;
      }
    }
  }
}
