using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHI.Mike1D.Generic;
using DHI.Mike1D.HDParameterDataAccess;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{

  /// <summary>
  /// Script for setting initial water level/depth values
  /// </summary>
  public class InitialWaterLevel
  {
    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void SetInitialWaterLevelDepthValues(Mike1DData mike1DData)
    {
      // Set water level initial value for node "90"
      mike1DData.HDParameters.InitialConditions.WaterLevelDepth.AddValue("90", new HDWaterLevelDepth(true, 196.3));
      // Set water depth initial value for node "91"
      mike1DData.HDParameters.InitialConditions.WaterLevelDepth.AddValue("91", new HDWaterLevelDepth(false, 0.4));
      
      // Set constant water depth initial value for entire reach "90l1"
      mike1DData.HDParameters.InitialConditions.WaterLevelDepth.AddGlobalReachValue("90l1", new HDWaterLevelDepth(false, 0.4));

      // Set varying water depth initial value on upstream/downstream end (chainage 0 and 47.95 respectively of reach "91l1"
      mike1DData.HDParameters.InitialConditions.WaterLevelDepth.AddValue(new Location("91l1", 0), new HDWaterLevelDepth(false, 0.4));
      mike1DData.HDParameters.InitialConditions.WaterLevelDepth.AddValue(new Location("91l1", 47.95), new HDWaterLevelDepth(false, 0.6));
    }

  }
}
