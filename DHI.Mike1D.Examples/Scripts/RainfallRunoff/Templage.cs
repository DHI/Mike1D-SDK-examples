using System;
using System.IO;
using System.Collections.Generic;
using DHI.Generic.MikeZero;
using DHI.PFS;
using DHI.Mike1D.BoundaryModule;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;
using DHI.Mike1D.RainfallRunoffModule;


/*
 * This script file contains two classes:
 *
 * - CatchmentMyModelScript: Sets up user written catchment model and adds it to the simulation
 *
 * - CatcmentMyModel: A user written catchment model
 */

namespace DHI.Mike1D.Catchments.Scripts
{
    /// <summary>
    /// Script class. Methods marked with the <code>[script]</code>
    /// attribute will be executed when the MIKE 1D engine is running
    /// </summary>
    public class CatchmentMyModelScript
    {
        // Conversion from mm/h to m/s
        

        /// <summary>
        /// prepare all models and catchments.

        /// </summary>
        [Script(Order = 1)]
        
        public void AddModifyCatchments(Mike1DData mike1DData)
        {
            // add infiltration
            mike1DData.ResultSpecifications[0].What.Add(Quantity.Create(PredefinedQuantity.TotalInfiltration));
           
           
            IDictionary<string, IQuantity> existingQuantities = new Dictionary<string, IQuantity>();
            foreach (var quantity in mike1DData.ResultSpecifications[0].What)
            {
                existingQuantities.Add(quantity.Id, quantity);
            }

            //var pfsPath = "UserModel.pfs";
            var pfsPath = @"c:\UserModel.pfs";
            var path1 = new DHI.Mike1D.Generic.FilePath(pfsPath);

            IDictionary<string, string> stringParams = null;
            IDictionary<string, double> doubleParams = null;
            HelperFunctions.LoadGloubalParameters(pfsPath, out stringParams, out doubleParams);

            IDictionary<string, ModelStructure> modelStructures = HelperFunctions.LoadModelStructures(pfsPath);
            foreach (var model in modelStructures.Values)
            {
                foreach (var stateVariable in model.StateVariables.Values)
                {
                    if (!existingQuantities.ContainsKey(stateVariable.Id))
                    {
                        existingQuantities.Add(stateVariable.Id, new Quantity(stateVariable.Id, stateVariable.Name, stateVariable.Item, stateVariable.Unit));
                        mike1DData.ResultSpecifications[0].What.Add(existingQuantities[stateVariable.Id]);
                    }
                    stateVariable.StateQuantity = existingQuantities[stateVariable.Id];
                }
            }

            var filePath = @"c:\RR_hotstart.res1d";
            if (stringParams.ContainsKey("hotstartFile"))
            {
                filePath = stringParams["hotstartFile"];
            }
            HotstartInfo hotstartInfo = null;
            var path = new DHI.Mike1D.Generic.FilePath(filePath);
            if (File.Exists(filePath))
            {
                hotstartInfo = new HotstartInfo(path, mike1DData.SimulationStart);
            }
            var myModels = HelperFunctions.LoadCatchments(pfsPath);

            RRHotstartFiles hotstartFiles = null;
            IDiagnostics diag = new Diagnostics("Hotstart");
            var param = new RRParameters();
            if (hotstartInfo != null)
            {
                param.HotstartInfos.Add(hotstartInfo);
                hotstartFiles = RRHotstartFiles.Create(param, diag);
            }

            var typedModels = new List<GenericCatchmentModel>();

            //one Catchment Area
            foreach (var model in myModels)
            {
                string catchType = model.ModelId;
                GenericCatchmentModel typedModel = null;

                switch (catchType)
                {
                    case "*TO FILL IN*":
                        typedModel = new TestModel(model.CatchmentName);
                        break;
                }
                typedModel.PopulateFromModelStructure(modelStructures[catchType]);
                HelperFunctions.CopyUserCatchmentData(model, typedModel);
                typedModel.UseHotStartFile = true;
                if (hotstartFiles != null)
                {
                    typedModel.ApplyHotStart(hotstartFiles);
                }
                typedModels.Add(typedModel);
            }

            foreach (var myModel in typedModels)
            {
                //myModel.TimeStep = TimeSpan.FromSeconds(60);
                var catchment = mike1DData.RainfallRunoffData.Catchments.Find(myModel.CatchmentName);
                if (catchment != null)
                {
                    HelperFunctions.CopyMikeCatchmentData(catchment, myModel);
                    //myModel.Area = 2.5;
                    mike1DData.RainfallRunoffData.Catchments.Remove(catchment);
                }
               
                mike1DData.RainfallRunoffData.Catchments.Add(myModel);
            }

            foreach (ICatchment catchment in mike1DData.RainfallRunoffData.Catchments)
            {
                var modelType = catchment.ModelId;
                var type = catchment.Type();
            }

            foreach (var quantity in mike1DData.ResultSpecifications[0].What)
            {
                var id = quantity.Id;
            }
        }
    }

