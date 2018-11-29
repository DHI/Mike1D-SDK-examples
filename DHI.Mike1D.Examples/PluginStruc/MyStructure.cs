using System;
using DHI.Mike1D.StructureModule;
using DDoubleGetter = System.Func<double>;

namespace DHI.Mike1D.Examples.PluginStruc
{
  /// <summary>
  /// MyStructure class, implementing the Honma weir equations.
  /// <para>
  /// It is necessary for a structure to implement the <see cref="IStructure"/> 
  /// interface. For convenience there is a basic (abstract) <see cref="Structure"/>
  /// class that takes care of many of the details, so it is usually easier to
  /// extent from the <see cref="Structure"/> class.
  /// </para>
  /// <para>
  /// Code marked with (*2) is example code on how to enable user defined output
  /// from a structure. StructurePlugin.AddStructureOutput method on how to enable 
  /// the output in the default HD result file.
  /// </para>
  /// <para>
  /// All code and methods marked with (*2) can be safely deleted, in case
  /// that functionality is not desired.
  /// </para>
  /// </summary>
  /// 
  public class MyStructure : Structure
  {
    /// <summary> Crest level of weir </summary>
    public double CrestLevel;

    /// <summary> Crest width of weir </summary>
    public double CrestWidth;

    /// <summary> Honma weir coefficient </summary>
    public double WeirCoefficient;

    /// <summary>
    /// Linear flow variation.                                               
    /// For water level differences less than delhs, use a linear Q variation.
    /// This is to avoid stability problems for small water level differences where 
    /// dQdh goes to infinity.  
    /// </summary>
    public double Delhs = 0.1;

    /// <summary>
    /// This is the main structure method, which much calculate the structure flow. 
    /// It gets the upstream and downstream water level, and must calculate the three values:
    /// - <see cref="IStructure.Discharge"/>
    /// - <see cref="IStructure.dDischargedDownStreamWaterLevel"/>
    /// - <see cref="IStructure.dDischargedUpStreamWaterLevel"/>.
    /// <para>
    /// The derivatives with respect to downstream and upstream water level need not be calculated, 
    /// nevertheless, the solution will be much more accurate and stable when included.
    /// </para>
    /// <para>
    /// Since this class is extending from the <see cref="Structure"/> class, 
    /// we need to set the _q,  _dqdhUpstream and _dqdhDownstream variables.
    /// </para>
    /// </summary>
    /// <param name="upStreamWaterLevel">  Water level in upstream   direction (lower chainage - higher chainage if reach direction is reversed (Flow direction = negative)")</param>
    /// <param name="downStreamWaterLevel">Water level in downstream direction (higher chainage - lower chainage if reach direction is reversed (Flow direction = negative))</param>
    public override void SetWaterLevels(double upStreamWaterLevel, double downStreamWaterLevel)
    {
      // Determine flow direction
      FlowDir flowDir = upStreamWaterLevel >= downStreamWaterLevel ? FlowDir.PositiveFlow : FlowDir.NegativeFlow;

      // Set x and y such that x is the largest depth
      double x, y;
      if (flowDir == FlowDir.PositiveFlow)
      {
        x = upStreamWaterLevel - CrestLevel;
        y = downStreamWaterLevel - CrestLevel;
      }
      else
      {
        x = downStreamWaterLevel - CrestLevel;
        y = upStreamWaterLevel - CrestLevel;
      }

      if (x < 1e-15)
      {
        // Flow upstream water level below crest level
        // Set _q and derivatives to zero.
        _q = 0;
        _dqdhUpstream = 0;
        _dqdhDownstream = 0;
      }
      else if (x - y > Delhs)
      {
        // Difference between upstream and downstream water level is sufficiently big
        // Calculate structure flow
        HonmaWeirFlow(x, y);
      }
      else
      {
        // For water level differences less than delhs, use a linear variation in Q
        // between (x,y) = (x,x-delhs) and (x,x). This is to avoid stability problems 
        // for small water level differences where dQdh goes to infinity. 
        HonmaWeirFlow(x, x - Delhs);
        Linearisation(ref _q, ref _dqdhUpstream, ref _dqdhDownstream, Delhs, x - y);
      }

      // If the flow is negative, switch sign and derivatives
      if (flowDir == FlowDir.NegativeFlow)
      {
        SwitchFlowDirection(ref _q, ref _dqdhUpstream, ref _dqdhDownstream);
      }
    }

