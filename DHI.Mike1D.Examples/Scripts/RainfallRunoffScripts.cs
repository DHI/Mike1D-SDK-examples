using System;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;
using DHI.Mike1D.RainfallRunoffModule.LID;

namespace DHI.Mike1D.Examples.Scripts
{

  /// <summary>
  /// Various scripts for Rainfall Runoff modeling.
  /// <para>
  /// In most cases only one script methods is to be applied.
  /// The other methods can either be deleted, or just disabled by removing the
  /// [script]
  /// attribute in front of the method.
  /// </para>
  /// </summary>
  public class RainfallRunoffScripts
  {
    /// <summary>
    /// Set Catchment Discharge time step to 30 minutes.
    /// <para>
    /// This can be used to set another timestep for CD than for RR
    /// </para>
    /// </summary>
    [Script]
    public void SetCatchmentDischargeTimestep(Mike1DData mike1DData)
    {
      foreach (ICatchment catchment in mike1DData.RainfallRunoffData.Catchments)
      {
        if (catchment is CatchmentDischarge)
        {
          CatchmentDischarge cdCatchment = catchment as CatchmentDischarge;
          cdCatchment.TimeStep = TimeSpan.FromMinutes(30);
        }
      }
    }


    /// <summary>
    /// Create a Combined Catchment containing all other catchments,
    /// effectively making one catchment summing up Total Runoff for all other catchments.
    /// </summary>
    [Script]
    public void CreateSumTotalRunoffCatchment(Mike1DData mike1DData)
    {
      CatchmentCombined sumTotalRunoffCatchment = new CatchmentCombined("SumAllCatchments")
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
    }


    /// <summary>
    /// Set Catchment Discharge time step to 30 minutes.
    /// <para>
    /// This can be used to set another timestep for CD than for RR
    /// </para>
    /// </summary>
    [Script]
    public void DisableLidReporting(Mike1DData mike1DData)
    {
      foreach (ICatchment icatchment in mike1DData.RainfallRunoffData.Catchments)
      {
        if (icatchment is Catchment)
        {
          Catchment catchment = icatchment as Catchment;
          foreach (IStormwaterLID lid in catchment.StormWaterLIDs)
          {
            LIDBase lidBase = lid as LIDBase;
            if (lidBase != null)
              lidBase.ReportPath = null;
          }
        }
      }
    }


    /// <summary>
    /// Script that takes Snow parameters from RDI (NAM)
    /// and applies it to the matching urban catchment (Model A or Model B).
    /// Assumes the model is set up as RDI+A or RDI+B.
    /// </summary>
    //[Script] // Disabled right now. Remove the // in front of [Script] to enable again
    public void CatchmenUrbanSnowFromRDI(Mike1DData m1DData)
    {
      if (m1DData.UseRR && m1DData.RainfallRunoffData != null)
      {
        // Loop over all catchments 
        foreach (ICatchment catchment in m1DData.RainfallRunoffData.Catchments)
        {
          // Find the combined catchment
          if (catchment is CatchmentCombined)
          {
            // Find the two catchment models, being RDI+A or RDI+B
            CatchmentCombined catchmentCombined = catchment as CatchmentCombined;
            ICatchmentNamData   catchmentNAM = null; // RDI catchment
            ICatchmentUrbanData catchmentUrb = null; // Urban catchment
            // Loop through all sub-catchments
            foreach (string subCatchId in catchmentCombined.SubCatchmentNames.Keys)
            {
              // Based on sub-catchment id, find actual catchment
              ICatchment c = m1DData.RainfallRunoffData.Catchments.Find(subCatchId);
              // Check if catchment is RDI/NAM or urban catchment (Model A/B)
              if (c is ICatchmentNamData) catchmentNAM   = (ICatchmentNamData)c;
              if (c is ICatchmentUrbanData) catchmentUrb = (ICatchmentUrbanData)c;
            }

            // Check if both RDI and A/B was found
            if (catchmentNAM != null && catchmentUrb != null)
            {
              // Apply RDI snow parameters on urban catchment.
              catchmentUrb.UseSnowModule = catchmentNAM.IncludeSnow;
              // Convert from mm/day to m/s
              catchmentUrb.SnowMeltCoefficient = catchmentNAM.ConstDegreeDayCoef * 0.001 / (3600*24);
            }
          }
        }
      }
    }

