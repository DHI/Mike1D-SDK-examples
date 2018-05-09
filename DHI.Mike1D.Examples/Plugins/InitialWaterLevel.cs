using System;
using System.Collections.Generic;
using System.Globalization;
using DHI.Mike1D.Generic;
using DHI.Mike1D.HDParameterDataAccess;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using IMike1DPlugin = DHI.Mike1D.Plugins.IMike1DPlugin;

namespace DHI.Mike1D.Examples.Plugins
{
  /// <summary>
  /// Plugin for adding parameter initial water level conditions
  /// <para>
  /// The plugin takes the following parameters:
  /// <code>
  ///    Node = '&lt;nodeId&gt;;&lt;wl&gt;'
  ///    Location = '&lt;reachId&gt;;&lt;chainage&gt;;&lt;wl&gt;'
  ///    Span = '&lt;reachId&gt;;&lt;startChainage&gt;;&lt;endChainage&gt;;&lt;wl&gt;'
  /// </code>
  /// </para>
  /// </summary>
  /// <example>
  /// Plugin is loaded through a .m1da file, as:
  /// <code>
  ///      [Plugin]
  ///         AssemblyName = 'DHI.Mike1D.Examples.dll'
  ///         ClassName = 'DHI.Mike1D.Examples.Plugins.InitialWaterLevel'
  ///         [Arguments]
  ///            Node = '90;196.3'
  ///            Location = '90l1;57.2876;196.4'
  ///            Span = '90l1;50;120;196.4'
  ///
  ///         EndSect  // Arguments
  ///
  ///      EndSect  // Plugin
  /// </code>
  /// </example>
  public class InitialWaterLevel : IMike1DPlugin
  {
    public void Initialize(IList<Mike1DPluginArgument> arguments, Mike1DData mike1DData)
    {
      InitialWaterLevel initialWaterLevel = new InitialWaterLevel();
      NetworkDataInterp<HDWaterLevelDepth> initialWl = mike1DData.HDParameters.InitialConditions.WaterLevelDepth;

      for (int i = 0; i < arguments.Count; i++)
      {
        Mike1DPluginArgument argument = arguments[i];
        if (StringComparer.OrdinalIgnoreCase.Equals("Node", argument.Key))
        {
          string[] split = StringAlgorithms.SplitQuoted(argument.Value, ';', '"');
          if (split.Length == 2 || split.Length == 3)
          {
            string nodeId = split[0];
            if (string.IsNullOrEmpty(nodeId))
              continue;
            double wl;
            if (!double.TryParse(split[1], NumberStyles.Any, CultureInfo.InvariantCulture, out wl))
              continue;
            // Depth or level based value - by default level
            bool depth = (split.Length == 3 && StringComparer.OrdinalIgnoreCase.Equals("depth", split[2]));

            initialWl.AddValue(nodeId, new HDWaterLevelDepth(!depth, wl));
          }
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("Location", argument.Key))
        {
          string[] split = StringAlgorithms.SplitQuoted(argument.Value, ';', '"');
          if (split.Length == 3 || split.Length == 4)
          {
            string reachId = split[0];
            if (string.IsNullOrEmpty(reachId))
              continue;
            double chainage;
            if (!double.TryParse(split[1], NumberStyles.Any, CultureInfo.InvariantCulture, out chainage))
              continue;
            double wl;
            if (!double.TryParse(split[2], NumberStyles.Any, CultureInfo.InvariantCulture, out wl))
              continue;
            // Depth or level based value - by default level
            bool depth = (split.Length == 4 && StringComparer.OrdinalIgnoreCase.Equals("depth", split[3]));

            initialWl.AddValue(new Location(reachId, chainage), new HDWaterLevelDepth(!depth, wl));
          }
        }
        else if (StringComparer.OrdinalIgnoreCase.Equals("Span", argument.Key))
        {
          string[] split = StringAlgorithms.SplitQuoted(argument.Value, ';', '"');
          if (split.Length == 4 || split.Length == 5)
          {
            string reachId = split[0];
            if (string.IsNullOrEmpty(reachId))
              continue;
            double chainage;
            if (!double.TryParse(split[1], NumberStyles.Any, CultureInfo.InvariantCulture, out chainage))
              continue;
            double chainage2;
            if (!double.TryParse(split[2], NumberStyles.Any, CultureInfo.InvariantCulture, out chainage2))
              continue;
            double wl;
            if (!double.TryParse(split[3], NumberStyles.Any, CultureInfo.InvariantCulture, out wl))
              continue;
            // Depth or level based value - by default level
            bool depth = (split.Length == 5 && StringComparer.OrdinalIgnoreCase.Equals("depth", split[4]));

            initialWl.AddValue(new LocationSpan(reachId, chainage, chainage2), new HDWaterLevelDepth(!depth, wl));
          }
        }
      }

    }

    #region Deprecated implementation

    public void Initialize(IList<Mike1DPluginArgument> arguments)
    {
      throw new NotImplementedException();
    }

    public void OnSetupLoaded(Mike1DData mike1DData)
    {
      throw new NotImplementedException();
    }

    #endregion

  }
}