    /// <summary>
    /// For every time step, the following methods are called:
    /// <code>
    /// PrepareForTimeStep()
    /// UpdateStorage()
    /// UpdateRouting()
    /// FinalizeTimeStep()
    /// CalculateStatistics()
    /// </code>
    /// </para>
    /// </summary>
    public class GenericCatchmentModel : Catchment
    {
        const double mmph2mps = 1 / 3.6 * 1e-6;

        // Quantities for various outputs from this catchment. Used in the Initialize method.
        public static IQuantity DepthQuantity = new Quantity("CDepth", "Depth of water", eumItem.eumIStorageDepth, eumUnit.eumUmeter);
        public static IQuantity SLossQuantity = new Quantity("CStorageDepth", "Storage Depth", eumItem.eumIStorageDepth, eumUnit.eumUmeter);
        public List<CatchmentSourceBoundaryTypes> UsedCatchmentSourceBoundaryTypes { get; internal set; } //TODO - use this variable
        public IList<GlobalSourceBoundaryTypes> UsedGlobalSourceBoundaryTypes { get; internal set; } //TODO - use this variable

        /// <summary>
        /// Create new catchment, given its <paramref name="name"/>.
        /// The <see cref="ICatchment.ModelId"/> will equal the name.
        /// </summary>
        public GenericCatchmentModel(string name) : base(name)
        {

        }

        /// <summary>
        /// Create new catchment, given its <paramref name="modelId"/> and <paramref name="name"/>.
        /// </summary>
        public GenericCatchmentModel(string modelId, string name) : base(modelId, name)
        {
        }

        private IDictionary<string, double> _parameters = new Dictionary<string, double>();
        private IDictionary<string, StateVariable> _stateVariables = new Dictionary<string, StateVariable>();

        public virtual void SetSurfaceParameters()
        {
            Surface.WettingCapacity = 0.05e-3;
            Surface.StorageCapacity = 2.0e-3;
            Surface.Infiltration = new Horton() { F0 = 2 * mmph2mps, Fc = 0.5 * mmph2mps, Kwet = 0.0015, Kdry = 3.0e-5 };
        }

        public void ApplyHotStart(RRHotstartFiles hotstartFiles)
        {
            try
            {
                var h = hotstartFiles.GetCatchmentData(CatchmentName);
                var cont = h.ResultData.Quantities.Count;
                double val = 0;
                foreach (StateVariable variable in StateVariables.Values)
                {
                    try
                    {
                        if (h.GetValue(variable.StateQuantity, out val))
                        {
                            variable.StateValue = val;
                        }
                        else
                        {
                            variable.StateValue = variable.DefaultValue;
                        }
                    }
                    catch
                    {
                        variable.StateValue = variable.DefaultValue;
                    }
                }
            }
            catch
            {
                ResetDefaultStates();
            }
        }

        public void ResetDefaultStates()
        {
            foreach (var stateVariable in StateVariables.Values)
            {
                stateVariable.StateValue = stateVariable.DefaultValue;
            }
        }

        public void PopulateFromModelStructure(ModelStructure modelStructure)
        {
            foreach (var parameter in modelStructure.Parameters)
            {
                Parameters.Add(parameter.Key, parameter.Value);
            }
            foreach (var stateVariable in modelStructure.StateVariables)
            {
                StateVariables.Add(stateVariable.Key, (StateVariable)stateVariable.Value.Clone());
            }
        }

