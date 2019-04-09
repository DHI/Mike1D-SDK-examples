using System;

namespace DHI.M1DSimulator
{
    /// <summary>
    /// Example demonstrating how to run a M1D MU model and get/set engine variables.
    /// </summary>
    class M1DSimulatorExample
    {
        public static void M1DSimulatorExample1()
        {
            const string modelFilePath = @"C:\MIKEUrban_M1D_model.m1dx";

            var simulator = new M1DSimulatorRtc(modelFilePath)
            {
                // Change input files if different than model default
                RainfallRunoffResultDataFilePath =  @"C:\RainEvent.res1d",
                CatchmentDischargeResultDataFilePath = @"C:\CatchmentDischarge.res1d",
                ResultBaseFilePath = @"C:\M1DSimulatorExample1.res1d"
                // HotStartFilePath = @""    
            };

            // Add watervolume to default result file
            simulator.SetQuantitiesOfResultSpecification(new[] { "WaterVolume", "Discharge" });

            var allAvailableQuantities = M1DSimulatorRunner.GetAllQuantities;
            
            // Set simulation start and end
            var from = new DateTime(2014, 7, 12, 10, 45, 0);
            var to = new DateTime(2014, 7, 12, 10, 55, 0);

            simulator.PrepareSimulation(from, to);

            var actionNames = simulator.PidActionIds; // Get action names

            // Setup PIDs (before running simulation)
            simulator.PrepareSetpointPID("action_PID_onActuatorName");
            simulator.SetSetpoint("action_PID_onActuatorName", 0.1);

            // Start simulation
            simulator.RunUntil(new DateTime(2014, 7, 12, 10, 50, 0));
            var nodeVolume = simulator.ReadNode("nodeName");
            var reachTBHBsensor = simulator.ReadSensor("flowsensor");
            simulator.SetSetpoint("action_PID_onActuatorName", 0.2);

            simulator.RunUntil(to);

            // Stop simulation and write results
            simulator.Finish();
        }
    }
}
