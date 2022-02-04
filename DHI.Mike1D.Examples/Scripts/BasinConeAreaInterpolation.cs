using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.NetworkDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  public class BasinConeAreaInterpolation
  {
    /// <summary>
    /// Setup all Basin to interpolate the area between level assuming
    /// the basin has a conical shape, and interpolation is on
    /// cone radii instead of cone area.
    /// </summary>
    /// <param name="mike1DData"></param>
    [Script]
    public void AddAdditionalParameter(Mike1DData mike1DData)
    {
      if (!mike1DData.UseHD)
        return;

      foreach (INode node in mike1DData.Network.Nodes)
      {
        IBasin basin = node as IBasin;
        if (basin == null) continue;

        basin.Geometry.AreaInterpolation = false;
      }
    }
  }
}