        /// <summary>
        /// The type name of this catchment.
        /// This is a unique name for all catchment model types.
        /// </summary>
        public override string Type()
        {
            return "Generic";
        }

        #region Catchment paramters
        //// This region contains setup parameters for this catchment model,
        //// i.e. parameters provided initially and used through the simulation.

        public CatchmentSurface _surface = new CatchmentSurface();

        public CatchmentSurface Surface
        {
            get { return _surface; }
            set { _surface = value; }
        }

        public IDictionary<string, double> Parameters
        {
            get { return _parameters; }
        }



        #endregion

        #region State variables
        //// This region contains state variables for this catchment model,
        //// i.e. variables that change with each time-step in the model.

        public IDictionary<string, StateVariable> StateVariables
        {
            get { return _stateVariables; }
        }

        #endregion

        #region Helper variables
        // Various helper variables using during evaluation of the catchment runoff

        /// <summary> Effective time step in [s] </summary>
        public double _effectiveTimeStepSeconds;
        /// <summary> Actual rainfall. Unit: [m/s]</summary>
        public double _actualRainfall;
        /// <summary> Potential evaporation. Actual evaporation is limited to water on surface. Unit: [m/s] </summary>
        public double _potentialEvaporation;

        #endregion


        ///<summary>
        /// Initialize Rainfall Runoff model. Sets up static data.
        ///</summary>
        public override void Initialize(IDiagnostics diagnostics)
        {
            // The result output system is based on "offers" and value-getters (delegates),
            // and here we set up which variables are available for output
            // in the result files. Every variable to output must have a matching 
            // quantity and delegate, and the then output system handles the rest. 

            // A catchment must offer TotalRunOff and NetRainfall.
            // The rest is for convenience and result verifications.

            SetSurfaceParameters();

            _offers = new List<IQuantity>();

            _offerDelegates = new List<Func<double>>();

            //// predefined offers
            _offers.Add(Quantity.Create(PredefinedQuantity.TotalRunOff));
            _offerDelegates.Add(() => (_runoff + _additionalFlow));

            _offers.Add(Quantity.Create(PredefinedQuantity.NetRainfall));
            _offerDelegates.Add(() => (_actualRainfall - _potentialEvaporation));

            //// user defined offers
            foreach (var stateVariable in StateVariables)
            {
                _offers.Add(stateVariable.Value.StateQuantity);
                _offerDelegates.Add(() => stateVariable.Value.StateValue);
            }
        }

        #region Handling catchment boundaries/sources, typically rain and evaporation
        // Boundaries like rain and evaporation are specified in the BoundaryModule,
        // and then applied to each catchment. The following methods handle this for rain
        // and evaporation. For rain and evaporation, field variables are available
        // in the Catchment base class. If additional types of boundaries are required
        // in your model, the boundary field variable must be added here, as e.g.
        //    protected IBoundarySource _boundarySourceSunFactor;


        /// <summary>
        /// Get a list of boundary types required by this catchment
        /// </summary>
        public override IList<CatchmentSourceBoundaryTypes> GetRequiredTypes() //TODO - do it more general
        {
            var res = new List<CatchmentSourceBoundaryTypes>();

            // Check if boundaries have already been assigned first.
            if (_boundarySourceRainfall == null)
                res.Add(CatchmentSourceBoundaryTypes.Rainfall);
            if (_boundarySourceEvaporation == null)
                res.Add(CatchmentSourceBoundaryTypes.Evaporation);

            return res;
        }

