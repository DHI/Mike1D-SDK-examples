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
  /// for all catchments
  /// </summary>
  public class RRSumTotalRunoff
  {
    CatchmentCombined sumTotalRunoffCatchment;
    Func<double> runoffGetter;

    StreamWriter writer;

    /// <summary>
    /// Create Combined catchments containing all other catchments
    /// Make sure to close <see cref="writer"/> on simulation end
    /// </summary>
    [Script]
    public void CreateSumAllCatchment(IMike1DController controller)
    {
      Mike1DData mike1DData = controller.Mike1DData;
      sumTotalRunoffCatchment = new CatchmentCombined("SumAllCatchments")
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
          sumTotalRunoffCatchment.AddNewCatchment(catchment.ModelId, 1.0);
          minTimestep = System.Math.Min(minTimestep, catchment.TimeStep.TotalSeconds);
          maxTimestep = System.Math.Max(maxTimestep, catchment.TimeStep.TotalSeconds);
        }
      }
      sumTotalRunoffCatchment.TimeStep = TimeSpan.FromSeconds(minTimestep);
      mike1DData.RainfallRunoffData.Catchments.Add(sumTotalRunoffCatchment);


      // Setup writer to write total runoff to csv file
      writer = new StreamWriter("SumTotalRunoff.csv");
      sumTotalRunoffCatchment.PostTimeStepEvent +=
        delegate (DateTime time)
        {
          writer.WriteLine("{0};{1}", 
            time.ToString(Util.DateTimeFormatString), 
            runoffGetter().ToString(CultureInfo.InvariantCulture));
        };

      controller.ControllerEvent += HandleControllerEvent;
    }

    /// <summary>
    /// Make sure to close <see cref="writer"/> on simulation end
    /// </summary>
    private void HandleControllerEvent(object sender, ControllerEventArgs e)
    {
      switch (e.State)
      {
        case ControllerState.Prepared:
          // Getter is available when Prepared
          runoffGetter = sumTotalRunoffCatchment.Getter(Quantity.Create(PredefinedQuantity.TotalRunOff));
          break;
        case ControllerState.Finalized:
        case ControllerState.Failed:
          if (writer != null)
          {
            writer.Close();
            writer = null;
          }
          break;
      }
    }
  }
}
