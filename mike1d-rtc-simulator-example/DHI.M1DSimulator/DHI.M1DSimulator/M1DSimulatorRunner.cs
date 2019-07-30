using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.ResultDataAccess;
using DHI.Mike1D.RainfallRunoffModule;

namespace DHI.M1DSimulator
{
    public class M1DSimulatorRunner
    {
        protected IMike1DController Simulator;
        protected IDiagnostics Diagnostics;
        public Mike1DData Mike1DData => Simulator.Mike1DData;

        public string RainfallRunoffResultDataFilePath
        {
            set
            {
                if (value != null)
                {
                    Simulator.Mike1DData.UseRRResults = true;
                    Simulator.Mike1DData.RainfallRunoffResultDataFilePath = new FilePath(value);
                }
            }
        }

        public string CatchmentDischargeResultDataFilePath
        {
            set
            {
                Simulator.Mike1DData.UseCatchmentDischargeResults = true;
                Simulator.Mike1DData.CatchmentDischargeResultDataFilePath = new FilePath(value);
            }
        }

        public string ResultBaseFilePath
        {
            set
            {
                if (value != null)
                {
                    Simulator.Mike1DData.ResultBaseFilePath = new FilePath(value);
                    Simulator.Mike1DData.ResultSpecifications[0].Connection.FilePath = new FilePath(value);
                }
            }
            get => Simulator.Mike1DData.ResultBaseFilePath.FullFilePath;
        }

        public string ResultRRFilePath
        {
            set => GetResultSpecification("DefaultRRResults").Connection.FilePath = new FilePath(value);
        }

        public string ResultCDFilePath
        {
            set => GetResultSpecification("CatchmentDischargeRRResults").Connection.FilePath = new FilePath(value);
        }

        public string HotStartFilePath
        {
            set
            {
                if (value != null)
                {
                    var initialConditions = Simulator.Mike1DData.HDParameters.InitialConditions;
                    var hotstartInfo = new HotstartInfo(new FilePath(value));
                    if (initialConditions.UseHotStartSolution)
                        initialConditions.HotstartInfos[0] = hotstartInfo; // Overwrite hotstart info
                    else
                    {
                        initialConditions.UseHotStartSolution = true;
                        initialConditions.HotstartInfos.Add(hotstartInfo);
                    }
                }
            }
        }

        public DateTime SimulationTime { get; set; }
        public DateTime SimulationStart => Simulator.Mike1DData.SimulationStart;
        public DateTime SimulationEnd => Simulator.Mike1DData.SimulationEnd;
        public string GetTimings => Simulator.Timings.PrintTimings(0);
        public static string[] GetAllQuantities => Enum.GetNames(typeof(PredefinedQuantity));

        /// <summary>
        /// Stops the M1D simulator and writes .res1d result files and summary.
        /// </summary>
        public virtual void Finish() => Simulator.Finish();

        public void RunSimulator() => Simulator.Run();


        /// <summary>
        /// Class for simulating a MIKE 1D model.
        /// </summary>
        /// <param name="modelFilePath">M1D model as an m1dx modelfile.</param>
        public M1DSimulatorRunner(string modelFilePath)
        {
            Simulator = new Mike1DController();
            Diagnostics = new Diagnostics("M1DSimulator");
            Simulator.Mike1DData = new Mike1DBridge().Open(Connection.Create(modelFilePath), Diagnostics);
        }

        public virtual void PrepareSimulation(DateTime from, DateTime to)
        {
            SimulationTime = from;
            Simulator.Mike1DData.SimulationStart = from;
            Simulator.Mike1DData.SimulationEnd = to;

            //PrepareCatchmentSimulationTime(from, to);

            if (Simulator.Mike1DData.HDParameters.InitialConditions.UseHotStartSolution)
                Simulator.Mike1DData.HDParameters.InitialConditions.HotstartInfos[0].Time = from;

            IDiagnostics diagnostics = new Diagnostics();

            // Simulator initialization and error handling
            if (diagnostics.ErrorCountRecursive > 0)
                throw new Exception("Loading errors, aborting. " + diagnostics.ErrorsRecursive[0].Message);

            IDiagnostics validation = Simulator.Validate();

            if (validation.ErrorCountRecursive > 0)
                throw new Exception("Validation errors, aborting. " + validation.ErrorsRecursive[0].Message);

            Simulator.Initialize(diagnostics);

            if (diagnostics.ErrorCountRecursive > 0)
                throw new Exception("Initialization errors, aborting. " + diagnostics.ErrorsRecursive[0].Message);

            Simulator.Prepare();
        }

