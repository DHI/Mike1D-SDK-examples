using DHI.Mike1D.Engine;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Various scripts for modifying the HTML summary.
  /// </summary>
  public class HTMLSummaryScripts
  {

    /// <summary>
    /// Catch controller, and apply various modifications to the HTML summary.
    /// </summary>
    [Script]
    public void Initialize(IMike1DController controller)
    {
      // The special event is called with name "SummaryReady", when the summary has been build
      // and can be modified.
      controller.SpecialEvent += AddReachLengthColumnToSummary;
    }

    /// <summary>
    /// Called when Summary is ready for writing to file. This method will add an additional
    /// column to the "Reaches - Result summary" table, with the length of the reach/link/branch.
    /// </summary>
    private void AddReachLengthColumnToSummary(object sender, SpecialEventArgs e)
    {
      // Check if this is the right event
      HTMLSummary summary = sender as HTMLSummary;
      if (summary != null && e.Name == "SummaryReady")
      {
        // Find table
        IHTMLSectionItem htmlItem = summary.Sections.FindItem("Reaches - Result summary");
        HTMLTableItem<EngineReach> reachTableItem = htmlItem as HTMLTableItem<EngineReach>;
        if (reachTableItem != null)
        {
          // Add column to the table, also add accumulation (total length of all reaches)
          reachTableItem.Columns.Add(new HTMLTableColumnValueGetter<EngineReach>("Length", reach => reach.LocationSpan.Length()) { Accumulate = true });
        }
      }
    }

  }
}
