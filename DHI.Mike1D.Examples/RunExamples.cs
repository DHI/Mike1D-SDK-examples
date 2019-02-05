using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.ResultDataAccess;
using DHI.Mike1D.SimLauncher;

namespace DHI.Mike1D.Examples
{
  /// <summary>
  /// Examples of how to run the MIKE 1D engine in code
  /// </summary>
  public class RunExamples
  {

    static RunExamples()
    {
      // The setup method will make your application find the MIKE assemblies at runtime.
      // The first call of the setup method takes precedense. Any subsequent calls will be ignored.
      // It must be called BEFORE any method using MIKE libraries is called, i.e. it is not sufficient
      // to call it as the first thing in that method using the MIKE libraries. Often this can be achieved
      // by having this code in the static constructor.
      if (!DHI.Mike.Install.MikeImport.Setup(17, DHI.Mike.Install.MikeProducts.Mike1D))
        throw new Exception("Could not find a MIKE installation");
    }

    /// <summary>
    /// Using the SimulationWorker to run a simulation
    /// <para>
    /// The worker takes care of error messages, summary files etc,
    /// and you get exactly the same as if running the simulation from the
    /// MIKE HYDRO, MIKE URBAN or MIKE 11 user interfaces or by using the
    /// DHI.Mike1D.Application.exe.
    /// </para>
    /// </summary>
    /// <param name="setupFilepath">Path to setup file (.sim11, .mdb or .m1dx)</param>
    public static void WorkerRun(string setupFilepath)
    {
      SimulationWorker worker = new SimulationWorker(new FilePath(setupFilepath))
        {
          SimulationParameters = new SimulationParameters()
        };
      // Load setup
      worker.Load();
      // Now the setup is available
      Mike1DData mike1DData = worker.Mike1DData;
      // Run simulation
      worker.Run();
    }

    
    /// <summary>
    /// Using the MIKE 1D controller to run a simulation
    /// <para>
    /// The controller gives more control of the simulation.
    /// It also pushes the responsibility of error reporting and
    /// summary information to the user.
    /// </para>
    /// </summary>
    /// <param name="setupFilepath">Path to setup file (.sim11, .mdb or .m1dx)</param>
    public static void ControllerRun(string setupFilepath)
    {
      // The controller factory 
      Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
      IMike1DController controller = null;

      // Try-catch to catch any unexpected exception or runtime exceptions.
      try
      {
        // Diagnostics object receiving errors, warning and hints during 
        // load and initialize.
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");

        // creates a new Mike 1D controller and load the setup
        controller = controllerFactory.OpenAndCreate(Connection.Create(setupFilepath), diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Loading errors, aborting");

        // Now the MIKE 1D data is available
        Mike1DData mike1DData = controller.Mike1DData;

        // Validate setup, returns a new diagnostics object
        IDiagnostics validated = controller.Validate();
        if (validated.ErrorCountRecursive > 0)
          throw new Exception("Validation errors, aborting");

        // Initialize simulation
        controller.Initialize(diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Initialization errors, aborting");

        // Run the simulation
        controller.Prepare();
        controller.Run();
        controller.Finish();

      }
      catch (Exception e)
      {
        // Write exception to log file
        if (controllerFactory.LogFileWriter != null)
          controllerFactory.LogFileWriter.ExceptionToLogFile(e);
        // Call Finish, which should make sure to close down properly, and release any licences.
        if (controller != null)
        {
          try { controller.Finish(); }
          catch { }
        }
        // Rethrow exception
        throw;
      }
    }


  }
}
