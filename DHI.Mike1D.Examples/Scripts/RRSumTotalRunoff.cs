using System;
using System.Globalization;
using System.IO;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Script class that makes a catchment that sums Total Runoff
  /// for all catchments in the setup
  /// </summary>
  public class RRSumTotalRunoff
  {
    private CatchmentCombined _sumTotalRunoffCatchment;
    private Func<double> _runoffGetter;

    private StreamWriter _writer;

    /// <summary>
    /// Create Combined catchments containing all other catchments
    /// Make sure to close <see cref="_writer"/> on simulation end
    /// </summary>
    [Script]
    public void CreateSumAllCatchment(IMike1DController controller)
    {
      Mike1DData mike1DData = controller.Mike1DData;
      _sumTotalRunoffCatchment = new CatchmentCombined("SumAllCatchments")
      {
        ScaleByArea = false,
        Area        = 1,
      };
      double minTimestep = double.MaxValue;
      double maxTimestep = double.MinValue;
      foreach (ICatchment catchment in mike1DData.RainfallRunoffData.Catchments)
      {
        if (!(catchment is CatchmentCombined))
        {
          _sumTotalRunoffCatchment.AddNewCatchment(catchment.ModelId, 1.0);
          minTimestep = System.Math.Min(minTimestep, catchment.TimeStep.TotalSeconds);
          maxTimestep = System.Math.Max(maxTimestep, catchment.TimeStep.TotalSeconds);
        }
      }
      _sumTotalRunoffCatchment.TimeStep = TimeSpan.FromSeconds(minTimestep);
      mike1DData.RainfallRunoffData.Catchments.Add(_sumTotalRunoffCatchment);


      // Setup writer to write total runoff to csv file
      _writer = new StreamWriter("SumTotalRunoff.csv");
      _writer.WriteLine("sep=;");

      _sumTotalRunoffCatchment.PostTimeStepEvent +=
        delegate (DateTime time)
        {
          _writer.WriteLine("{0};{1}", 
            time.ToString(Util.DateTimeFormatString), 
            _runoffGetter().ToString(CultureInfo.InvariantCulture));
        };

      controller.ControllerEvent += HandleControllerEvent;
    }

    /// <summary>
    /// Make sure to close <see cref="_writer"/> on simulation end
    /// </summary>
    private void HandleControllerEvent(object sender, ControllerEventArgs e)
    {
      switch (e.State)
      {
        case ControllerState.Prepared:
          // Getter is available when Prepared
          _runoffGetter = _sumTotalRunoffCatchment.Getter(Quantity.Create(PredefinedQuantity.TotalRunOff));
          break;
        case ControllerState.Finalized:
        case ControllerState.Failed:
          if (_writer != null)
          {
            _writer.Close();
            _writer = null;
          }
          break;
      }
    }
  }
}
