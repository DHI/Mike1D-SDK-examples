/*
 * Script to clean up a catchment buffer. This script fixes an issue for
 * some setups, where the memory keeps on growing.
 */

using System;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;

namespace DHI.Mike1D.Examples.Scripts
{

  public class CatchmentBufferCleanup
  {
    IMike1DController _controller;

    /// <summary>
    /// This script is executed when simulation is starting 
    /// </summary>
    [Script]
    public void SetupCleanup(IMike1DController controller, bool cleanup=true)
    {
      _controller = controller;
      if (cleanup)
      {
        // Register for the ControllerEvent
        controller.ControllerEvent += OnControllerEvent;
        Console.Out.WriteLine("Registering cleanup-1");
      }
    }

    /// <summary>
    /// This method is called whenever the controller changes state.
    /// It register the <see cref="CcCleaner"/> class to the PostTimeStepEvent
    /// of all catchments.
    /// </summary>
    private void OnControllerEvent(object sender, ControllerEventArgs e)
    {
      // Covers can be updated when the model has been prepared.
      if (e.State == ControllerState.Prepared)
      {
        // Get the controller
        Mike1DController controller = (Mike1DController)sender;
        //controller.EngineNet.PostTimeStepEvent += PostTimestep;

        foreach (ICatchment icatchment in controller.Mike1DData.RainfallRunoffData.Catchments)
        {
          Catchment catchment = icatchment as Catchment;
          if (catchment != null)
          {
            var cleaner = new CcCleaner(catchment);
            catchment.PostTimeStepEvent += cleaner.Cleanup;
          }
        }

      }
    }

    //int _maxBufferLength = 0;
    //private void PostTimestep(DateTime time)
    //{
    //  int curBufferLength = _maxBufferLength;
    //  foreach (ICatchment icatchment in _controller.Mike1DData.RainfallRunoffData.Catchments)
    //  {
    //    Catchment catchment = icatchment as Catchment;
    //    if (catchment != null)
    //    {
    //      int length = (catchment.ValueBuffer.Times as FifoQueue<DateTime>).Capacity;
    //      if (length > curBufferLength)
    //        curBufferLength = length;
    //    }
    //  }
    //  if (curBufferLength > _maxBufferLength)
    //  {
    //    _maxBufferLength = curBufferLength;
    //    Console.Out.WriteLine("Buffers extended: {0} : {1}", _controller.EngineNet.EngineTime.NumberOfTimeSteps, _maxBufferLength);
    //  }
    //}


    class CcCleaner
    {
      private Catchment _catchment;

      public CcCleaner(Catchment catchment)
      {
        this._catchment = catchment;
      }

      public void Cleanup(DateTime time)
      {
        _catchment.ValueBuffer.CleanupBefore(_catchment.MinTime);
      }
    }

  }

}
