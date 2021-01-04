using System;
using System.Collections.Generic;
using System.Linq;
using DHI.Amelia.DataModule.Services.DataSource.DataTypes;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;

namespace DHI.Mike1D.AmeliaBridge
{

  /// <summary>
  /// A MU script can read data from the MU/M+ database. 
  /// The script is executed after all data have been exported to MIKE 1D,
  /// but before the connection to the database is closed. Hence, it is possible
  /// to modify and add to the MIKE 1D data object, based on data in the MU/M+ database.
  /// <para>
  /// This is not part of the example .csproj file, because it requires additional 
  /// dependencies to the MU/M+ system (DHI.Amelia), and these are not available unless
  /// MU/M+ is installed.
  /// </para>
  /// </summary>
  public class MURRScript
  {

    /// <summary>
    /// Scenario: The MU database contains RDI catchments. However, for each RDI catchment 
    ///           we want to add a Model A catchment to handle impervious runoff. 
    ///
    /// This script assumes that the table msm_Catchment has two User Defined Columns defined:
    ///  - "ModelAParameterSet" of type string
    ///  - "ImpervFraction" of type double
    /// If "ModelAParameterSet" has a value set, it contains the Model A parameter set Id to
    /// apply to the Model A catchment. The "ImpervFraction" column contains the fraction of
    /// imperviousness, i.e. the area fraction of the catchment to use for the Model A catchment. 
    /// </summary>
    /// <param name="bridge">Amelia bridge, loading the data</param>
    /// <param name="mike1DData">MIKE 1D data object</param>
    /// <param name="diagnostics">Diagnostics object for error reporting</param>
    [Script]
    public void AddModelACatchments(AmeliaBridge bridge, Mike1DData mike1DData, IDiagnostics diagnostics)
    {
      // Load all msm_Catchment rows, and load also User Defined Columnes (attachUserDefinedFields: true). Sort them by Muid.
      IEnumerable<msm_Catchment> catchments = bridge.DataSource.GetAllRows<msm_Catchment>(attachUserDefinedFields: true);
      catchments = catchments.OrderBy(catchment => catchment.Muid, StringComparer.OrdinalIgnoreCase);

      // Get a DataFactoryRainfallRunoff, which is required for easing the definition of catchments.
      DataFactoryRainfallRunoff rrDataFactory = bridge.CreateRainfallDataFactory();

      foreach (msm_Catchment msmCatchment in catchments)
      {
        object val;
        // Check if this catchment has "ModelAParameterSet" defined 
        if (msmCatchment.UserDefinedFieldsData.TryGetValue("ModelAParameterSet", out val))
        {
          // Convert val to string and also load the impervious fraction
          string modelAParId = (string)val;
          double impervFraction = (double)msmCatchment.UserDefinedFieldsData["ImpervFraction"];
          // Retrieve Model A parameter set matching "ModelAParameterSet" column value.
          msm_HParA catchmentParameters = bridge.DataSource.Get<msm_HParA>(modelAParId);
          // Create a Model A catchment
          ICatchmentTimeAreaData cta = rrDataFactory.PopulateRunoffA(msmCatchment, catchmentParameters, (1-impervFraction) * msmCatchment.Area.GetValueOrDefault());
          // Give the catchment a new name, so it does not collide with the original catchment model.
          cta.ModelId += "-Imperv";
          // Add the new Model A catchment to MIKE 1D.
          mike1DData.RainfallRunoffData.Catchments.Add((ICatchment)cta);
        }
      }
    }
  }
}