    /// <summary>
    /// Script that applies snow parameters to all urban catchments (Model A or Model B).
    /// </summary>
    [Script]
    public void CatchmenUrbanSnow(Mike1DData m1DData)
    {
      // Snow melt coefficient in [mm/C/day]
      // This snow melt coefficient is applied to all Model A and Model B catchments.
      double snowMeltMmCelsDay = 2;

      if (m1DData.UseRR && m1DData.RainfallRunoffData != null)
      {
        // Loop over all catchments 
        foreach (ICatchment catchment in m1DData.RainfallRunoffData.Catchments)
        {
          // Check if it is an urban catchment
          if (catchment is ICatchmentUrbanData)
          {
            ICatchmentUrbanData catchmentUrb = catchment as ICatchmentUrbanData;
            // Apply snow parameters on urban catchment.
            catchmentUrb.UseSnowModule = true;
            // SnowMeltCoefficient is in [m/C/s], convert from [mm/C/day]
            catchmentUrb.SnowMeltCoefficient = snowMeltMmCelsDay * 0.001 / (3600 * 24);
          }
        }
      }
    }

    #region Plowing script

    /// <summary>
    /// Script that enables snow plowing on all urban catchments (Model A or Model B).
    /// </summary>
    [Script]
    public void CatchmenUrbanSnowPlowingSetup(IMike1DController controller)
    {
      // Listen for ControllerEvent's
      controller.ControllerEvent += CatchmentUrbanSnowPlowingControllerEvent;
    }

    /// <summary>
    /// Method is called when <see cref="IMike1DController.ControllerEvent"/> is triggered.
    /// </summary>
    private void CatchmentUrbanSnowPlowingControllerEvent(object sender, ControllerEventArgs e)
    {
      // Listen for Prepared event (i.e. model is ready to run)
      if (e.State == ControllerState.Prepared)
      {
        Mike1DController controller = (Mike1DController) sender;

        // Find all urban catchments, loop over all catchments
        foreach (ICatchment catchment in controller.Mike1DData.RainfallRunoffData.Catchments)
        {
          // Check if it is an urban catchment
          if (catchment is CatchmentAbstractUrban)
          {
            CatchmentAbstractUrban catchmentAbstractUrban = catchment as CatchmentAbstractUrban;
            // Check if snow-module is enabled.
            if (catchmentAbstractUrban.UseSnowModule)
            {
              // Set up plowing for urban catchment
              UrbanCatchmentPlowing urbanCatchmentPlowing = new UrbanCatchmentPlowing(catchmentAbstractUrban);
              catchment.PostTimeStepEvent += urbanCatchmentPlowing.Plowing;
            }
          }
        }
      }
    }


    /// <summary>
    /// Helper class for snow plowing in urban catchments
    /// </summary>
    class UrbanCatchmentPlowing
    {
      /// <summary> Urban catchment to plow </summary>
      private CatchmentAbstractUrban _urbanCatchment;
      
      /// <summary> Time of last plowing </summary>
      private DateTime _lastPlowed;

      private TimeSpan _timeStepDry;

      public UrbanCatchmentPlowing(CatchmentAbstractUrban urbanCatchment)
      {
        _urbanCatchment = urbanCatchment;
        _lastPlowed = DateTime.MinValue;

        if (_urbanCatchment is CatchmentKinematicWave)
        {
          // Circumventing dry-jumping when there is snow on the catchment
          CatchmentKinematicWave catchmentKinematicWave = (CatchmentKinematicWave)_urbanCatchment;
          _timeStepDry = catchmentKinematicWave.TimeStepDry;
        }
      }

      /// <summary>
      /// This method is called whenever the urban catchment has completed a time step.
      /// </summary>
      /// <param name="time">Simulation time</param>
      public void Plowing(DateTime time)
      {
        if (_urbanCatchment is CatchmentKinematicWave)
        {
          // Circumventing dry-jumping when there is snow on the catchment:
          CatchmentKinematicWave catchmentKinematicWave = (CatchmentKinematicWave)_urbanCatchment;
          // Disable jumping when there is snow, re-enable jumping when there is no snow.
          if (_urbanCatchment.SnowStorage > 0)
            catchmentKinematicWave.TimeStepDry = catchmentKinematicWave.TimeStep;
          else
            catchmentKinematicWave.TimeStepDry = _timeStepDry;
        }

        // Plow between 4 and 6 every morning
        if (time > _lastPlowed.AddHours(12) && 4 <= time.Hour && time.Hour <= 6)
        {
          // Plow 1 mm (0.001 m), if there is more than 2 mm on the ground
          if (_urbanCatchment.SnowStorage > 0.002)
            _urbanCatchment.SnowStorage -= 0.001;
          else if (_urbanCatchment.SnowStorage > 0.001)
            _urbanCatchment.SnowStorage = 0.001;
          // Store _lastPlowed, to only plow once a day.
          _lastPlowed = time;
        }
      }
    }
    #endregion

  }
}
