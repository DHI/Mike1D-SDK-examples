using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using IMike1DPlugin = DHI.Mike1D.Plugins.IMike1DPlugin;

namespace DHI.Mike1D.Examples.Plugins
{
  /// <summary>
  /// Plugin for disabling writing of the additional NAM results file.
  /// </summary>
  public class DisableOutput : IMike1DPlugin
  {

    public void Initialize(IList<Mike1DPluginArgument> arguments, Mike1DData mike1DData)
    {
      mike1DData.ResultSpecifications.RemoveAll(rs => rs.ID == "AdditionalNAMRRResults");
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
