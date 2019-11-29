using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using IMike1DPlugin = DHI.Mike1D.Plugins.IMike1DPlugin;

namespace DHI.Mike1D.Examples.PluginStruc
{
  /// <summary>
  /// Example of a Mike 1D Plugin that adds structures to the setup.
  /// It also includes:
  /// (*1) Example on how to implement user defined control of structure
  /// (*2) Example on how to enable user defined output to the HD result file
  /// <para>
  /// This plugin implementation assumes that the input variable 
  /// provided in <see cref="Initialize"/> contains a number of 
  /// arguments where the key is "Struc" and the value is a semi-colon
  /// seperated set of parameters for each structure, i.e.
  /// all parameters included for the structure is defined in the
  /// value. There can be any number of "Struc" keys, for adding
  /// an arbitrary number of structures.
  /// </para>
  /// <para>
  /// For MIKE 11 and MIKE URBAN setups, the way to enable this
  /// is to create a .m1da file with the same filename as the
  /// MIKE 11 .sim11 file or MIKE URBAN .mdb database (just
  /// another extension). See the 
  /// <code>
  /// data\UnitFlow\UnitFlowStruc.m1da
  /// </code>
  /// for an example of such a .m1da file.
  /// </para>
  /// <para>
  /// Code marked with (*1) is example code on how to update the structure
  /// parameters before every time step. 
  /// </para>
  /// <para>
  /// Code marked with (*2) is example code on how to enable user defined output
  /// from a structure to the default result files.
  /// </para>
  /// <para>
  /// All code and methods marked with (*1)/(*2) can be safely deleted, in case
  /// that functionality is not desired.
  /// </para>
  /// </summary>
  public class StructurePlugin : IMike1DPlugin
  {
    private IList<Mike1DPluginArgument> _arguments;

    /// <summary> Controller, running the actual simulation </summary>
    private IMike1DController _controller;

    /// <summary>
    /// Initialize plugin, providing arguments from setup, see <see cref="P:DHI.Mike1D.Mike1DDataAccess.Mike1DPluginInfo.Arguments"/>
    /// </summary>
    /// <param name="arguments">Arguments stored in setup</param>
    /// <param name="mike1DData">MIKE 1D data object</param>
    public void Initialize(IList<Mike1DPluginArgument> arguments, Mike1DData mike1DData)
    {
      _arguments = arguments;

      // (*1) Register for the ControllerCreated event 
      mike1DData.ControllerCreatedEvent += ControllerCreated;

      // (*1) Store list of MyStructure and original weir coefficient, used in PreTimeStepEvent
      _myStructures = new List<MyStructure>();
      _myStructuresWeirCoefficients = new List<double>();

      // (*2) Add structure output to HD result file. This works together with the 
      //      implementation of Offers and ValueGetter in MyStructure class
      AddStructureOutput(mike1DData);

      // Culture used when parsing arguments
      CultureInfo invariantCulture = CultureInfo.InvariantCulture;

      // Process each argument
      foreach (Mike1DPluginArgument argument in _arguments)
      {
        // Process only arguments with key == "Struc"
        if (!System.StringComparer.OrdinalIgnoreCase.Equals(argument.Key, "Struc"))
          continue;

        string[] values = argument.Value.Split(';');

        // Create a new MyStructure
        MyStructure structure = new MyStructure();

        // This must be a unique ID!!!
        structure.ID = values[0];
        // Location in network
        structure.Location = new Location(values[1], double.Parse(values[2], invariantCulture));
        // Use TopoID of reach
        structure.TopoID = values[3];

        // Structure parameters - structure dependent.
        structure.CrestLevel = double.Parse(values[4], invariantCulture);
        structure.CrestWidth = double.Parse(values[5], invariantCulture);
        structure.WeirCoefficient = double.Parse(values[6], invariantCulture);

        // Add it to Mike 1D setup
        mike1DData.Network.StructureCollection.Structures.Add(structure);

        // (*1) Store structure locally
        _myStructures.Add(structure);
        _myStructuresWeirCoefficients.Add(structure.WeirCoefficient);

      }
    }

