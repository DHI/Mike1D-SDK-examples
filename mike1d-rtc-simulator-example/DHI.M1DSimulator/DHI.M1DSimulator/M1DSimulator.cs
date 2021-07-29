using System;
using System.Linq;
using System.Collections.Generic;
using DHI.Mike1D.Engine;
using DHI.Mike1D.Generic;
using DHI.Mike1D.RainfallRunoffModule;
using DHI.Mike1D.BoundaryModule;

namespace DHI.M1DSimulator
{
    /// <summary>
    /// Class for simulating a MIKE 1D model and extracting variables during simulation.
    /// Optimized for calling from outside .NET enabled script environments like Matlab or IronPython.
    /// Read current simulator quantities, e.g. a discharge or a water level, by passing node ID or reach ID. 
    /// The first call to a new node or reach quantity creates and stores an engine getter.
    /// </summary>
    public class M1DSimulator : M1DSimulatorRunner
    {
        protected ProxyUtil M1DEngine; // Access to M1D engine variables

        // Stored M1D engine variable 'getters'
        private Dictionary<string, Func<double>> _nodeGetters = new Dictionary<string, Func<double>>();
        private Dictionary<string, IVectorGetter<double>> _reachGetters = new Dictionary<string, IVectorGetter<double>>();
        private Dictionary<string, int> _reachPoints = new Dictionary<string, int>();
        private Dictionary<string, Func<double>> _reachStructureGetters = new Dictionary<string, Func<double>>();
        private Dictionary<string, Func<DateTime, double>> _catchmentGetters = new Dictionary<string, Func<DateTime, double>>();
        private Dictionary<string, BoundaryItemRRResult> _catchmentRes1DGetters = new Dictionary<string, BoundaryItemRRResult>();

        /// <inheritdoc />
        /// <summary>
        /// Class for simulating a MIKE 1D model and extracting engine variables during simulation.
        /// </summary>
        /// <param name="modelFilePath">M1D model as an m1dx modelfile.</param>
        public M1DSimulator(string modelFilePath) : base(modelFilePath) { }

        public override void PrepareSimulation(DateTime from, DateTime to)
        {
            base.PrepareSimulation(from, to);

            // Setup proxy for extracting M1D engine variables during simulation
            M1DEngine = new ProxyUtil(Simulator.EngineNet);
        }

        // Nodes
        public void AddNodeGetter(IEnumerable<string> nodes, string quantity = "WaterVolume")
        {
            // Retrieve and store nodeGetters
            var engineNodesToAdd = Simulator.EngineNet.Nodes.Where(n => nodes.Contains(n.ID));
            foreach (var engineNode in engineNodesToAdd)
            {
                var nodeGetter = M1DEngine.Getter(engineNode, ParseQuantity(quantity));
                _nodeGetters[engineNode.ID + quantity] = nodeGetter
                                                         ?? throw new Exception("Node " + engineNode.ID + " has no " + quantity + ", e.g. outlet.");
            }

            ValidateAdd(_nodeGetters.Keys, nodes, quantity);
        }

        public double ReadNode(string node, string quantity = "WaterVolume")
        {
            if (!_nodeGetters.ContainsKey(node + quantity))
                AddNodeGetter(new[] { node }, quantity);

            return _nodeGetters[node + quantity]();
        }

        public double[] ReadNodes(IEnumerable<string> nodes, string quantity = "WaterVolume")
        {
            return nodes.Select(node => ReadNode(node, quantity)).ToArray();
        }

        // Reaches
        public void AddReachGetter(IEnumerable<string> reaches, string quantity = "Discharge")
        {
            // Retrieve and store reachGetters
            var engineReachesToAdd = Simulator.EngineNet.Reaches.Where(r => reaches.Contains(r.LocationSpan.ID) && !r.IsStructureReach);
            foreach (var engineReach in engineReachesToAdd)
            {
                var reachGetter = M1DEngine.Getter(engineReach, ParseQuantity(quantity), out var gridPointIndices);
                var getterKey = engineReach.LocationSpan.ID + quantity;
                _reachGetters[getterKey] = reachGetter
                                           ?? throw new Exception("Reach " + engineReach.LocationSpan.ID + " has no " + quantity + ", e.g. structure.");
                _reachPoints[getterKey] = gridPointIndices.Length - 1;
            }

            //AddStructureReachGetter(reaches, quantity);

            ValidateAdd(_reachGetters.Keys, reaches, quantity);
        }

        public void AddStructureReachGetter(IEnumerable<string> reaches, string quantity = "Discharge")
        {
            var engineReachesToAdd = Simulator.EngineNet.Reaches.Where(r => reaches.Contains(r.LocationSpan.ID) && r.IsStructureReach);
            foreach (var engineReach in engineReachesToAdd)
            {
                var getterKey = engineReach.LocationSpan.ID + quantity;

                for (var gridPointIndex = 0; gridPointIndex < engineReach.GridPoints.Count; gridPointIndex++)
                {
                    //var gridPoint = engineReach.GridPoints[gridPointIndex];
                    //var getter = M1DEngine.Getter(engineReach, 1, ParseQuantity(quantity));
                    var reachGetter = M1DEngine.Getter(engineReach, gridPointIndex, ParseQuantity(quantity));
                    if (reachGetter != null)
                        _reachStructureGetters[getterKey] = reachGetter;
                }

                if (!_reachStructureGetters.ContainsKey(getterKey))
                    throw new Exception("Structure reach " + engineReach.LocationSpan.ID + " has no " + quantity + ".");
            }

            ValidateAdd(_reachStructureGetters.Keys, reaches, quantity);
        }