        /// <summary>
        /// Apply Catchment boundaries to this catchment
        /// </summary>
        public override void ApplyBoundary(CatchmentSourceBoundaryTypes catchmentSourceBoundaryType, IBoundarySource catchmentSourceBoundary) //TODO - do it more general
        {
            switch (catchmentSourceBoundaryType)
            {
                case CatchmentSourceBoundaryTypes.Rainfall:
                    _boundarySourceRainfall = catchmentSourceBoundary;
                    break;
                case CatchmentSourceBoundaryTypes.Evaporation:
                    _boundarySourceEvaporation = catchmentSourceBoundary;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Apply global boundaries to this catchment
        /// </summary>
        public override void ApplyBoundary(GlobalSourceBoundaryTypes type, GlobalGeoLocatedSource geoLocatedSource) //TODO - do it more general
        {
            switch (type)
            {
                case GlobalSourceBoundaryTypes.Rainfall:
                    _boundarySourceRainfall = geoLocatedSource;
                    break;
                case GlobalSourceBoundaryTypes.Evaporation:
                    _boundarySourceEvaporation = geoLocatedSource;
                    break;
                default:
                    throw new ArgumentOutOfRangeException("type");
            }
        }

        #endregion


        #region Time stepping routines

        /// <summary>
        /// Prepares catchment for the next time step.
        /// </summary>
        protected override void PrepareForTimeStep()
        {
            // Update time for this time step. If timestep is dynamic, be sure to 
            // calculate _effectiveTimeStep before calling UpdateTime
            UpdateTime();
            _effectiveTimeStepSeconds = _effectiveTimeStep.TotalSeconds;

            // Transfer current state to Old
            _surface.DepthOld = _surface.Depth;
        }

        /// <summary>
        /// Updates the storage for the catchment.
        /// </summary>
        protected override void UpdateStorage()
        {
            UpdateForcings();

            // Storage calculations is handled by the _surface class
            _surface.PerformTimeStep(_effectiveTimeStepSeconds, _actualRainfall, _potentialEvaporation);
        }

        /// <summary>
        /// This routine updates the boundary/forcing values of rainfall, evaporation etc
        /// This is default implementation
        /// </summary>
        internal virtual void UpdateForcings()
        {
            if (_boundarySourceRainfall != null)
                _actualRainfall = _boundarySourceRainfall.GetAccumulatedValue(_timeOld, _timeNew) / (_effectiveTimeStep.TotalSeconds);

            if (_boundarySourceEvaporation != null)
                // GetAccumulatedValue returns a negative value for evaporation boundary
                _potentialEvaporation = -_boundarySourceEvaporation.GetAccumulatedValue(_timeOld, _timeNew) /
                                        (_effectiveTimeStep.TotalSeconds);
            else
                // Apply a constant evaporation
                _potentialEvaporation = 5e-5 / 3600.0; // 1.2 mm / day

            //Only allow evaporation in dry periods
            if (_actualRainfall > 0)
                _potentialEvaporation = 0;
        }

        /// <summary>
        /// In case any calculations are required after UpdateRouting
        /// </summary>
        protected override void FinalizeTimeStep()
        {
        }
        
        protected virtual void ComputeStep(double E, double P, bool initialStep = false)
        {

        }

        protected override void UpdateRouting()
        {
            double Pl = _actualRainfall * 66 * 60 * 1000;
            double E = _potentialEvaporation * 3600 * 1000;
            double dt = (_timeNew - _timeOld).TotalSeconds;
            Pl = _actualRainfall * dt * 1000;
            E = _potentialEvaporation * dt * 1000;
            ComputeStep(E, Pl);
        }

        public override void Prepare(DateTime simulationStartTime, DateTime simulationEndTime, RRParameters rrpars, IDiagnostics diagnostics)
        {
            base.Prepare(simulationStartTime, simulationEndTime, rrpars, diagnostics);
            ComputeStep(0, 0, true);
        }
        #endregion

        /// <summary>
        /// Volume of water stored in catchment.
        /// </summary>
        public override double VolumeInCatchment()
        {
            double volume = _area * (_surface.Depth + _surface.WettingLoss + _surface.StorageLoss);
            return volume;
        }

        /// /<summary>
        /// In order to get HTML summary right, some statistics must be calculated
        /// </summary>
        protected override void CalculateStatistics()
        {
            double dt = (_timeNew - _timeOld).TotalSeconds;

            double flow = _runoff + _additionalFlow;

            // Figure out min/max flow and its time
            if (flow < _minimumFlow)
            {
                _minimumFlow = flow;
                _timeOfMinimumFlow = _timeNew;
            }
            if (flow > _maximumFlow)
            {
                _maximumFlow = flow;
                _timeOfMaximumFlow = _timeNew;
            }

            // Runoff flow is in [m^3/s]
            double totalRunoffVolume = dt * flow;
            _totalRunoffVolume += totalRunoffVolume;

            // Infiltration and evaporation is in [m/s]
            double totalLossVolume = dt * (_surface.InfiltrationActual + _surface.EvapActual) * _area;
            _totalLossVolume += totalLossVolume;

            // Rainfall is in [m/s]
            double totalRainfallVolume = dt * _actualRainfall * _area;
            _totalRainfallVolume += totalRainfallVolume;

            // AdditionalFlow is constant and given in [m3/s]
            double totalAdditionalInflowVolume = dt * _additionalFlow;
            _totalAdditionalInflowVolume += totalAdditionalInflowVolume;

            if (_yearlyStatistics != null)
            {
                int year = _timeNew.Year;
                RRYearlyStat yearlyStat = GetYearlyStat(year);
                yearlyStat.TotalRunoff += totalRunoffVolume;
                yearlyStat.Losses += totalLossVolume;
                yearlyStat.Inflow += totalRainfallVolume + totalAdditionalInflowVolume;
            }
        }
    }

    internal class DisposablePFS : DHI.PFS.PFSFile, IDisposable
    {
        public DisposablePFS(string path)
            : base(path)
        {
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.Close();
            }
        }
    }