    #region (*1)/(*2) methods and variables
    
    /// <summary> (*1) List of structures </summary>
    private IList<MyStructure> _myStructures;
    /// <summary> (*1) List of original weir coefficients </summary>
    private IList<double> _myStructuresWeirCoefficients; 

    /// <summary>
    /// (*1) Method that is called when a new controller has been created.
    /// </summary>
    /// <param name="sender">The <see cref="IMike1DController"/> that triggered the event</param>
    /// <param name="controllerCreatedEventArgs">Event arguments</param>
    private void ControllerCreated(object sender, ControllerCreatedEventArgs controllerCreatedEventArgs)
    {
      // Store the controller, and register for the ControllerEvent.
      _controller = controllerCreatedEventArgs.Controller;
      _controller.ControllerEvent += ControllerEvent;
    }

    /// <summary>
    /// (*1) Method that is called everytime the controller changes state
    /// </summary>
    /// <param name="sender">The <see cref="IMike1DController"/> that triggered the event</param>
    /// <param name="controllerEventArgs">Event arguments, containing new controller state</param>
    private void ControllerEvent(object sender, ControllerEventArgs controllerEventArgs)
    {
      // EngineNet may be zero, if the setup is a pure RR setup.
      // Or if an RR calibration is executed before the actual HD simulation, then
      // first an RR controller without an EngineNet is created, and then an ordinary
      // HD controller is created with an enginet, i.e. this method is called twice in that case.
      if (controllerEventArgs.State == ControllerState.Initialized && _controller.EngineNet != null)
      {
        // It is possible to register for the TimeStep event of the EngineNet after the Initialize phase
        _controller.EngineNet.PreTimeStepEvent += PreTimeStepEvent;
      }
    }

    /// <summary>
    /// (*1) Method that is called before every time step
    /// </summary>
    /// <param name="timeN">Time at beginning of time step</param>
    /// <param name="timeNp1">Time at end of time step</param>
    /// <param name="redoCount">redo-counter, incremented when an adaptive time step is redone. Initially zero.</param>
    private void PreTimeStepEvent(DateTime timeN, DateTime timeNp1, int redoCount)
    {
      // In this method we will vary the WeirCoefficient as a function of time.
      // This method can add many kinds of user defined control of structures
      // (or other simulation parameters), as eg. functionality for closing 
      // gates, starting pumps etc.
      
      // Calculate number of hours since simulation start
      DateTime startTime = _controller.Mike1DData.SimulationStart;
      double hours = (timeNp1 - startTime).TotalHours;

      // Vary the weir-coefficient in a 2-hour loop with +/- 0.5, though never smaller than 0.4
      for (int i = 0; i < _myStructures.Count; i++)
      {
        MyStructure myStructure = _myStructures[i];
        myStructure.WeirCoefficient = System.Math.Max(_myStructuresWeirCoefficients[0] + 0.5*System.Math.Sin(2*System.Math.PI*hours/2), 0.4);
      }
    }

    /// <summary>
    /// (*2) Enable output of WeirCoefficent values to default HD result file
    /// </summary>
    private void AddStructureOutput(Mike1DData mike1DData)
    {
      // Loop for the default HD results
      foreach (DHI.Mike1D.ResultDataAccess.ResultSpecification resultSpec in mike1DData.ResultSpecifications)
      {
        if (StringComparer.OrdinalIgnoreCase.Equals(resultSpec.ID, "DefaultHDResults"))
        {
          // Add the WeirCoefficient in the list of what to output
          resultSpec.What.Add(MyStructure.WeirCoefficientQuantity);
        }
      }
    
    }

    #endregion


    #region Old interface methods

    public void Initialize(IList<Mike1DPluginArgument> arguments)
    {
      throw new NotImplementedException();
    }

    public void OnSetupLoaded(Mike1DData mike1DData)
    {
      throw new NotImplementedException();
    }

    #endregion
  }
}