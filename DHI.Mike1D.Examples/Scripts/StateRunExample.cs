using System;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.ModelState;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{

  /// <summary>
  /// Example of scripts for working with state files.
  /// A state file is a file containing the state of the engine at one point in time.
  /// </summary>
  public class StateRunExample
  {
    /// <summary>
    /// Modify start of simulation with some minutes.
    /// The startTimeOffset can be enabled through the command line by
    ///   -scriptpars:startTimeOffset=180
    /// </summary>
    [Script(Order = 1)] // Make sure this method is called first (Order = 1)
    public void ModifySimulationStart(Mike1DData mike1DData, int startTimeOffset = 0)
    {
      if (startTimeOffset != 0)
        mike1DData.SimulationStart = mike1DData.SimulationStart.AddMinutes(startTimeOffset);
    }

    /// <summary>
    /// Setup state output to write out state every 60 minutes for the first 6 hours of the simulation.
    /// A new state file will be created for each state output.
    /// </summary>
    [Script]
    public void SetStateOutput(Mike1DData mike1DData, bool atEndOfSimulation = true)
    {
      // State file name template
      FilePath stateFilePath = mike1DData.Connection.FilePath.Clone();
      stateFilePath.Extension = ".sta1d";
      stateFilePath.FileNameWithoutExtension += "-{0}";

      if (atEndOfSimulation)
      {
        // Write out state at end of simulation: leave TimeStep at zero
        mike1DData.StateWriteSpecification.StartTime = mike1DData.SimulationEnd;
        mike1DData.StateWriteSpecification.TemplateFilePath = stateFilePath;
      }
      else
      {
        // Write out state every 60 minutes for the first 600 minutes.
        mike1DData.StateWriteSpecification.StartTime = mike1DData.SimulationStart.AddMinutes(60);
        // If you only want to output state at one time, set TimeStep to Zero (delete the next two lines, TimeStep is by default zero).
        mike1DData.StateWriteSpecification.EndTime = mike1DData.SimulationStart.AddMinutes(600);
        mike1DData.StateWriteSpecification.TimeStep = TimeSpan.FromMinutes(60);
        mike1DData.StateWriteSpecification.TemplateFilePath = stateFilePath;
      }
    }

    /// <summary>
    /// Setup state initial conditions, assuming there is a file with that name.
    /// </summary>
    [Script]
    public void SetStateInitialConditions(Mike1DData mike1DData, IDiagnostics diagnostics)
    {
      // State file name template
      FilePath stateFilePath = mike1DData.Connection.FilePath.Clone();
      stateFilePath.Extension = ".sta1d";
      stateFilePath.FileNameWithoutExtension += "-{0}";

      // DateTime of state to use. Here using mike1DData.SimulationStart;
      DateTime startTime = mike1DData.SimulationStart;
      stateFilePath.Path = string.Format(stateFilePath.Path, startTime.ToString("yyyyMMddTHHmmss"));

      // If state file exists, use it
      if (System.IO.File.Exists(stateFilePath.FullFilePath))
      {
        StatestartInfo statestartInfo = new StatestartInfo(stateFilePath);
        mike1DData.StatestartInfos.Add(statestartInfo);
        diagnostics.Info("Using state file: "+stateFilePath.Path);
      }
      else
      {
        diagnostics.Info("Could not find any state file named: " + stateFilePath.Path);
      }

    }
  }
}