    static class HelperFunctions
    {
        public static IList<GenericCatchmentModel> LoadCatchments(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }
            var modelcatchmentsStructures = new List<GenericCatchmentModel>();
            using (DisposablePFS pfs = new DisposablePFS(fileName))
            {
                PFSSection pfsModelStructures = pfs.GetTarget("RRDefinitions", 1);
                if (pfsModelStructures == null)
                {
                    throw new Exception("Incorrect file format");
                }

                var catchmentCount = pfsModelStructures.GetSectionsCount("Catchment");
                for (int i = 1; i <= catchmentCount; i++)
                {

                    var section = pfsModelStructures.GetSection("Catchment", i);
                    var catchName = section.GetKeyword("name", 1).GetParameter(1).ToString();

                    var modelId = section.GetKeyword("modelId", 1).GetParameter(1).ToString();
                    var area = section.GetKeyword("area", 1).GetParameter(1).ToDouble();
                    var parametrsSection = section.GetSection("Parameters",1);
                    var catchment = new GenericCatchmentModel(modelId, catchName);
                    catchment.Area = area;
                    int paramCount = parametrsSection.GetKeywordsCount();
                    for (int j = 1; j <= paramCount; j++)
                    {
                        var param = parametrsSection.GetKeyword(j);
                        var paramName = param.Name;
                        var value = param.GetParameter(1).ToDouble();
                        catchment.Parameters.Add(paramName, value);
                    }
                    var stateVariablesSection = section.GetSection("StateVariables", 1);
                 
                    int stateVariablesCount = stateVariablesSection.GetKeywordsCount();
                    for (int j = 1; j <= stateVariablesCount; j++)
                    {
                        var param = stateVariablesSection.GetKeyword(j);
                        var paramName = param.Name;
                        
                        var stateVariable = new StateVariable(paramName);
                        stateVariable.StateValue = param.GetParameter(1).ToDouble();
                        stateVariable.DefaultValue = param.GetParameter(2).ToDouble();
                        catchment.StateVariables.Add(paramName, stateVariable);
                    }
                    modelcatchmentsStructures.Add(catchment);
                }
            }
            return modelcatchmentsStructures;
        }
        public static void LoadGloubalParameters(string fileName, out IDictionary<string, string> stringParams, out IDictionary<string, double> doubleParams)
        {
            
            stringParams = new Dictionary<string, string>();
            doubleParams = new Dictionary<string, double>();
            if (!File.Exists(fileName))
            {
                return;
            }

            using (DisposablePFS pfs = new DisposablePFS(fileName))
            {
                PFSSection pfsModelStructures = pfs.GetTarget("RRDefinitions", 1);
                if (pfsModelStructures == null)
                {
                    throw new Exception("Incorrect file format");
                }
                var parametrsSection = pfsModelStructures.GetSection("GlobalVariables",1);
                int paramCount = parametrsSection.GetKeywordsCount();
                for (int j = 1; j <= paramCount; j++)
                {
                    var param = parametrsSection.GetKeyword(j);
                    var paramName = param.Name;
                    var parameterType = param.GetParameter(1).ToString();
                    if (string.Compare(parameterType, "string") == 0)
                    {
                        stringParams.Add(paramName, param.GetParameter(2).ToString());
                    }
                    if (string.Compare(parameterType, "double") == 0)
                    {
                        doubleParams.Add(paramName, param.GetParameter(2).ToDouble());
                    }
                }
            }
        }


