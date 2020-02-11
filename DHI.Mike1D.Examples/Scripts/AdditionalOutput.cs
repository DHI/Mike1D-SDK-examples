using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DHI.Mike1D.ADDataAccess;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script for adding additional outputs.
  /// <para>
  /// If you want to disable one or more of the script methods in the class,
  /// you can delete that method, or you can just comment out the [Script] attribute line.
  /// </para>
  /// </summary>
  public class AdditionalOutput
  {
    /// <summary>
    /// Add water depth output to default HD result file
    /// <para>
    /// Method called when Mike1DData object has been loaded.
    /// </para>
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

    /// <summary>
    /// Set flag such that results are written at exact time matching output interval specification.
    /// When output time does not match engine time, interpolation in time is applied.
    /// <para>
    /// This is relevant when running with adaptive or tabulated time stepping.
    /// </para>
    /// </summary>
    [Script]
    public void SetInterpolationFlag(Mike1DData mike1DData)
    {
      foreach (ResultSpecification resSpec in mike1DData.ResultSpecifications)
      {
        resSpec.Interpolate = true;
      }
    }

    /// <summary>
    /// Disable various outputs, based on their output id.
    /// </summary>
    [Script]
    public void DisableOutput(Mike1DData mike1DData)
    {
      // Remove default RR results
      mike1DData.ResultSpecifications.RemoveAll(item => item.ID == "DefaultRRResults");
      // Remove additional NAM RR results
      mike1DData.ResultSpecifications.RemoveAll(item => item.ID == "AdditionalNAMRRResults");
      // Remove Catchment Discharge results
      mike1DData.ResultSpecifications.RemoveAll(item => item.ID == "CatchmentDischargeRRResults");
    }

    /// <summary>
    /// Make a user defined output file that outputs mass transport in a specified set of links and weirs
    /// <para>
    /// Method called when Mike1DData object has been loaded.
    /// </para>
    /// </summary>
    [Script]
    public void AddADUserOutput(Mike1DData mike1DData, IDiagnostics diagnostics)
    {
      ResultSpecification adUserResSpec = mike1DData.GetDefaultResultSpecification(ResultSpecificationTypes.AD);
      adUserResSpec.ID = "ADUserOutput";

      // How often to store
      adUserResSpec.StoringFrequencyType = StoringFrequencyUnitTypes.Minutes;
      adUserResSpec.StoringFrequency = 15;

      // Where to store the file
      adUserResSpec.Connection.FilePath = new FilePath(mike1DData.Id + mike1DData.ScenarioId + "UserAD.res1d");

      // What to store
      ADComponent adComp = mike1DData.ADComponentData.Components.Find(comp => comp.Id == "Spill");
      if (adComp == null)
      {
        diagnostics.Error(new DiagnosticItem("Could not find AD component with Id: Spill"));
        return;
      }
      // Mass transport of "Spill" AD component
      // IQuantity massTransport = ADComponentQuantities.FaceTransport(adComp.Quantity);                // For Release 2019 (17.0), this works
      IQuantity massTransport = ADComponentQuantities.Create(adComp, PredefinedQuantityAD.Transport);   // For later releases (>17.0), this is recommended
      adUserResSpec.What.Add(massTransport);

      // Which data in the network to store
      var spatialFilter = new Res1DNetworkFilter();
      // Links, output at all grid points
      spatialFilter.AddGlobalReachValue("Link_1", true);
      spatialFilter.AddGlobalReachValue("Link_2", true);
      // Links, output at inflow to link only
      spatialFilter.AddGlobalReachValue("Link_3", Res1DSpatialFilterTypeEnum.First);
      spatialFilter.AddGlobalReachValue("Link_4", Res1DSpatialFilterTypeEnum.First);
      // Weirs
      spatialFilter.AddGlobalReachValue("Weir:Weir_1", true);
      spatialFilter.AddGlobalReachValue("Weir:Weir_2", true);
      // Set filter for specific quantity or for all quantities
      //adUserResSpec.SetFilter(massTransport, spatialFilter);
      adUserResSpec.DefaultFilter = spatialFilter;

      // Add to list of result files
      mike1DData.ResultSpecifications.Add(adUserResSpec);
    }

  }
}
