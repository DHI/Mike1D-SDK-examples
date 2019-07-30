using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DHI.Mike1D.ControlModule;
using DHI.Mike1D.Mike1DDataAccess;

namespace DHI.M1DSimulator
{
    /// <summary>
    /// Class that extracts information about the control rules in a M1D model.
    /// </summary>
    public class M1DRtcRules
    {
        public Mike1DData Model { get; }

        public Dictionary<string, HashSet<string>> Actuators { get; }

        public Dictionary<string, string> SensorLocations => Model.ControlData.SensorDescriptions.ToDictionary(s => s.Key, s => s.Value.LocationId ?? s.Value.Location.ID);

        public M1DRtcRules(Mike1DData model)
        {
            Model = model;
            Actuators = GetActuators();
        }

        public override string ToString()
        {
            // ToDo : add PID id and setpoint unit

            var rtcRules = new StringBuilder();

            foreach (var csm in Model.ControlData.ControlStateMachines)
            {
                var csmState = csm.States.First(); // Do not repeat states

                int stateTransistionIndex = 0, stateTransitions = csmState.TransitionTable.StateTransitions.Count;
                var firstStateTransition = true;

                foreach (var stateTransition in csmState.TransitionTable.StateTransitions)
                {
                    if (stateTransition.LogicalCondition != null)
                    {
                        if (stateTransitions > 1)
                        {
                            var conditionExpression = stateTransition.LogicalCondition.Expression.ExpressionString();

                            if (firstStateTransition)
                            {
                                rtcRules.Append($"if;({conditionExpression});");
                                firstStateTransition = false;
                            }
                            else
                                rtcRules.Append($"else if;({conditionExpression});");
                        }
                    }
                    else
                        rtcRules.Append(stateTransitions > 1 ? "else;;" : " ;always;");

                    rtcRules.AppendLine(GetAction(csm.States[stateTransistionIndex++]));
                }
                rtcRules.AppendLine();
            }
            return rtcRules.ToString();
        }

        public Dictionary<string, string> GetActionExpressions()
        {
            var actionExpressions = new Dictionary<string, string>();

            foreach (var csm in Model.ControlData.ControlStateMachines)
            {
                foreach (var action in csm.Actions)
                {
                    if (action is ActionPID actionPID) // ActionChange;
                    {
                        // ActionPidReferenceExpressions.Add(action.Id, action.ReferenceExpression.ExpressionString());
                        actionExpressions.Add(actionPID.Id, actionPID.RegulatedExpression.ExpressionString());
                    }
                }
            }
            return actionExpressions;
        }

        private static string GetAction(State s)
        {
            string a;
            if (s.Action is ActionPID action)
                a = $"{action.Id}; {action.ReferenceExpression.ExpressionString()}; {action.RegulatedExpression.ExpressionString()}";
            else if (s.Action is ActionSet actions)
                a = $"{actions.Id}; {actions.ValueExpression.ExpressionString()};";
            else
                a = $"{s.Action.Id};?;";

            Debug.WriteLine(a);
            return a;
        }

        public Dictionary<string, HashSet<string>> GetActuators()
        {
            var regex = new Regex(Regex.Escape("_"));

            var actuators = new Dictionary<string, HashSet<string>>();
            foreach (var actionExpression in GetActionExpressions())
            {
                var actKey = actionExpression.Value.Trim('[', ']');
                if (!actuators.ContainsKey(actKey))
                    actuators.Add(actKey, new HashSet<string>());

                var actuator = actionExpression.Key.Substring(actionExpression.Key.IndexOf("_on") + 3);
                actuator = regex.Replace(actuator, ":", 1);
                actuators[actKey].Add(actuator);
            }

            return actuators;
        }

        public void Print(string outputFilePath)
        {
            File.WriteAllText(outputFilePath, ToString());
        }
    }
}