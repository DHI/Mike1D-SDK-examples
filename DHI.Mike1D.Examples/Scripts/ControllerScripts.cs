using System;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  public class ControllerScripts
  {
    /// <summary>
    /// Set maximum number of threads for a HD simulation
    /// </summary>
    [Script]
    public void SetMaxNumberOfThreads(IMike1DController controller)
    {
      Mike1DController hdController = controller as Mike1DController;
      if (hdController != null)
        hdController.MaxNumberOfThreadsUser = 1;
    }
  }
}
