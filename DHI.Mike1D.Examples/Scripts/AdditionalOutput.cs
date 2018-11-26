using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script for adding additional quantities to an output file
  /// </summary>
  public class AdditionalOutput
  {
    /// <summary>
    /// Method called when Mike1DData object has been loaded.
    /// </summary>
    [Script]
    public void AddToOutput(Mike1DData mike1DData)
    {

      // Add Water Depth to default HD result output
      ResultSpecification hdResSpec = mike1DData.ResultSpecifications.Find(rs => rs.ID == "DefaultHDResults");
      if (hdResSpec != null)
      {
        hdResSpec.What.Add(Quantity.Create(PredefinedQuantity.WaterDepth));
      }
    }

  }
}
