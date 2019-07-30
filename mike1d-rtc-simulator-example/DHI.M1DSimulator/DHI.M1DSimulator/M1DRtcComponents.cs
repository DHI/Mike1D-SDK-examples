using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace DHI.M1DSimulator
{
    public abstract class SoftSensor
    {
        public string Id { get; set; }
        public virtual double Value { get; protected set; }
        public DateTime Time { get; set; }
        public string Name { get; protected set; }
        public string Quantity { get; protected set; }
        public string Unit { get; protected set; }
        public Selection Selection { get; protected set; }

        public override string ToString() => Id;

        protected SoftSensor(string id)
        {
            Id = Regex.Replace(id, " ", "");
        }

        protected SoftSensor(string name, string quantity, string unit, string type) : this(type + quantity + name)
        {
            Name = name;
            Quantity = quantity;
            Unit = unit;
        }

        protected SoftSensor(string name, string quantity, string unit, Selection selection, string type = "") : this(name, quantity, unit, type)
        {
            Selection = selection;
        }

        protected virtual string GetId()
        {
            return ""; //Regex.Replace(Type + Quantity + Name, " ", "");
        }

        public static double? Constrain(double value, double? min, double? max)
        {
            if (min.HasValue && max.HasValue)
                return System.Math.Min(System.Math.Max(value, min.Value), max.Value);

            return null;
        }
    }

    public class Selection
    {
        public IEnumerable<string> this[string key, string prefix = ""] => _selection.ContainsKey(key) ? _selection[key].Select(s => prefix + s) : new HashSet<string>();

        private Dictionary<string, HashSet<string>> _selection;

        public Selection()
        {
            _selection = new Dictionary<string, HashSet<string>>();
        }

        public Selection(Dictionary<string, HashSet<string>> selection)
        {
            _selection = selection;
        }

        public Selection(string musFilePath)
        {
            _selection = MusFile.ReadMusFile(musFilePath);
        }

        public void Add(string category, IEnumerable<string> ids)
        {
            if (_selection.ContainsKey(category))
                _selection[category].UnionWith(ids);
            else
                _selection.Add(category, new HashSet<string>(ids));
        }

        public void Add(string category, string id)
        {
            Add(category, new[] { id });
        }
    }

    public class Setpoint : SoftSensor, IUpdateableM1D
    {
        public IEnumerable<string> ActionIds;
        public Sensor Sensor;
        public double DefaultValue = 0;

        public Setpoint(string id) : base(id) { }

        public Setpoint(string id, string quantity, string unit) : this(id)
        {
            Quantity = quantity;
            Unit = unit;
        }

        public Setpoint(string id, string quantity, string unit, string sensorId) : this(id, quantity, unit)
        {
            Sensor = new Sensor(sensorId, quantity, unit);
        }

        public void SetValue(double value) => Value = value;

        public void Prepare(M1DSimulatorRtc m1DSimulator)
        {
            m1DSimulator.PrepareSetpointsPID(ActionIds, DefaultValue);
        }

        public void Update(DateTime time, M1DSimulatorRtc m1DSimulator)
        {
            foreach (var actuator in ActionIds)
                m1DSimulator.SetSetpoint(actuator, Value);
        }
    }

    public class Sensor : SoftSensor, IUpdateableM1D
    {
        public Sensor(string id) : base(id) { }

        public Sensor(string id, string quantity, string unit) : this(id)
        {
            Quantity = quantity;
            Unit = unit;
        }

        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            Value = m1Dmodel.ReadSensor(Id);
        }
    }

    public class Volume : SoftSensor, IUpdateableM1D
    {
        public string Type = "Volume";
        public virtual bool IsConstrained { get; protected set; }

        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var linkVolumes = m1Dmodel.ReadReaches(Selection["Link"], Quantity);
            var nodeVolumes = m1Dmodel.ReadNodes(Selection["Node"], Quantity);

            Value = linkVolumes.Sum() + nodeVolumes.Sum();
            Time = time;
        }

        public Volume(string name, Selection selection) : base(name, "Water Volume", "m^3", selection, "Volume") { }
    }

    public class VolumeConstrained : Volume
    {
        public readonly double? MinVolume, MaxVolume;
        public double? ValueConstrained => Constrain(Value, MinVolume, MaxVolume);
        public override bool IsConstrained => ValueConstrained.HasValue;

        public VolumeConstrained(string name, Selection selection, double? minVolume = null, double? maxVolume = null) : base(name, selection)
        {
            MinVolume = minVolume;
            MaxVolume = maxVolume;
        }
    }

    public class Inflow : SoftSensor, IUpdateableM1D
    {
        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var inflows = m1Dmodel.ReadReaches(Selection["Link"], Quantity, atFromNode: false);
            var inflowsCatchments = m1Dmodel.ReadCatchments(Selection["Catchment"]); // 
            var inflowsCatchmentsDWF = m1Dmodel.ReadCatchments(Selection["Catchment"], "Catchment Discharge"); // ToDo check

            Value = inflows.Sum() + inflowsCatchments.Sum() + inflowsCatchmentsDWF.Sum();
            Time = time;
        }

        public Inflow(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Inflow") { }
    }

    public class InflowRes1D : SoftSensor, IUpdateableM1D
    {
        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var inflows = m1Dmodel.ReadReaches(Selection["Link"], Quantity, atFromNode: false);
            var inflowsCatchments = m1Dmodel.ReadCatchmentsRes1D(Selection["Catchment"]);
            //var inflowsCatchmentsDWF = m1Dmodel.ReadCatchmentsRes1D(Selection["Catchment"], "CatchmentDischarge");

            Value = inflows.Sum() + inflowsCatchments.Sum(); // + inflowsCatchmentsDWF.Sum();
            Time = time;
        }

        public InflowRes1D(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Inflow") { }
    }

    public class InflowDWFRes1D : SoftSensor, IUpdateableM1D
    {
        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var inflows = m1Dmodel.ReadReaches(Selection["Link"], Quantity, atFromNode: false);
            var inflowsCatchments = m1Dmodel.ReadCatchmentsRes1D(Selection["Catchment"]);
            var inflowsCatchmentsDWF = m1Dmodel.ReadCatchmentsRes1D(Selection["Catchment"], "CatchmentDischarge");

            Value = inflows.Sum() + inflowsCatchments.Sum() + inflowsCatchmentsDWF.Sum();
            Time = time;
        }

        public InflowDWFRes1D(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Inflow") { }
    }

    public class Inflows : SoftSensor, IUpdateableM1D
    {
        public Dictionary<string, double> Values { get; set; }

        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var inflows = m1Dmodel.ReadReaches(Selection["Link"], Quantity, atFromNode: false);
            Values = Selection["Link"].Zip(inflows, (k, v) => new { k, v }).ToDictionary(x => x.k, x => x.v);
            Time = time;
        }

        public Inflows(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Inflows") { }
    }

    public class Outflow : SoftSensor, IUpdateableM1D
    {
        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var outflows = m1Dmodel.ReadReaches(Selection["Link"], Quantity, atFromNode: true);
            Value = outflows.Sum();
            Time = time;
        }

        public Outflow(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Outflow") { }
    }

    public class Overflow : SoftSensor, IUpdateableM1D
    {
        public void Update(DateTime time, M1DSimulatorRtc m1Dmodel)
        {
            var overflowReaches = Selection["Link"].Union(Selection["Weir", "Weir:"]).ToArray();
            var overflows = m1Dmodel.ReadReaches(overflowReaches, Quantity, atFromNode: false);
            Value = overflows.Sum();
            Time = time;
        }

        public Overflow(string name, Selection selection) : base(name, "Discharge", "m^3/s", selection, "Overflow") { }
    }

    public interface IUpdateableM1D
    {
        void Update(DateTime time, M1DSimulatorRtc m1Dmodel);
    }
}