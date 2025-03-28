using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script showing how parameters can be modified from the command line
  /// </summary>

  public class ScriptParameters
  {
    /// <summary>
    /// Script method containing three additional parameters, which can be
    /// assigned from the command line
    /// 
    /// The diagnostics object is automatically assigned.
    /// 
    /// The two other parameters can be assigned from the command line using
    /// the command line argument <code>-ScriptPars:</code>, in this case:
    /// <code>
    /// -ScriptPars:addWaterDepth=true;alpha=1.5;
    /// </code>
    ///
    /// By specifying a default value to the arguments in the method definition here, it not required to
    /// assign a value in the <code>-ScriptPars:</code> to those argument. Hence this script will run
    /// without the <code>-ScriptPars:</code> definition, using the default values.
    ///
    /// If not specifying a default value to all arguments here, those argument must always be
    /// indluded in the <code>-ScriptPars:</code>.
    /// </summary>
    /// <param name="mike1DData">MIKE 1D data object, must be present in the argument list</param>
    /// <param name="diagnostics">If adding a diagnostics object to the argument list,
    ///                           it can be used to put messages to the MIKE 1D setup log file</param>
    /// <param name="addWaterDepth">User defined parameter, whether to output water level or not. </param>
    /// <param name="alpha">User defined parameter, the value of the alpha parameter</param>
    [Script]
    public void ModifySetup(Mike1DData mike1DData, IDiagnostics diagnostics, bool addWaterDepth = false, double alpha = 1.0)
    {
      // Use diagnostics object to add to setup log file
      diagnostics.Info(string.Format("Modifying setup, adding depth: {0}", addWaterDepth));

      // Add Water Depth to default HD result output
      ResultSpecification hdResSpec = mike1DData.ResultSpecifications.Find(rs => rs.ID == "DefaultHDResults");
      if (hdResSpec != null)
      {
        if (addWaterDepth)
          hdResSpec.What.Add(Quantity.Create(PredefinedQuantity.WaterDepth));
      }

      // Change alpha parameter
      mike1DData.HDParameters.SolverSettings.Alpha = alpha;

    }

  }
}
