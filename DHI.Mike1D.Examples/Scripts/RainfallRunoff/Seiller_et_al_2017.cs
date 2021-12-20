/*
 * This script shows how to make a user written custom hydrological catchment model,
 * where the hydrology is based on
 *   https://www.sciencedirect.com/science/article/abs/pii/S0022169417304535?via%3Dihub
 *
 * The hydraulic model calculations take place in the SellierCatchment class,
 * in the methods UpdateStorage and UpdateRouting. 
 *
 * Information of each catchment is loaded from a .pfs file. The name and path of this file can
 * be specified in MIKE+, in "Simulation" - "MIKE 1D Engine Configuration" - "Custom options";
 * insert an option with the name "SeillerPFS" as a string, and enter the file name
 * (relative to MIKE+ setup file (.mupp)). If not specified there, it will look for the file 
 * Seiller_et_al_2017.pfs, which must exist next the the setup file.
 *
 * The Seiller_et_al_2017.pfs currently specifies only one catchment.
 *
 * If a catchment in the SeillerPFS file is also defined in the MIKE 1D simulation (MIKE+),
 * the existing catchment will be replaced. Otherwise a new catchment is added to the simulation.
 *
 * This script file contains the following classes:
 *
 * - SellierCatchmentScript: Sets up the user written catchments
 * - SellierCatchment: The user written catchment model
 * - StateVariable: Handles state of the catchment model
 * - HelperFunctions: Reads parameters from PFS file and other convenient functions
 */

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


namespace DHI.Mike1D.Catchments.Scripts.RRSeiller
{

    /// <summary>
    /// Script class. Methods marked with the <code>[script]</code>
    /// attribute will be executed when the MIKE 1D engine is running
    /// </summary>
    public class SellierCatchmentScript
    {
        // Conversion from mm/h to m/s
        

        /// <summary>
        /// prepare all models and catchments.
        /// </summary>
        [Script(Order = 1)]
        
        public void AddModifyCatchments(Mike1DData mike1DData)
        {
            // Try read SeillerPFS file name from AddtionalData, otherwise use default name
            string pfsPath;
            if (!mike1DData.AdditionalData.TryGetValue("SeillerPFS", out pfsPath))
                pfsPath = "Seiller_et_al_2017.pfs";

            FilePath pfsFilePath = new FilePath(pfsPath, mike1DData.Connection.FilePath);
            if (!File.Exists(pfsFilePath.FullFilePath))
                throw new FileNotFoundException(pfsFilePath.Path);

            IDictionary<string, string> stringParams = null;
            IDictionary<string, double> doubleParams = null;
            HelperFunctions.LoadGlobalParameters(pfsFilePath.FullFilePath, out stringParams, out doubleParams);

            // add infiltration
            mike1DData.ResultSpecifications[0].What.Add(Quantity.Create(PredefinedQuantity.TotalInfiltration));

            // record all existing quantities from the result-specification, to not add them twice
            IDictionary<string, IQuantity> existingQuantities = new Dictionary<string, IQuantity>();
            foreach (var quantity in mike1DData.ResultSpecifications[0].What)
            {
                existingQuantities.Add(quantity.Id, quantity);
            }

            // Add quantities to the result specification, use a dummy catchment to get all quantities
            var scDummy = new SellierCatchment("dummy");
            foreach (var stateVariable in scDummy.StateVariables.Values)
            {
                if (!existingQuantities.ContainsKey(stateVariable.Id))
                {
                    existingQuantities.Add(stateVariable.Id, stateVariable.StateQuantity);
                    mike1DData.ResultSpecifications[0].What.Add(stateVariable.StateQuantity);
                }
                stateVariable.StateQuantity = existingQuantities[stateVariable.Id];
            }

            // Load hotstart data, if present
            RRHotstartFiles hotstartFiles = null;
            if (stringParams.ContainsKey("hotstartFile"))
            {
                string filePath = stringParams["hotstartFile"];
                FilePath path = new FilePath(filePath, mike1DData.Connection.FilePath);
                if (File.Exists(path.FullFilePath))
                {
                    var hotstartInfo = new HotstartInfo(path, mike1DData.SimulationStart);
                    var param = new RRParameters();
                    param.HotstartInfos.Add(hotstartInfo);
                    IDiagnostics diag = new Diagnostics("Hotstart");
                    hotstartFiles = RRHotstartFiles.Create(param, diag);
                }
            }

            // Create new catchments from PFS file
            var myModels = HelperFunctions.LoadCatchments(pfsFilePath.FullFilePath);

            // Apply hotstart values to catchments
            foreach (var model in myModels)
            {
                model.UseHotStartFile = true;
                if (hotstartFiles != null)
                {
                    model.ApplyHotStart(hotstartFiles);
                }
            }

            // Add catchment to setup, remove any existing catchment with the same name
            foreach (var myModel in myModels)
            {
                var catchment = mike1DData.RainfallRunoffData.Catchments.Find(myModel.CatchmentName);
                if (catchment != null)
                {
                    HelperFunctions.CopyMikeCatchmentData(catchment, myModel);
                    mike1DData.RainfallRunoffData.Catchments.Remove(catchment);
                }
                mike1DData.RainfallRunoffData.Catchments.Add(myModel);
            }
        }
    }

