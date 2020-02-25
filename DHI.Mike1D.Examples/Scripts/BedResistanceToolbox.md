Bed Resistance Toolbox Script
=============================

The Bed Resistance Toolbox Script offers a possibility to make MIKE 1D engine
calculate the bed resistance as a function of the various hydraulic parameters
during the simulation after every time step by applying a user defined
Bed Resistance Equation. In the present script it is assumed that all equations
provide the bed resistance using the Manning's *M* resistance formulation.


This script is a replacement of the Bed Resistance Toolbox available for MIKE 11
engine.

Usage Example
-------------

The wanted bed resistance calculators need to be added in the method
**CreateBedResistanceCalculators()**. For example, to add
*1/M=a\*V<sup>b</sup>* bed resistance velocity *V* dependence (with
*a=0.05*,
*b=1.0*,
minimum resistance *M<sub>min</sub>=10*, and
maximum resistance *M<sub>max</sub>=40*)
on a reach with name *MyReach* from chainage *1000* to *2000* add a line:

```cs
AddFormula(FormulaType.Velocity, 0.05, 1.0, 10, 40, "MyReach", 1000, 2000);
```

To apply the same formula on the whole reach use a simplified method:

```cs
AddFormula(FormulaType.Velocity, 0.05, 1.0, 10, 40, "MyReach");
```

If the formula needs to be applied globally then add a line:

```cs
AddFormula(FormulaType.Velocity, 0.05, 1.0, 10, 40);
```

As in MIKE 11 there are three predefined formula types are:

* **FormulaType.VelocityHydraulicRadius** [*1/M=a\*ln(V\*R)+b*]
* **FormulaType.HydraulicDepth** [*1/M=a\*D<sup>b</sup>*]
* **FormulaType.Velocity** [*1/M=a\*V<sup>b</sup>*]

where *V* is velocity, *R* is hydraulic radius, and *D* is hydraulic depth.
Note that hydraulic depth is defined as an average depth to avoid local deep
parts of a section to control the resistance for the entire cross section.
Hydraulic depth is therefore calculated as: *D=Area/Width*.

The velocity dependence of bed resistance using a velocity-resistance table
can be done using:

```cs
double[] velocityValues = { 0.0, 1.0, 2.0 };
double[] resistanceValues = { 15.0, 20.0, 40.0 };
AddTable(velocityValues, resistanceValues, "MyReach", 1000, 2000);
```

As the name suggests the array *velocityValues* contains velocity values in
unit m/s and the array *resistanceValues* contains Manning's *M* resistance
values in unit m<sup>1/3</sup>/s. Outside the bounds of the *velocityValues* array, 
the nearest *resistanceValues* is used. 