    /// <summary>
    /// Evaluate Honma formula
    /// </summary>
    /// <param name="x">Upstream depth above crest</param>
    /// <param name="y">Downstream depth above crest</param>
    private void HonmaWeirFlow(double x, double y)
    {
      if ((y/x) <= (2.0/3.0))
      {
        // Free overflow
        _q = WeirCoefficient*CrestWidth*Math.Pow(x, 1.5);
        _dqdhUpstream = 1.5*WeirCoefficient*CrestWidth*Math.Sqrt(x);
        _dqdhDownstream = 0.0;
      }
      else
      {
        // Submerged flow
        double coefficient2 = 1.5*Math.Sqrt(3)*WeirCoefficient;
        _q = coefficient2*CrestWidth*y*Math.Sqrt(x - y);
        double helpVar = coefficient2*CrestWidth/(2*Math.Sqrt(x - y));
        _dqdhUpstream = helpVar*y;
        _dqdhDownstream = helpVar*(2*x - 3*y);
      }
    }


    /// <summary>
    /// String identifying the type of structure
    /// <para>
    /// Used for grouping structures, e.g. in the result presentations
    /// </para>
    /// </summary>
    public override string Type
    {
      get { return "MyStructure"; }
    }

    /// <summary>
    /// Invert level up stream. 
    /// The invert level is the level where water starts 
    /// flowing in to the structure from the upstream side. 
    /// <para>
    /// It is used in MIKE URBAN type setups, for setting up
    /// initial conditions on surrounding grid points and nodes
    /// and when processing "flush-in" from boundaries.
    /// </para>
    /// </summary>
    public override double InvertLevelUpstream
    {
      get { return CrestLevel; }
    }

    /// <summary>
    /// Invert level down stream
    /// The invert level is the level where water starts 
    /// flowing in to the structure from the downstream side. 
    /// <para>
    /// It is used in MIKE URBAN type setups, for setting up
    /// initial conditions on surrounding grid points and nodes
    /// and when processing "flush-in" from boundaries.
    /// </para>
    /// </summary>
    public override double InvertLevelDownstream
    {
      get { return CrestLevel; }
    }


    ///// <summary>
    ///// Method to initialize structure. Override, if required, especially if cross section data is required
    ///// in order to setup the structure. In case you do not need any special initialization, this need not be overridden.
    ///// </summary>
    ///// <param name="boundaryData">Boundary data to get time series information</param>
    ///// <param name="crossSectionData">Cross section data to get cross section on each side of the structure</param>
    ///// <param name="diagnostics">Diagnostics that errors are reported to</param>
    ///// <param name="reverseDirection">Set to true if the direction of the structure is reversed</param>
    //public override void Initialize(DHI.Mike1D.BoundaryModule.IBoundaryData boundaryData, DHI.Mike1D.CrossSectionModule.CrossSectionData crossSectionData, Generic.IDiagnostics diagnostics, bool reverseDirection)
    //{
    //  base.Initialize(boundaryData, crossSectionData, diagnostics, reverseDirection);
    //}


    #region (*2) User defined output
    /**
     * To enable user defined output, you need to specify which outputs are offered,
     * and you need to provide a delegate (pointer to function) that can extract
     * the value. The Offers() method here below must return what is offered, while
     * the ValueGetter() method returns the delegate.
     * 
     * See the StructurePlugin.AddStructureOutput method on how to enable the output 
     * in the result file.
     */

    /// <summary>
    /// (*2) Quantity for weir coefficient
    /// If there is no appropriate EUM item and EUM unit to use, you can always use the 
    /// DHI.Generic.MikeZero.eumItem.eumIItemUndefined and DHI.Generic.MikeZero.eumUnit.eumUUnitUndefined
    /// </summary>
    public static DHI.Mike1D.Generic.IQuantity WeirCoefficientQuantity =
      new DHI.Mike1D.Generic.Quantity("MyStructureWeirCoeff", "Structure Weir Coefficient",
                                      DHI.Generic.MikeZero.eumItem.eumIItemUndefined,
                                      DHI.Generic.MikeZero.eumUnit.eumUMeter2OneHalfPerSec);

    /// <summary>
    /// (*2) List of data types that is offered, i.e., that you can get a ValueGetter for.
    /// </summary>
    public override System.Collections.Generic.IList<DHI.Mike1D.Generic.IQuantity> Offers()
    {
      if (_offers == null)
      {
        _offers = new System.Collections.Generic.List<DHI.Mike1D.Generic.IQuantity>();
        
        // Add a quantity. 
        _offers.Add(WeirCoefficientQuantity);
      }
      return _offers;
    }

    /// <summary>
    /// (*2) Get value getter for the specified data quantity
    /// </summary>
    /// <param name="quantityIndex">Requested Quantity, index in <see cref="DHI.Mike1D.Generic.IQuantityProxy.Offers"/> list</param>
    /// <returns>A value getter delegate that can get the value</returns>
    public override DDoubleGetter ValueGetter(int quantityIndex)
    {
      switch (quantityIndex)
      {
        case 0:
          // Create a delegate that returns the WeirCoefficients
          return (delegate { return WeirCoefficient; });
      }
      throw new ArgumentOutOfRangeException("quantityIndex");
    }

    #endregion

  }
}