    /// <summary>
    /// User written catchment implementation
    ///
    /// For every time step, the following methods are called:
    /// <code>
    /// PrepareForTimeStep()
    /// UpdateStorage()
    /// UpdateRouting()
    /// FinalizeTimeStep()
    /// CalculateStatistics()
    /// </code>
    /// </summary>
    public class SellierCatchment : Catchment
    {
        /// <summary>
        /// Create new catchment, given its <paramref name="name"/>.
        /// The <see cref="ICatchment.ModelId"/> will equal the name.
        /// </summary>
        public SellierCatchment(string name) : base(name) { Init(); }

        /// <summary>
        /// Create new catchment, given its <paramref name="modelId"/> and <paramref name="name"/>.
        /// </summary>
        public SellierCatchment(string modelId, string name) : base(modelId, name) { Init(); }

        /// <summary>
        /// Some default initialization
        /// </summary>
        private void Init()
        {
            _stateVariables.Add("Rbf", RbfState);
            _stateVariables.Add("Rif", RifState);
            _stateVariables.Add("Rof", RofState);
            _stateVariables.Add("So", SoState);
            TimeStep = TimeSpan.FromMinutes(1);
        }

        /// <summary>
        /// Apply hotstart values to catchment, reading results from a previous simulation.
        /// </summary>
        public void ApplyHotStart(RRHotstartFiles hotstartFiles)
        {
            try
            {
                var h = hotstartFiles.GetCatchmentData(CatchmentName);
                if (h != null)
                {
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
                else
                {
                    ResetDefaultStates();
                }
            }
            catch
            {
                ResetDefaultStates();
            }
        }

        /// <summary>
        /// Reset all states to their default value
        /// </summary>
        public void ResetDefaultStates()
        {
            foreach (var stateVariable in StateVariables.Values)
            {
                stateVariable.StateValue = stateVariable.DefaultValue;
            }
        }

        /// <summary>
        /// The type name of this catchment.
        /// This is a unique name for all catchment model types.
        /// </summary>
        public override string Type()
        {
            return "Sellier";
        }

        #region Catchment paramters
        // This region contains setup parameters for this catchment model,
        // i.e. parameters provided initially and used through the simulation.

        public double cmso;
        public double crof;
        public double csib;
        public double drqi;
        public double drsi;
        public double mrin;
        public double rtpc;
        public double trpc;

        #endregion

        #region State variables
        // This region contains state variables for this catchment model,
        // i.e. variables that change with each time-step in the model.

        public StateVariable RbfState = new StateVariable("Rbf", "Rbf", eumItem.eumIDischarge, eumUnit.eumUm3PerSec);
        public StateVariable RifState = new StateVariable("Rif", "Rif", eumItem.eumIDischarge, eumUnit.eumUm3PerSec);
        public StateVariable RofState = new StateVariable("Rof", "Rof", eumItem.eumIDischarge, eumUnit.eumUm3PerSec);
        public StateVariable SoState  = new StateVariable("So",  "So" , eumItem.eumIItemUndefined, eumUnit.eumUUnitUndefined);

        /// <summary>
        /// All state variables, indexed by their id.
        /// </summary>
        public IDictionary<string, StateVariable> StateVariables
        {
            get { return _stateVariables; }
        }
        private IDictionary<string, StateVariable> _stateVariables = new Dictionary<string, StateVariable>();

        #endregion

        #region Helper variables
        // Various helper variables using during evaluation of the catchment runoff

        /// <summary> Actual rainfall. Unit: [m/s]</summary>
        private double _actualRainfall;
        /// <summary> Potential evaporation. Actual evaporation is limited to water on surface. Unit: [m/s] </summary>
        private double _potentialEvaporation;

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

            _offers = new List<IQuantity>();

            _offerDelegates = new List<Func<double>>();

            // predefined offers
            _offers.Add(Quantity.Create(PredefinedQuantity.TotalRunOff));
            _offerDelegates.Add(() => (_runoff + _additionalFlow));

            _offers.Add(Quantity.Create(PredefinedQuantity.NetRainfall));
            _offerDelegates.Add(() => (_actualRainfall - _potentialEvaporation));

            // user defined offers
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
        public override IList<CatchmentSourceBoundaryTypes> GetRequiredTypes()
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
        public override void ApplyBoundary(CatchmentSourceBoundaryTypes catchmentSourceBoundaryType, IBoundarySource catchmentSourceBoundary)
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
        public override void ApplyBoundary(GlobalSourceBoundaryTypes type, GlobalGeoLocatedSource geoLocatedSource)
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

        ///<summary>
        /// Prepare Rainfall Runoff model. Sets up dynamic data so that the model is ready for first time step.
        ///</summary>
        public override void Prepare(DateTime simulationStartTime, DateTime simulationEndTime, RRParameters rrpars, IDiagnostics diagnostics)
        {
            base.Prepare(simulationStartTime, simulationEndTime, rrpars, diagnostics);
            UpdateRouting(true);
        }

        #region Time stepping routines

        /// <summary>
        /// Prepares catchment for the next time step.
        /// </summary>
        protected override void PrepareForTimeStep()
        {
            // Update time for this time step. If timestep is dynamic, be sure to 
            // calculate _effectiveTimeStep before calling UpdateTime
            UpdateTime();
        }

        /// <summary>
        /// Updates the storage for the catchment.
        /// </summary>
        protected override void UpdateStorage()
        {
            UpdateForcings();
        }

        /// <summary>
        /// This routine updates the boundary/forcing values of rainfall, evaporation etc
        /// </summary>
        private void UpdateForcings()
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


        /// <summary> Updates the routing for the catchment. </summary>
        protected override void UpdateRouting()
        {
          UpdateRouting(false);
        }

        /// <summary> Updates the routing for the catchment. </summary>
        protected void UpdateRouting(bool initialStep)
        {
            double dt = (_timeNew - _timeOld).TotalSeconds;

            double Pl;
            double E;
            if (!initialStep)
            {
                Pl = _actualRainfall * dt * 1000;
                E = _potentialEvaporation * dt * 1000;
            }
            else
            {
                Pl = 0;
                E = 0;
            }

            double Rbf = RbfState.StateValue;
            double Rif = RifState.StateValue;
            double Rof = RofState.StateValue;
            double So  = SoState .StateValue;

            // Snow module
            // Snow/Rain split (Ps)
            /////////////////////////////////////////
            double Pg = 0.0;

            // Snow accumulation and melt (G)
            double Me = 0.0;

            // Production part
            // Surface processes (Sf)
            double Esf = System.Math.Min((Me + Pl), E);
            double Inf = 0.0;

            if (So >= mrin)
            {
                Inf = 0.0;
            }
            else
            {
                Inf = ((Me + Pl) - Esf) * (1 - So / mrin);
            }
            double Of = (Me + Pl) - Esf - Inf;
            double Xio = 0.0;
            E = E - Esf;
            double E1 = E;
            // moisture accounting (So)
            So = So + Inf;
            double Is = System.Math.Max(0.0, (So / cmso) * ((So - trpc) / rtpc));
            So = So - Is;
            double helpNum = (System.Math.PI / 2) * ((trpc - So) / trpc);
            double Eso = System.Math.Max(E, E * (System.Math.Cos((System.Math.PI / 2) * ((trpc - So) / trpc))));
            So = System.Math.Max(0.0, So - Eso);
            double If = Is * csib;
            double Perc = 0.0;
            double Xoi = 0.0;
            double Xbi = Is * (1 - csib);
            E = E - Eso;
            double e2 = E;
            // Groundwater (Gw)
            double Egw = 0.0;
            double Bf = Perc;
            double Xib = 0.0;


            // Routing part
            // Overland flow routing (Rof)
            Rof = Rof + Of + Xoi;
            double Qof = System.Math.Pow(Rof, 2) / (Rof + crof);
            Rof = Rof - Qof;
            // Interflow routing(Rif);
            Rif = Rif + If + Xio + Xib;
            double Qif = Rif / drqi;
            Rif = Rif - Qif;
            // Baseflow routing (Rbf)
            Rbf = Rbf + Bf + Xbi;
            double Qbf = Rbf / (drqi * drsi);
            Rbf = Rbf - Qbf;
            // Total flow routing (Rtf)
            double Qsim = Qof + Qif + Qbf;

            // Save states if not if not initial step
            if (!initialStep)
            {
                RbfState.StateValue = Rbf;
                RifState.StateValue = Rif;
                RofState.StateValue = Rof;
                SoState .StateValue = So;
            }
            _runoff = Qsim * Area / (1000 * _timeStep.TotalSeconds);
        }

        /// <summary>
        /// In case any calculations are required after UpdateRouting
        /// </summary>
        protected override void FinalizeTimeStep()
        {
        }

        #endregion

        /// <summary>
        /// Volume of water stored in catchment.
        /// </summary>
        public override double VolumeInCatchment()
        {
            double volume = 0;
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
            double totalLossVolume = dt * (0) * _area;
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

    /// <summary>
    /// State variable class, containing the current value of the state, <see cref="StateValue"/>,
    /// and the default value, <see cref="DefaultValue"/>. It also stores the item and unit definition
    /// of the variable
    /// </summary>
    public class StateVariable : ICloneable
    {
        public StateVariable(string id)
        {
            Id = id;
            Name = id;
            Item = eumItem.eumIItemUndefined;
            Unit = eumUnit.eumUUnitUndefined;
            StateQuantity = new Quantity(Id, Name, Item, Unit);
        }

        public StateVariable(string id, string name, eumItem item, eumUnit unit)
        {
            Id = id;
            Name = name;
            Item = item;
            Unit = unit;
            StateValue = 0.0;
            DefaultValue = 0.0;
            StateQuantity = new Quantity(Id, Name, Item, Unit);
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

    /// <summary>
    /// Static class containing various helper methods
    /// </summary>
    static class HelperFunctions
    {

        /// <summary>
        /// Load catchments from pfs file
        /// </summary>
        public static IList<SellierCatchment> LoadCatchments(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return null;
            }
            var modelcatchments = new List<SellierCatchment>();
            using (DisposablePFS pfs = new DisposablePFS(fileName))
            {
                PFSSection pfsRRDefs = pfs.GetTarget("RRDefinitions", 1);
                if (pfsRRDefs == null)
                    throw new Exception("Incorrect file format");

                var catchmentCount = pfsRRDefs.GetSectionsCount("Catchment");
                for (int i = 1; i <= catchmentCount; i++)
                {

                    var pfsCatchment = pfsRRDefs.GetSection("Catchment", i);
                    var catchName = pfsCatchment.GetKeyword("name", 1).GetParameter(1).ToString();

                    var catchmentType = pfsCatchment.GetKeyword("modelId", 1).GetParameter(1).ToString();

                    ICatchment catchment;
                    switch (catchmentType)
                    {
                        case "HM62095":
                            var scatchment = new SellierCatchment(catchName, catchName);
                            SetupSellierCatchment(scatchment, pfsCatchment);
                            catchment = scatchment;
                            modelcatchments.Add(scatchment);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException("modelId");
                    }

                    catchment.Area = pfsCatchment.GetKeyword("area", 1).GetParameter(1).ToDouble();

                }
            }
            return modelcatchments;
        }

        /// <summary>
        /// Set parameters and state values to catchment
        /// </summary>
        private static void SetupSellierCatchment(SellierCatchment catchment, PFSSection catchmentSection)
        {
            var pfsParams = catchmentSection.GetSection("Parameters", 1);
            catchment.cmso = pfsParams.GetKeyword("cmso").GetParameter(1).ToDouble();
            catchment.crof = pfsParams.GetKeyword("crof").GetParameter(1).ToDouble();
            catchment.csib = pfsParams.GetKeyword("csib").GetParameter(1).ToDouble();
            catchment.drqi = pfsParams.GetKeyword("drqi").GetParameter(1).ToDouble();
            catchment.drsi = pfsParams.GetKeyword("drsi").GetParameter(1).ToDouble();
            catchment.mrin = pfsParams.GetKeyword("mrin").GetParameter(1).ToDouble();
            catchment.rtpc = pfsParams.GetKeyword("rtpc").GetParameter(1).ToDouble();
            catchment.trpc = pfsParams.GetKeyword("trpc").GetParameter(1).ToDouble();

            var pfsStateVars = catchmentSection.GetSection("StateVariables", 1);
            catchment.RbfState.StateValue   = pfsStateVars.GetKeyword("Rbf").GetParameter(1).ToDouble();
            catchment.RifState.StateValue   = pfsStateVars.GetKeyword("Rif").GetParameter(1).ToDouble();
            catchment.RofState.StateValue   = pfsStateVars.GetKeyword("Rof").GetParameter(1).ToDouble();
            catchment.SoState .StateValue   = pfsStateVars.GetKeyword("So" ).GetParameter(1).ToDouble();
            catchment.RbfState.DefaultValue = pfsStateVars.GetKeyword("Rbf").GetParameter(2).ToDouble();
            catchment.RifState.DefaultValue = pfsStateVars.GetKeyword("Rif").GetParameter(2).ToDouble();
            catchment.RofState.DefaultValue = pfsStateVars.GetKeyword("Rof").GetParameter(2).ToDouble();
            catchment.SoState .DefaultValue = pfsStateVars.GetKeyword("So" ).GetParameter(2).ToDouble();

        }

        /// <summary>
        /// Load global parameters from pfs file, and store parameters in two dictionaries
        /// </summary>
        /// <param name="fileName">Name of PFS file</param>
        /// <param name="stringParams">Global string parameters</param>
        /// <param name="doubleParams">Global double parameters</param>
        public static void LoadGlobalParameters(string fileName, out IDictionary<string, string> stringParams, out IDictionary<string, double> doubleParams)
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
                    if (string.Equals(parameterType, "string"))
                    {
                        stringParams.Add(paramName, param.GetParameter(2).ToString());
                    }
                    if (string.Equals(parameterType, "double"))
                    {
                        doubleParams.Add(paramName, param.GetParameter(2).ToDouble());
                    }
                }
            }
        }


        /// <summary>
        /// Copy data from source to target
        /// </summary>
        /// <param name="source">Source catchment</param>
        /// <param name="target">Target catchment</param>
        public static void CopyMikeCatchmentData(ICatchment source, SellierCatchment target)
        { 
            target.CenterPoint = source.CenterPoint;
            target.CatchmentGeometry = source.CatchmentGeometry;
            target.Area = source.Area;
            target.TimeStep = source.TimeStep;
            target.MinTime = source.MinTime;
        }

    }

    /// <summary>
    /// Helper class, making the PFSFile disposable
    /// </summary>
    internal class DisposablePFS : DHI.PFS.PFSFile, IDisposable
    {
        public DisposablePFS(string path) : base(path) { }

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

}