        public double[] ReadReaches(IEnumerable<string> reaches, string quantity, bool? atFromNode = null)
        {
            return reaches.Select(reach => ReadReach(reach, quantity, atFromNode)).ToArray();
        }

        public double ReadReach(string reach, string quantity, bool? atFromNode)
        {
            // Reads current quantity in reach
            if (IsStructureReach(reach))
                return ReadStructureReach(reach, quantity);
            else
                return ReadReachHDPoints(reach, quantity, atFromNode).Sum();
        }

        // Structure reaches
        public double ReadStructureReach(string reach, string quantity = "GateLevel")
        {
            if (!_reachStructureGetters.ContainsKey(reach + quantity))
                AddStructureReachGetter(new[] { reach }, quantity);

            return _reachStructureGetters[reach + quantity]();
        }

        public bool IsStructureReach(string reachName)
        {
            return reachName.Contains(":");
        }

        public double[] ReadReachHDPoints(string reach, string quantity, bool? atFromNode)
        {
            if (!_reachGetters.ContainsKey(reach + quantity))
                AddReachGetter(new[] { reach }, quantity);

            return ReadReachHDPoints(reach + quantity, atFromNode);
        }

        /// <summary>
        /// Get current quantity at reach HD point.
        /// </summary>
        /// <param name="reach"></param>
        /// <param name="atFromNode"> 
        /// atFromNode = null:  Returns values for all HD points in link.
        /// atFromNode = true:  Returns values for the link HD point nearest its 'from' node.
        /// atFromNode = false: Returns values for the link HD point nearest its 'to' node.
        /// </param>
        /// <returns></returns>
        private double[] ReadReachHDPoints(string reach, bool? atFromNode)
        {
            if (atFromNode.HasValue)
            {
                int pointIndex = (bool)atFromNode ? 0 : _reachPoints[reach];
                return new[] { _reachGetters[reach].GetValue(pointIndex) };
            }

            return _reachGetters[reach].GetValues();
        }

        public double ReadReachHDPointsSum(string reach, string quantity = "WaterVolume")
        {
            return ReadReachHDPoints(reach, quantity, null).Sum();
        }

        public double[] ReadReachesHDPointsSum(IEnumerable<string> reaches, string quantity = "WaterVolume")
        {
            var a = reaches.Select(r => ReadReachHDPointsSum(r, quantity));
            return a.ToArray();
        }

        // Catchments
        public void AddCatchmentGetter(IEnumerable<string> catchments, string quantity = "TotalRunOff")
        {
            var q = ParseQuantity(quantity);

            // Retrieve and store getters
            foreach (ICatchment catchment in Simulator.Mike1DData.RainfallRunoffData.Catchments)
            {
                if (catchments.Contains(catchment.CatchmentName))
                {
                    if (catchment.TimeOffers().Contains(q))
                    {
                        var catchmentGetter = catchment.TimeValueGetter(q);
                        var getterKey = catchment.CatchmentName + quantity;
                        if (!_catchmentGetters.ContainsKey(getterKey))
                            _catchmentGetters.Add(getterKey, catchmentGetter);
                    }

                    //else
                    //throw new Exception("Could not find catchment quantity: " + quantity);
                }
            }

            ValidateAdd(_catchmentGetters.Keys, catchments, quantity);
        }

        public void AddCatchmentsRes1DGetter(IEnumerable<string> catchments, string quantity = "TotalRunOff")
        {
            foreach (var catchment in catchments)
            {
                var getterKey = catchment + quantity;
                if (!_catchmentRes1DGetters.ContainsKey(getterKey))
                {
                    IFilePath filePath;
                    var isDryWeatherFlow = quantity.Equals("CatchmentDischarge");
                    if (isDryWeatherFlow) // ToDo: Test
                        filePath = Simulator.Mike1DData.CatchmentDischargeResultDataFilePath;
                    else
                        filePath = Simulator.Mike1DData.RainfallRunoffResultDataFilePath;

                    var rrItem = BoundaryItemRRResult.Create(filePath, catchment, quantity);
                    rrItem.IsDryWeatherFlow = isDryWeatherFlow;

                    rrItem.Prepare(SimulationStart, SimulationEnd, new Diagnostics());
                    _catchmentRes1DGetters.Add(getterKey, rrItem);
                }
            }

            ValidateAdd(_catchmentRes1DGetters.Keys, catchments, quantity);
        }

        public double[] ReadCatchments(IEnumerable<string> catchments, string quantity = "TotalRunOff")
        {
            return catchments.Select(c => ReadCatchment(c, quantity)).ToArray();
        }

        public double ReadCatchment(string catchment, string quantity = "TotalRunOff")
        {
            if (!_catchmentGetters.ContainsKey(catchment + quantity))
                AddCatchmentGetter(new[] { catchment }, quantity);

            return _catchmentGetters[catchment + quantity](SimulationTime);
        }

        public double ReadCatchmentRes1D(string catchment, string quantity = "TotalRunOff")
        {
            if (!_catchmentRes1DGetters.ContainsKey(catchment))
                AddCatchmentsRes1DGetter(new[] { catchment }, quantity);

            return _catchmentRes1DGetters[catchment + quantity].GetValue(SimulationTime);
        }

        public double[] ReadCatchmentsRes1D(IEnumerable<string> catchments, string quantity = "TotalRunOff")
        {
            return catchments.Select(c => ReadCatchmentRes1D(c, quantity)).ToArray();
        }

        public void ValidateAdd(IEnumerable<string> existing, IEnumerable<string> added, string quantity)
        {
            var unrecognized = added.Select(k => k + quantity).Except(existing).ToArray();
            if (unrecognized.Any())
                throw new Exception("Could not find: " + unrecognized.First());
        }
    }
}