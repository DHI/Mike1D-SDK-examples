using System;
using DHI.Mike.Install;
using DHI.M1DSimulator;

namespace DHI.M1DSimulator.Test
{
    public class TestExample
    {
        public static void Main()
        {
            if (!MikeImport.Setup(new Version(17, 0)))
                throw new Exception();

            var modelFilePath = @"../../../../../MU/RTCExampleBase.m1dx";
            var simulator = new M1DSimulatorRtc(modelFilePath);


            var from = new DateTime(2014, 7, 12, 10, 45, 0);
            var to = new DateTime(2014, 7, 12, 10, 55, 0);

            simulator.PrepareSimulation(from, to);
            simulator.Run();
        }
    }
}
