using System;
using System.Collections.Generic;
using System.Linq;
using DHI.Math.Expression;
using DHI.Mike1D.ControlModule;

namespace DHI.M1DSimulator
{
    public class M1DSimulatorRtc : M1DSimulator
    {
        public string[] SensorNames => Simulator.Mike1DData.ControlData.SensorDescriptions.Select(s => s.Key).ToArray();

        public string[] PidActionIds
        {
            get
            {
                var controlStateMachines = Simulator.Mike1DData.ControlData.ControlStateMachines;
                return (from controlStateMachine in controlStateMachines
                        from action in controlStateMachine.Actions
                        select action.Id).ToArray();
            }
        }

        // Collection of manipulable setpoints in M1D model.
        // Add controllable setpoints using PrepareSetpointPid() before PrepareSimulator().
        private Dictionary<string, Variable<double>> _setpoints = new Dictionary<string, Variable<double>>();

        public M1DSimulatorRtc(string modelFilePath) : base(modelFilePath) { }

        /// <summary>
        /// Read current measurement value from specific sensor.
        /// </summary>
        /// <param name="sensorId"></param>
        /// <returns></returns>
        public double ReadSensor(string sensorId)
        {
            ISensor sensor = Simulator.Mike1DData.ControlData.Sensors.SingleOrDefault(s => s.Id.Equals(sensorId))
                             ?? throw new Exception($"Sensor ''{sensorId}'' not found.");
            return (double)sensor.Value;
        }

        public double[] ReadSensors(IEnumerable<string> sensorIds)
        {
            return sensorIds.Select(ReadSensor).ToArray();
        }

        /// <summary>
        /// Reads current values for all sensors.
        /// </summary>
        public Dictionary<string, double> ReadSensors()
        {
            var sensors = Simulator.Mike1DData.ControlData.Sensors;
            return sensors.ToDictionary(sensor => sensor.Id, sensor => (double)sensor.Value);
        }

        public void PrepareSetpointPID(string actionId, double initialSetpointValue = 0)
        {
            PrepareSetpointsPID(new[] { actionId }, initialSetpointValue);
        }

        /// <summary>
        /// Connect action to setpoint. The M1D action is added to and manipulable through the list '_setpoints'.
        /// The setpoint is reset to 0 when connected. Use SetSetpoint to change setpoint value.
        /// </summary>
        public void PrepareSetpointsPID(IEnumerable<string> actionIds, double initialSetpointValue = 0)
        {
            foreach (ControlStateMachine controlStateMachine in Simulator.Mike1DData.ControlData.ControlStateMachines)
                foreach (IAction action in controlStateMachine.Actions)
                {
                    if (actionIds.Contains(action.Id))
                    {
                        //var actionSet = action as ActionWith2Setters;
                        var actionPID = action as ActionPID
                                        ?? throw new Exception($"Action ID ''{action.Id}'' does not exist in model.");

                        var exprFactory = new DHI.Math.Expression.ExpressionFactory();
                        Variable<double> variable = exprFactory.CreateVariable<double>(action.Id);
                        variable.Value = initialSetpointValue;

                        _setpoints.Add(action.Id, variable);

                        actionPID.ReferenceExpression.Expression = "[" + action.Id + "]";
                        actionPID.ReferenceExpression.AddVariable(variable);
                        actionPID.ReferenceExpression.Compile<double>();
                    }
                }
        }

        /// <summary>
        /// Set setpoint related to a connected control action.
        /// Use ConnectSetpointPID() to connect before setting setpoint.
        /// </summary>
        /// <param name="actionId"></param>
        /// <param name="setpointValue"></param>
        public void SetSetpoint(string actionId, double setpointValue)
        {
            if (_setpoints.ContainsKey(actionId))
                _setpoints[actionId].Value = setpointValue;
            else
                throw new Exception($"Action ID ''{actionId}'' does not exist. Use PidActionIds() to see all action IDs. " +
                                    "Please also connect with ConnectSetpointPid() before running PrepareSimulation().");
        }
    }
}