                public static IDictionary<string, ModelStructure> LoadModelStructures(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }
            IDictionary<string, ModelStructure> modelStructures = new Dictionary<string, ModelStructure>();
            using (DisposablePFS pfs = new DisposablePFS(fileName))
            {
                PFSSection pfsModelStructures = pfs.GetTarget("RRDefinitions", 1);
                if (pfsModelStructures == null)
                {
                    throw new Exception("Incorrect file format");
                }

                var modelCount = pfsModelStructures.GetSectionsCount("ModelDefinition");
                for (int i = 1; i <= modelCount; i++)
                {
                    var section = pfsModelStructures.GetSection("ModelDefinition", i);
                    var name = section.GetKeyword("ModelName", 1).GetParameter(1).ToString();
                    ModelStructure modelStructure = new ModelStructure(name);
                    modelStructures.Add(name, modelStructure);

                    var parametersCount = section.GetSectionsCount("Parameter");
                    for (int j = 1; j <= parametersCount; j++)
                    {
                        var paramSection = section.GetSection("Parameter", j);
                        var paramName = paramSection.GetKeyword("ParameterName", 1).GetParameter(1).ToString();
                        var defaultValue = paramSection.GetKeyword("DefaultValue", 1).GetParameter(1).ToDouble();
                        modelStructure.Parameters.Add(paramName, defaultValue);
                    }
                    var stateVariablesCount = section.GetSectionsCount("StateVariable");
                    for (int j = 1; j <= stateVariablesCount; j++)
                    {
                        var stateVariableSection = section.GetSection("StateVariable", j);
                        var id = stateVariableSection.GetKeyword("Id", 1).GetParameter(1).ToString();
                        var stateVariableName = stateVariableSection.GetKeyword("Name", 1).GetParameter(1).ToString();
                        
                        var defaultValue = stateVariableSection.GetKeyword("DefaultValue", 1).GetParameter(1).ToDouble();
                        
                        var item = (eumItem)stateVariableSection.GetKeyword("eumItem", 1).GetParameter(1).ToInt();
                        var unit = (eumUnit)stateVariableSection.GetKeyword("eumUnit", 1).GetParameter(1).ToInt();
                        StateVariable stateVariable = new StateVariable(id, stateVariableName, item, unit);
                        modelStructure.StateVariables.Add(id, stateVariable);
                    }
                  
                    //    UsedCatchmentSourceBoundaryTypes = new List<CatchmentSourceBoundaryTypes>();
                    //    UsedGlobalSourceBoundaryTypes = new List<GlobalSourceBoundaryTypes>();
                }
            }
                return modelStructures;
        }

        public static void CopyMikeCatchmentData(ICatchment source, GenericCatchmentModel target)
        { 
            target.CenterPoint = source.CenterPoint;
            target.CatchmentGeometry = source.CatchmentGeometry;
            target.Area = source.Area;
            target.TimeStep = source.TimeStep;
            target.MinTime = source.MinTime;
           
        }

