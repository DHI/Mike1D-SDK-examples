using System;
using System.Collections.Generic;
using DHI.Math.Expression;
using DHI.Mike1D.ControlModule;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.Plugins;

namespace DHI.Mike1D.Examples.Scripts
{
  /// <summary>
  /// Example script that defines a user defined function
  /// that can be used in the control module expression.
  /// 
  /// The user defined function is a 2D lookup-table,
  /// and can be used in the editor by specifying a
  /// formula on the form:
  /// <code>
  ///    Table2D('tableId', [valueX], [valueY])
  /// </code>
  /// </summary>
  public class ControlFunctionTable2DScript
  {

    /// <summary>
    /// Method called when Mike1DData has been loaded.
    /// 
    /// It creates a <see cref="UserControlFunctionFactory"/>
    /// and adds it to the Mike1DData.ControlData.UserFunctionFactories,
    /// which will support the use of the functions inside the
    /// <see cref="UserControlFunctionFactory"/>.
    ///
    /// It defines all the tables that is used in the model.
    /// </summary>
    [Script]
    public void Initialize(Mike1DData mike1DData)
    {
      // A user function factory can inject custom functions to be used in the MIKE 1D control module
      // This needs only be done once.
      UserControlFunctionFactory ucff = new UserControlFunctionFactory(mike1DData.ControlData);
      mike1DData.ControlData.UserFunctionFactories.Add(ucff);

      // Add tables and associate them with an ID. Then we can use the function Table2D, as:
      //    Table2D('Gate_1Table', [SensorWL], [SensorQ])
      // Table data could be read from a file or similar, but here we insert table data directly. 
      Table2D table = new Table2D();
      table.XAxis       = new double[]   {6.0, 6.5, 6.8, 7.0, 7.2, 7.5, 8.0};
      table.YAxis       = new double[]   {0.7 , 1.0 , 1.3 };
      table.TableValues = new double[,]{ {6.00, 6.00, 6.00},   // x = 6.0
                                         {6.40, 6.30, 6.20},   // x = 6.5
                                         {6.50, 6.40, 6.30},   // x = 6.8
                                         {6.60, 6.45, 6.35},   // x = 7.0
                                         {6.70, 6.50, 6.40},   // x = 7.2
                                         {6.85, 6.60, 6.40},   // x = 7.5
                                         {7.00, 6.65, 6.40},   // x = 8.0
                                       };
      mike1DData.ControlData.TableInfos.Add("Gate_1Table", table);
    }
  }


  /// <summary>
  /// Factory class for creating a user function class from a function name in a control expression
  /// <para>
  /// This factory will handle the function "Table2D('tableId', argX, argY)"
  /// </para>
  /// </summary>
  public class UserControlFunctionFactory : IUserFunctionFactory
  {
    /// <summary>
    /// Default constructor
    /// </summary>
    /// <param name="controlData"></param>
    public UserControlFunctionFactory(ControlData controlData)
    {
      // Reuse TableInfos from control data
      _tableInfos = controlData.TableInfos;
    }

    /// <summary>
    /// Dictionary, where each <see cref="Table2D"/> is identified by a string key.
    /// </summary>
    private Dictionary<string, IAnyTable> _tableInfos;

    /// <summary>
    /// Return an IUserFunction for the function name "Table2D"
    /// </summary>
    /// <param name="name">Name of functions</param>
    /// <param name="arguments">Argument to function</param>
    /// <param name="errors">Errors when trying to create function. Leave as null if no errors found</param>
    public IUserFunction TryCreateUserFunction(string name, IList<ITypedExpression> arguments, out IList<string> errors)
    {
      errors = null;
      switch (name)
      {
        // Check the name of the function. The function will have the form:
        //     Table2D('tableId', argX, argY)
        case "Table2D":
        {
          // There must be three function arguments
          if (arguments.Count != 3)
          {
            AddError(ref errors, string.Format("{0} function requires {1} argument(s) but has {2}", name, 3, arguments.Count));
            return null;
          }
          // The first argument must be a string containing the table ID
          if (!(arguments[0] is Constant<string>))
          {
            AddError(ref errors, "Table2DLookup function requires a first argument as a constant string being the table ID");
            return null;
          }
          // The next two argument must be of type double
          IExpression<double> argX = TryConvertExpression<double>(name, 1, arguments[1], ref errors);
          IExpression<double> argY = TryConvertExpression<double>(name, 2, arguments[2], ref errors);
          // Get the table from the tableid
          string  tableid = ((Constant<string>)arguments[0]).Value;
          Table2D table = null;
          IAnyTable anytable;
          if (!_tableInfos.TryGetValue(tableid, out anytable))
          {
            AddError(ref errors, string.Format("{0}: table id not found: '{1}'", name, tableid));
          }
          else
          {
            table = anytable as Table2D;
            if (table == null)
            {
              AddError(ref errors, string.Format("{0}: table has wrong type of data: '{1}'", name, tableid));
            }
          }
          // Create and return our control function
          return new ControlFunctionTable2D(table, argX, argY);
        }
      }
      // Not "my" function name, just return null
      return null;
    }

