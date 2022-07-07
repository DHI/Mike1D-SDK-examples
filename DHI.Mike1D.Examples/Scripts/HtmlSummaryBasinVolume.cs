using System;
using System.Linq;
using DHI.Generic.MikeZero;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Engine.ModuleData;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.NetworkDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Example of a script that makes an additional
  /// section in the HTML summary, containing basin volume statistics
  /// </summary>
  public class HtmlSummaryBasinVolume
  {
    private IMike1DController _controller;
    /// <summary> Storing maximum volume over time for all basins </summary>
    EngineDataItemAllArray<double> _basinMaxVolume;
    /// <summary> Storing time that water level has been above the critical level, for all nodes </summary>
    EngineDataItemAllArray<double> _nodeWlAboveCriticalTime;
    /// <summary> Critical levels for all nodes </summary>
    double[] _criticalNodeLevels;

    /// <summary>
    /// Catch controller, and apply various modifications to the HTML summary.
    /// </summary>
    [Script]
    public void Initialize(IMike1DController controller)
    {
      // Save controller for later use
      _controller = controller;

      // The special event is called with name "SummaryReady", when the summary has been build
      // and can be modified.
      controller.SpecialEvent += AddBasinVolumeTableToSummary;

      // The setup of additional engine data items must wait until the engine is initialized, so
      // register for the ControllerEvent and do it then
      controller.ControllerEvent += ControllerInitialized;

      // Extract Critical Node Levels to a _criticalNodeLevels array
      NetworkDataDouble critLevelsData = _controller.Mike1DData.HDParameters.UserDefinedNetworkDatas["CriticalLevel"];
      _criticalNodeLevels = new double[_controller.Mike1DData.Network.Nodes.Count];
      for (int i = 0; i < _controller.Mike1DData.Network.Nodes.Count; i++)
      {
        INode engineNode = _controller.Mike1DData.Network.Nodes[i];
        double critLevel;
        if (critLevelsData.GetValue(engineNode.Id, out critLevel))
          _criticalNodeLevels[i] = critLevel;
        else if (engineNode is IUndergroundNode) // Use ground level, if not critical level was defined
        {
          IUndergroundNode sewerNode = engineNode as IUndergroundNode;
          _criticalNodeLevels[i] = sewerNode.GroundLevel;
        }
        else
          _criticalNodeLevels[i] = double.MaxValue;
      }
    }

    /// <summary>
    /// Method called whenever the controller state changes (the controller event is triggered)
    /// </summary>
    private void ControllerInitialized(object sender, ControllerEventArgs e)
    {
      var controller = _controller;

      // Only do stuff once, when engine is Initialized
      if (e.State != ControllerState.Initialized)
        return;

      // Make data item that stores the maximum volume in a basin over the entire simulation
      _basinMaxVolume = new EngineDataItemAllArray<double>(_controller.EngineNet, new Quantity("MaxBasinVolume", "Max Basin Volume", eumItem.eumIVolume));
      // Only store values for basin nodes (EngineNodeBasin)
      _basinMaxVolume.SetupDataForNodes(_controller.EngineNet, (engineNet, nodeIndex) => engineNet.Nodes[nodeIndex] is EngineNodeBasin);
      controller.EngineNet.DataModule.AddDataItem(_basinMaxVolume);

      // Make data item that stores the time the water level in a node was above the critical level
      _nodeWlAboveCriticalTime = new EngineDataItemAllArray<double>(_controller.EngineNet, new Quantity("BasinWaterLevelCritical", "Time above critical level", eumItem.eumITime));
      _nodeWlAboveCriticalTime.SetupDataForNodes(_controller.EngineNet);
      controller.EngineNet.DataModule.AddDataItem(_nodeWlAboveCriticalTime);

      // Register for the post-time-step event to update the above data items.
      controller.EngineNet.PostTimeStepEvent += UpdateDataItems;

    }

    /// <summary>
    /// After every time step, this method is called.
    /// </summary>
    private void UpdateDataItems(DateTime time)
    {
      var engineNodes = _controller.EngineNet.Nodes;
      var hdNodes = _controller.EngineNet.HDModule.Nodes;

      for (int i = 0; i < engineNodes.Count; i++)
      {
        // Water level in node
        double waterLevel = hdNodes[i].WaterLevelNp1;
        
        // Update time above critical level
        if (waterLevel > _criticalNodeLevels[i])
          _nodeWlAboveCriticalTime.NodesData[i] += _controller.EngineNet.EngineTime.Dt;

        // For basins, also update the maximum volume
        var basin = engineNodes[i] as EngineNodeBasin;
        if (basin != null)
        {
          // Water volume in basin
          double volume = basin.GetVolume(waterLevel);
          // Update maximum volume
          if (volume > _basinMaxVolume.NodesData[i])
            _basinMaxVolume.NodesData[i] = volume;
        }
      }
    }

    /// <summary>
    /// Called when Summary is ready for writing to file. This method will add an additional
    /// table to the summary
    /// </summary>
    private void AddBasinVolumeTableToSummary(object sender, SpecialEventArgs e)
    {
      // Check if this is the right event
      HTMLSummary summary = sender as HTMLSummary;
      if (summary != null && e.Name == "SummaryReady")
      {
        // Add new section to summary
        HTMLSection hdMySection = new HTMLSection("Basin summary section");
        summary.Sections.Add(hdMySection);

        // Create a new summary table and add it to section
        HTMLTableItem<EngineNode> basinVolumeTable = new HTMLTableItem<EngineNode>() { Name = "Basins - Volume" };
        basinVolumeTable.RowData = _controller.EngineNet.Nodes;
        basinVolumeTable.RowSelector = (i, node) => node is EngineNodeBasin;
        basinVolumeTable.Columns.Add(new HTMLTableColumnStringGetter<EngineNode>("Basin ID", n => n.ID));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Basin Volume - capacity, raw", n => GetBasinRawVolume(n as EngineNodeBasin), new eumQuantity(eumItem.eumIVolume, eumUnit.eumUm3)));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Basin Volume - capacity with expanding cover", n => GetBasinVolumeWithCover(n as EngineNodeBasin), new eumQuantity(eumItem.eumIVolume, eumUnit.eumUm3)));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Basin Volume - maximum", n => _basinMaxVolume.GetValue(n), new eumQuantity(eumItem.eumIVolume, eumUnit.eumUm3)));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Basin Volume - maximum raw full percent", n => _basinMaxVolume.GetValue(n) / GetBasinRawVolume(n as EngineNodeBasin) * 100, new eumQuantity(eumItem.eumIFraction, eumUnit.eumUPerCent)));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Critical level", n => _criticalNodeLevels[n.NodeListIndex], new eumQuantity(eumItem.eumITime, eumUnit.eumUsec)));
        basinVolumeTable.Columns.Add(new HTMLTableColumnValueGetter<EngineNode>("Minutes above critical level", n => _nodeWlAboveCriticalTime.GetValue(n) / 60, new eumQuantity(eumItem.eumITime, eumUnit.eumUminute)));
        hdMySection.Items.Add(basinVolumeTable);
      }
    }

    /// <summary>
    /// Helper method returning the raw geometric volume of a basin
    /// </summary>
    private double GetBasinRawVolume(EngineNodeBasin basin)
    {
      if (basin == null) return 0;
      return basin.Geometry.GetVolume(basin.Geometry.Levels.Last(), true);
    }

    /// <summary>
    /// Helper method returning the volume of a basin, including any expanding cover changes,
    /// i.e. the value will differ only for the normal expanding cover.
    /// </summary>
    private double GetBasinVolumeWithCover(EngineNodeBasin basin)
    {
      if (basin == null) return 0;
      return basin.GetVolume(basin.Geometry.Levels.Last());
    }
  }
}
