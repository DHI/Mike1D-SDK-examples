using System;
using System.Collections.Generic;
using DHI.Generic.MikeZero;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Engine.ModuleData;
using DHI.Mike1D.Engine.ModuleHD;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.ResultDataAccess;


namespace DHI.Mike1D.Examples.Scripts
{
  public class WaterVolumeChangeDA
  {
    private Mike1DController _mike1dController;
    private EngineNet _engineNet;
    private DAWaterVolumeChangeCalculator _daWaterVolumeChangeCalculator;

    /// <summary>
    /// Method called when IMike1DController is available.
    /// </summary>
    [Script]
    public void Initialize(IMike1DController controller)
    {
      controller.ControllerEvent += ControllerOnControllerEvent;
    }


    private void ControllerOnControllerEvent(object sender, ControllerEventArgs e)
    {
      if (e.State == ControllerState.Initialized)
      {
        _mike1dController = (Mike1DController)sender;
        _mike1dController.Mike1DData.ResultSpecifications[0].WhatAddUnique(new Quantity("WaterVolumeChangeDA", "DA - Water volume change", eumItem.eumIVolume));
        _mike1dController.Mike1DData.ResultSpecifications[0].WhatAddUnique(new Quantity(
          "WaterVolumeAccumulatedChangeDA", "DA - Accumulated water volume change", eumItem.eumIVolume));
      }

      if (e.State == ControllerState.Preparing)
      {
        _engineNet = _mike1dController.EngineNet;
        CreateVolCalculator(_engineNet);
        _engineNet.EvaluateHDImplicitSourcesEvent += PostHDTimeStepEvent;
      }

      if (e.State == ControllerState.Finalizing)
      {
        string totalAddedWater = _daWaterVolumeChangeCalculator.GetAccumulatedAddedVolume().ToString();
        _mike1dController.DiagnosticsHandler.NewMessage(DiagnosticsHandler.MessageType.Info, "DA - Total added water volume: "+ totalAddedWater+" m3");
      }
    }

    private void PostHDTimeStepEvent()
    {
      _daWaterVolumeChangeCalculator.UpdatePostHDValues();
    }

    public void CreateVolCalculator(EngineNet engineNet)
    {
      _daWaterVolumeChangeCalculator = DAWaterVolumeChangeCalculator.Create(engineNet);
      engineNet.DataModule.AddDataItem(_daWaterVolumeChangeCalculator);
    }
  }
  
  /// <summary>
  /// Helper class to calculate the change of volume of water in a timestep in the network
  /// </summary>
  public class DAWaterVolumeChangeCalculator : EngineDataItemAllArray<double>
  {
    private HDModule _hdModule;
    EngineDataItemAll<double> _volPostHd;
    EngineDataItemAll<double> _volAccumulated;

    internal DAWaterVolumeChangeCalculator(EngineNet engineNet, IQuantity quantity)
      : base(engineNet, quantity)
    {
      _hdModule = engineNet.HDModule;

      _volPostHd = new EngineDataItemAll<double>(engineNet, new Quantity());
      _volPostHd.SetupDataForNodes(engineNet);
      _volPostHd.SetupDataForReaches(engineNet, (gp) => (gp is HGridPoint));
      _volPostHd.GenericData = new EngineDataGeneric<double>() { Values = new double[1] };

      var volumeAccumulatedQuant =
        new Quantity("WaterVolumeAccumulatedChangeDA", "DA - Accumulated water volume change", eumItem.eumIVolume);
      _volAccumulated = new EngineDataItemAll<double>(engineNet, volumeAccumulatedQuant);
      _volAccumulated.SetupDataForNodes(engineNet);
      _volAccumulated.SetupDataForReaches(engineNet, (gp) => (gp is HGridPoint));
      _volAccumulated.GenericData = new EngineDataGeneric<double>() { Values = new double[1] };
      engineNet.DataModule.AddDataItem(_volAccumulated);
    }