        /// <summary>
        /// Update catchment start and end times to avoid catchments simulating from original start time.
        /// Must run before prepare. Might not work in future versions of M1D.
        /// </summary>
        /// <param name="from"></param>
        /// <param name="to"></param>
        public void PrepareCatchmentSimulationTime(DateTime from, DateTime to)
        {
            if (Simulator.Mike1DData.RainfallRunoffData != null)
                foreach (ICatchment catchment in Simulator.Mike1DData.RainfallRunoffData.Catchments)
                    if (catchment is ICatchmentData catchmentData)
                    {
                        catchmentData.StartTime = from;
                        catchmentData.EndTime = to;
                        if (Simulator.Mike1DData.HDParameters.InitialConditions.UseHotStartSolution)
                            catchmentData.UseHotStartFile = true;
                    }
        }

        public static void PrepareSimulator(ref IMike1DController simulator, IDiagnostics diagnostics)
        {
            // Simulator initialization and error handling
            if (diagnostics.ErrorCountRecursive > 0)
                throw new Exception("Loading errors, aborting. " + diagnostics.ErrorsRecursive[0].Message);

            IDiagnostics validation = simulator.Validate();

            if (validation.ErrorCountRecursive > 0)
                throw new Exception("Validation errors, aborting. " + validation.ErrorsRecursive[0].Message);

            simulator.Initialize(diagnostics);

            if (diagnostics.ErrorCountRecursive > 0)
                throw new Exception("Initialization errors, aborting. " + diagnostics.ErrorsRecursive[0].Message);

            simulator.Prepare();
        }

        /// <summary>
        /// Simulate M1D model with whole loaded event and write results.
        /// </summary>
        public virtual void Run()
        {
            // Simulator.Mike1DData.IsPrepared?
            RunUntil(SimulationEnd);
            Console.WriteLine(GetTimings);
            Finish();
        }

        /// <summary>
        /// Simulate M1D model until time 'to'.
        /// </summary>
        /// <param name="to">Next simulation time.</param>
        public virtual void RunUntil(DateTime to)
        {
            Simulator.EngineNet.RunUntil(to); // ToDo: use Simulator.RunUntil(to) for runoff simulations
            SimulationTime = to;
        }

        public static Mike1DController M1DcontrollerBuilder(string modelFilePath)
        {
            IMike1DController controller = new Mike1DController();
            IDiagnostics diagnostics = new Diagnostics("Mike1DController");
            controller.Mike1DData = new Mike1DBridge().Open(Connection.Create(modelFilePath), diagnostics);

            PrepareSimulator(ref controller, diagnostics);

            return controller as Mike1DController;
        }

        public static Mike1DData GetMike1DData(string modelFilePath)
        {
            return new Mike1DBridge().Open(Connection.Create(modelFilePath), new Diagnostics("Model"));
        }

        public void ClearAllResultSpecifications() => Simulator.Mike1DData.ResultSpecifications.Clear();


        public void SetQuantitiesOfResultSpecification(IEnumerable<string> quantities,
            string resultSpecificationName = "DefaultHDResults")
        {
            var res = GetResultSpecification(resultSpecificationName);
            res.What.Clear();
            res.What.AddRange(quantities.Select(ParseQuantity));
        }

        public void SetResultSpecificationPaths(string resultFileName)
        {
            SetResultSpecificationBasePath(resultFileName);
            ResultRRFilePath = resultFileName + "_RR.res1d";
            ResultCDFilePath = resultFileName + "_CatchmentDischarge.res1d";
        }

        public void SetResultSpecificationBasePath(string resultFileName)
        {
            ResultBaseFilePath = resultFileName + "_Baseline.res1d";
        }

        private ResultSpecification GetResultSpecification(string resultSpecificationName = "DefaultHDResults")
        {
            /*
            Simulator.Mike1DData.ResultSpecifications.Add(new ResultSpecification
            {
                SaveRR = true,
                ID = "DefaultRRResults",
                Mode = ResultSpecification.FileMode.Overwrite,
                Connection = new Connection {FilePath = new FilePath("")},
                StartTime = new DateTime(),
                EndTime = new DateTime(),
                // What = new List<IQuantity>()
                // StoringFrequencyUnitTypes StoringFrequencyType
                // int StoringFrequency
                // IRes1DSpatialFilter DefaultFilter
                // bool Interpolate
                // string Description
             });*/
            return Simulator.Mike1DData.ResultSpecifications.Find(rs =>
                       StringComparer.OrdinalIgnoreCase.Equals(rs.ID, resultSpecificationName))
                   ?? throw new Exception("Resultspecification not found: " + resultSpecificationName);
        }

        public void RemoveResultSpecification(string resultSpecificationName = "DefaultHDResults")
        {
            var r = GetResultSpecification(resultSpecificationName);
            Simulator.Mike1DData.ResultSpecifications.Remove(r);
        }

        public Quantity ParseQuantity(string quantity)
        {
            try
            {
                quantity = Regex.Replace(quantity, " ", "");
                var q = (PredefinedQuantity)Enum.Parse(typeof(PredefinedQuantity), quantity);
                return Quantity.Create(q);
            }
            catch
            {
                throw new Exception("Invalid or unsupported quantity: " + quantity +
                                    ". See valid quantities in M1DSimulatorRunner.GetAllQuantities.");
            }
        }
    }
}