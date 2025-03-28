using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.StructureModule;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Misc scripts for updating structure parameters
  /// </summary>
  public class StructureScripts
  {
    /// <summary>
    /// The Underflow gate has a couple of parameters to stabilize the transition between
    /// the different flow regimes. This script methods sets these parameters for all UnderFlowGate
    /// structures.
    /// </summary>
    [Script]
    public void UpdateUnderflowGateTransitionCoefficients(Mike1DData mike1DData)
    {
      foreach (IStructure structure in mike1DData.Network.StructureCollection.Structures)
      {
        IUnderFlowGate underFlowGate = structure as IUnderFlowGate;
        if (underFlowGate != null)
        {
          // Coefficient used to avoid instabilities when switching from subcritical to critical
          underFlowGate.SurchargedUnderflowCoefficient = 0.1;
          // Coefficient used to avoid instabillities when switching from submerged gate to not submerged gate.
          underFlowGate.UnderflowTransitionCoefficient = 0.1;
        }
      }
    }

    /// <summary>
    /// The EnergyLoss structure solves a set of energy-loss equations using an iterative approach.
    /// This can be somewhat unstable.
    /// Choosing another method can make an unstable energy-loss structure stable.
    /// </summary>
    [Script]
    public void UpdateEnergyLossSolverMethod(Mike1DData mike1DData)
    {
      foreach (IStructure structure in mike1DData.Network.StructureCollection.Structures)
      {
        EnergyLoss energyLoss = structure as EnergyLoss;
        if (energyLoss != null)
        {
          energyLoss.SolverMethod = EnergyEquationSolverMethod.LevenbergMarquardt;
        }
      }
    }


  }
}
