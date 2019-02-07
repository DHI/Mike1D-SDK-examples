clear, clc
dllpath = 'C:/ ?? /M1DSimulator/DHI.M1D/DHI.M1DSimulator/bin/Debug/';
NET.addAssembly([dllpath 'DHI.M1DSimulator.dll']);
import System.*; import DHI.M1DSimulator.*

modelFilePath = 'C:\MIKEUrban_M1D_model.m1dx';
simulator = M1DSimulatorRtc(modelFilePath);
simulator.RainfallRunoffResultDataFilePath = 'C:\RainEvent.res1d';
simulator.CatchmentDischargeResultDataFilePath = 'C:\CatchmentDischarge.res1d';
simulator.ResultBaseFilePath = 'C:\M1DSimulatorExample1.res1d';

% Add watervolume to result file
simulator.SetQuantitiesOfResultSpecification(cell2arrayNET({'WaterVolume', 'Discharge'}));

% Set simulation start and end
from = DateTime(2014, 7, 12, 10, 45, 0); // Original from model: simulator.SimulationStart and simulator.SimulationEnd
to = DateTime(2014, 7, 12, 10, 55, 0);

simulator.PrepareSimulation(from, to);

actionNames = simulator.PidActionIds; % Get action Ids

% Setup PIDs (before running simulation)
simulator.PrepareSetpointPID('action_PID_onActuatorName', 0.1);

% Start simulation
simulator.RunUntil(DateTime(2014, 7, 12, 10, 50, 0));
nodeVolume = simulator.ReadNode('nodeName');
reachTBHBsensor = simulator.ReadSensor('flowsensor');
simulator.SetSetpoint('action_PID_onActuatorName', 0.2);

% Stop simulation and write results
simulator.RunUntil(to);
simulator.Finish();
