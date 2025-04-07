using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.NetworkDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Sets the start level of basin covers to the default value
  /// used in version 2025 and earlier.
  /// </summary>
  public class SetBasinCoverStartLevel
  {
    private double _coverStartLevel = -0.25;
    
    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void Initialize(Mike1DData mike1DData)
    {
      var nodes = mike1DData.Network.Nodes;

      foreach (var node in nodes)
      {
        if (node is Basin basin && basin.Cover is INormalCover)
        {
          double groundLevel = basin.GroundLevel;
          basin.Cover.WaterLevelStart = groundLevel + _coverStartLevel;
        }
      }
    }
  }
}