        public static void CopyUserCatchmentData(GenericCatchmentModel source, GenericCatchmentModel target)
        {
            foreach (var param in source.Parameters)
            {
                if (target.Parameters.ContainsKey(param.Key))
                {
                    target.Parameters[param.Key] = source.Parameters[param.Key];
                }
            }
            foreach (var stateVar in source.StateVariables)
            {
                if (target.StateVariables.ContainsKey(stateVar.Key))
                {
                    target.StateVariables[stateVar.Key].DefaultValue = source.StateVariables[stateVar.Key].DefaultValue;
                    target.StateVariables[stateVar.Key].StateValue = source.StateVariables[stateVar.Key].StateValue;
                }
            }

        }
    }

    public class StateVariable : ICloneable
    {
        public StateVariable(string id)
        {
            Id = id;
            Name = id;
            Item = eumItem.eumIItemUndefined;
            Unit = eumUnit.eumUUnitUndefined;
            StateQuantity = null;
        }

        public StateVariable(string id, string name, eumItem item, eumUnit unit)
        {
            Id = id;
            Name = name;
            Item = item;
            Unit = unit;
            StateValue = 0.0;
            DefaultValue = 0.0;
            StateQuantity = null;
        }

        public string Id { get; internal set; }
        public string Name { get; set; }
        public double StateValue { get; set; }
        public double DefaultValue { get; set; }
        public eumItem Item { get; internal set; }
        public eumUnit Unit { get; internal set; }
        public IQuantity StateQuantity { get; set; }
        
        public object Clone()
        {
            StateVariable result = new StateVariable(Id, Name, Item, Unit);
            result.DefaultValue = DefaultValue;
            result.StateQuantity = StateQuantity;
            return result;
        }
    }

    public class ModelStructure
    {
        public ModelStructure(string name)
        {
            Name = name;
            Parameters = new Dictionary<string, double>();
            StateVariables = new Dictionary<string, StateVariable>();
            UsedCatchmentSourceBoundaryTypes = new List<CatchmentSourceBoundaryTypes>();
            UsedGlobalSourceBoundaryTypes = new List<GlobalSourceBoundaryTypes>();
        }
        public string Name { get; set; }
        public IDictionary<string, double> Parameters { get; internal set; }
        public IDictionary<string, StateVariable> StateVariables { get; internal set; }
        public List<CatchmentSourceBoundaryTypes> UsedCatchmentSourceBoundaryTypes { get; internal set; }
        public IList<GlobalSourceBoundaryTypes> UsedGlobalSourceBoundaryTypes { get; internal set; }
    }

    /*========================= Rainfall Runoff Model====================*/
    public class TestModel : GenericCatchmentModel
    {
        /// <summary>
        /// Create new catchment, given its <paramref name="name"/>.
        /// The <see cref="ICatchment.ModelId"/> will equal the name.
        /// </summary>
        public TestModel(string name) : base(name)
        {
        }
                
        /// <summary>
        /// Create new catchment, given its <paramref name="modelId"/> and <paramref name="name"/>.
        /// </summary>
        public TestModel(string modelId, string name) : base(modelId, name)
        {
        }

        /// <summary>
        /// The type name of this catchmentModel type.
        /// This is a unique name for all catchment model types.
        /// </summary>
        public override string Type()
        {
            return "TestModel";
        }

        ///<summary>
        /// Initialize Rainfall Runoff model. Sets up static data.
        ///</summary>
        public override void Initialize(IDiagnostics diagnostics)
        {
            base.Initialize(diagnostics);
            //// more can be add there
        }

        /// <summary>
        /// example of override function 
        /// </summary>
        public override void SetSurfaceParameters()
        {
            base.SetSurfaceParameters();
            Surface.StorageCapacity = 2.5e-3;
        }
        // other override methods can be implemented

        /// <summary>
        /// This routine updates the boundary/forcing values of rainfall, evaporation etc
        /// This is specific
        /// </summary>
        internal override void UpdateForcings()
        {
            if (_boundarySourceRainfall != null)
                _actualRainfall = _boundarySourceRainfall.GetAccumulatedValue(_timeOld, _timeNew) / (_effectiveTimeStep.TotalSeconds);

            if (_boundarySourceEvaporation != null)
                // GetAccumulatedValue returns a negative value for evaporation boundary
                _potentialEvaporation = -_boundarySourceEvaporation.GetAccumulatedValue(_timeOld, _timeNew) /
                                        (_effectiveTimeStep.TotalSeconds);
            else
                // Apply a constant evaporation
                _potentialEvaporation = 0;
        }


        /// <summary>
        /// This is part of the RR engine
        /// </summary>
        protected override void ComputeStep(double e, double p, bool initialStep = false)
        {
            // *TO FILL IN*
        }
    }
}
