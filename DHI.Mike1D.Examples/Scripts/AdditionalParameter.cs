using System;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script showing how to add an additional parameter to the MIKE 1D data object
  /// </summary>
  public class AdditionalParameter
  {
    /// <summary>
    /// Add additional parameter
    /// </summary>
    /// <param name="mike1DData"></param>
    [Script]
    public void AddAdditionalParameter(Mike1DData mike1DData)
    {
      mike1DData.AdditionalData.Add("AllowPMMultipleTailNodes", true);
    }
  }
}