    /// <summary>
    /// Add error to list of errors
    /// </summary>
    private static void AddError(ref IList<string> errors, string error)
    {
      if (errors == null) errors = new List<string>();
      errors.Add(error);
    }

    /// <summary>
    /// Try convert <paramref name="argument"/> to specified type {T}.
    /// </summary>
    private static IExpression<T> TryConvertExpression<T>(string name, int argI, ITypedExpression argument, ref IList<string> errors)
    {
      IExpression<T> arg = argument as IExpression<T>;
      if (arg == null)
      {
        if (typeof(T) == typeof(double))
          AddError(ref errors, string.Format("{0} function argument {1} must be a number type, but was '{2}'", name, argI + 1, argument.ResultType.Name));
        else
          AddError(ref errors, string.Format("{0} function argument {1} must be of type '{2}', but was '{3}'", name, argI + 1, typeof(T).Name, argument.ResultType.Name));
      }
      return arg;
    }
  }

  /// <summary>
  /// Control function that looks up in a <see cref="Table2D"/>
  /// <para>
  /// This is the IUserFunction implementation of the Table2D function,
  /// which will be part of the control expression evaluation.
  /// </para>
  /// </summary>
  public class ControlFunctionTable2D : IUserFunction<double>
  {
    private readonly Table2D             _table;
    private readonly IExpression<double> _argumentX;
    private readonly IExpression<double> _argumentY;

    public ControlFunctionTable2D(Table2D table, IExpression<double> argumentX, IExpression<double> argumentY)
    {
      _table    = table;
      _argumentX = argumentX;
      _argumentY = argumentY;
    }

    /// <summary>
    /// This ControlFunction returns values of type double.
    /// </summary>
    public Type ResultType { get { return typeof(double); } }

    /// <summary>
    /// Evaluate control function and return table value
    /// </summary>
    public double Evaluate(IList<ITypedExpression> arguments)
    {
      // Evaluate arguments
      double xVal = _argumentX.Evaluate();
      double yVal = _argumentY.Evaluate();
      // Lookup in table
      double val = _table.Interpolate(xVal, yVal);
      // return table value
      return val;
    }

    /// <summary>
    /// Visitor pattern method - this has to be there.
    /// </summary>
    public T Accept<T>(IUserFunctionVisitor<T> visitor) { return visitor.Visit(this); }

  }

  /// <summary>
  /// 2D table class, handling interpolation in 2D table.
  /// Implementing IAnyTable to be able to go into:
  ///     Mike1DData.ControlData.TableInfos
  /// </summary>
  public class Table2D : IAnyTable
  {
    public double[]  XAxis;
    public double[]  YAxis;
    public double[,] TableValues;

    /// <summary>
    /// Interpolate in table values
    /// </summary>
    public double Interpolate(double x, double y)
    {
      double xFrac, yFrac;
      int xInterval = GetInterval(x, XAxis, out xFrac);
      int yInterval = GetInterval(y, YAxis, out yFrac);
      // Bilinear interpolation
      double v00 = TableValues[xInterval - 1, yInterval - 1];
      double v01 = TableValues[xInterval - 1, yInterval    ];
      double v10 = TableValues[xInterval    , yInterval - 1];
      double v11 = TableValues[xInterval    , yInterval    ];
      double v0  = (1 - yFrac) * v00 + yFrac * v01;
      double v1  = (1 - yFrac) * v10 + yFrac * v11;
      double v   = (1 - xFrac) * v0  + xFrac * v1;
      return v;
    }

    /// <summary>
    /// Finds the interval in a vector where the argument lays in between, and also
    /// the fraction between the two values. Assumes that the vector has at least two elements.
    /// </summary>
    // The result is the interval number, one based, meaning that the arg fullfills:
    //     vector[res-1] <= arg   &&   arg <= vector[res]
    // Also the scale factor for interpolating is returned. For linear interpolation, do
    //    arg = vector[res-1] + fraction*(vector[res]-vector[res-1])
    // or
    //     arg = (1-fraction)*vector[res-1] + fraction*vector[res]
    private static int GetInterval(double x, double[] xvec, out double frac)
    {
      int xInterval = MathUtil.GetInterval(x, xvec, out frac);
      if (xInterval == 0) // Before first value
      {
        // use first value
        xInterval = 1;
        frac      = 0;
      }
      else if (xInterval == xvec.Length) // After last value
      {
        // use last value
        xInterval = xvec.Length - 1;
        frac      = 1;
      }
      return xInterval;
    }
  }

}