    ///<summary>
    /// Create helper class for calculating total inflow to a reach
    ///</summary>
    ///<param name="engineNet"></param>
    public static DAWaterVolumeChangeCalculator Create(EngineNet engineNet)
    {
      // TODO: Quantity
      DAWaterVolumeChangeCalculator res = new DAWaterVolumeChangeCalculator(engineNet, new Quantity("WaterVolumeChangeDA", "DA - Water volume change", eumItem.eumIVolume));

      res.SetupDataForNodes(engineNet);
      res.SetupDataForReaches(engineNet, (gp) => (gp is HGridPoint));
      res.GenericData = new EngineDataGeneric<double>() { Values = new double[1] };

      return res;
    }

    public double GetAccumulatedAddedVolume()
    {
      return Math.Round(_volAccumulated.GenericData[0],1);
    }

    public void UpdatePostHDValues()
    {
      {
        double sum = 0;
        EngineNet engineNet = _engineNet;
        for (int i = 0; i < engineNet.Nodes.Count; i++)
        {
          //if (NodesData[i] == null) continue;
          var engineNodeSewer = engineNet.Nodes[i] as EngineNodeVolume;
          if (engineNodeSewer == null) continue;
          IHDNode hdNode = _hdModule.GetNode(engineNodeSewer);
          if (hdNode == null) continue;
          double volume = engineNodeSewer.GetVolume(hdNode.WaterLevelNp1);
          _volPostHd.NodesData[i].Value = volume;
          sum += volume;
        }
        for (int i = 0; i < engineNet.Reaches.Count; i++)
        {
          IEngineDataReach<double> reachData = _volPostHd.ReachesData[i];
          if (reachData == null) continue;
          IHDReach hdReach = _hdModule.GetReach(engineNet.Reaches[i]);
          if (hdReach == null) continue;
          for (int j = 0; j < reachData.IndexList.Length; j++)
          {
            int jgp = reachData.IndexList[j];
            IHDHGridPoint hgp = hdReach.GridPoints[jgp] as IHDHGridPoint;
            if (hgp == null)
              reachData[j] = 0;
            else
            {
              reachData[j] = hgp.WaterVolumeNp1;
              sum += hgp.WaterLevelNp1;
            }
          }
        }
        // Store total 
        GenericData[0] = sum;
      }
    }

    /// <inheritdoc />
    public override void UpdateValues()
    {
      // if pull version, values are extracted when requested, and no action is required here.
      // If not pull, this needs to update the data and store it.
      {
        double sum = 0;
        EngineNet engineNet = _engineNet;
        for (int i = 0; i < engineNet.Nodes.Count; i++)
        {
          //if (NodesData[i] == null) continue;
          var engineNodeSewer = engineNet.Nodes[i] as EngineNodeVolume;
          if (engineNodeSewer == null) continue;
          IHDNode hdNode = _hdModule.GetNode(engineNodeSewer);
          if (hdNode == null) continue;
          double volumeN = _volPostHd.NodesData[i].Value;
          double volumeNp1 = engineNodeSewer.GetVolume(hdNode.WaterLevelNp1);
          double volumechange = volumeNp1 - volumeN;
          sum += volumechange;
          NodesData[i] = volumechange;
          _volAccumulated.NodesData[i].Value += volumechange;
        }
        for (int i = 0; i < engineNet.Reaches.Count; i++)
        {
          IEngineDataReach<double> reachDataPostHD = _volPostHd.ReachesData[i];
          IEngineDataReach<double> reachDataAcc = _volAccumulated.ReachesData[i];
          IEngineDataReach<double> reachData = ReachesData[i];
          if (reachData == null) continue;
          IHDReach hdReach = _hdModule.GetReach(engineNet.Reaches[i]);
          if (hdReach == null) continue;
          for (int j = 0; j < reachData.IndexList.Length; j++)
          {
            int jgp = reachData.IndexList[j];
            IHDHGridPoint hgp = hdReach.GridPoints[jgp] as IHDHGridPoint;
            if (hgp == null)
              reachData[j] = 0;
            else
            {
              double volumeN = reachDataPostHD[j];
              double volumeNp1 = hgp.WaterVolumeNp1;
              double volumeChange = volumeNp1 - volumeN;
              sum += volumeChange;
              reachData[j] = volumeChange;
              reachDataAcc[j] += volumeChange;
            }
          }
        }
        // Store total 
        GenericData[0] = sum;
        _volAccumulated.GenericData[0] += sum;
      }
    }
  }

}
