# Scripts
This folder contains a number of examples of scripts that can be loaded
and executed by the MIKE 1D engine.

## Enable a script
To enable a script there are different options:

#### Automatic
Put the file beside the setup file and give it the same name, 
keeping the .cs extension, i.e. if you have a mySetup.mhydro, 
name this file mySetup.cs. Similar for a mySetup.mdb, mySetup.m1dx 
or mySetup.sim11.

For a MIKE URBAN setup, if the script has the same name as the .mdb 
file, it will be executed as part of any export step, i.e for all simulations. 
To execute for a single simulation only, and to enable it in the run
step only, give the script the same name as the exported .m1dx file.
 
#### Command line
Enable through command line using the -script parameter:

```
   "c:\Program Files (x86)\DHI\2019\bin\x64\DHI.Mike1D.Application.exe" MySetup.m1dx -script=AdditionalOutput.cs -gui -close
```
   If the .cs file is not in the same folder as the setup file (.m1dx) file, a relative 
   or full path must be provided
```
   "c:\Program Files (x86)\DHI\2019\bin\x64\DHI.Mike1D.Application.exe" MySetup.m1dx -script=..\myscripts\AdditionalOutput.cs -gui -close
```

## Define a script
A script method needs the ```[Script]``` attribute. The name of the method is not important, however the method must have one of the following arguments.

#### Modify a setup
To modify a setup after it has been loaded, add a ```Mike1DData``` parameter

```
    [Script]
    public void SetupLoaded(Mike1DData mike1DData)
```

#### Interact with engine during runtime
To interact with the engine during runtime, add a ```IMike1DController``` parameter

```
    [Script]
    public void ControllerCreated(IMike1DController controller)
```

#### Multiple scripts in same file
There can be multiple script methods in the same file. All methods with the ```[Script]``` attribute will be executed. If the order of the script methods is important, it is possible to order them like ```[Script(Order = 1)]```.


## User defined script parameters
It is possible to define additional parameters to a script, on the form:

```
    [Script]
    public void ModifySetup(Mike1DData mike1DData, IDiagnostics diagnostics, bool addWaterDepth = false, double alpha = 1.0)
    {
       ...
    }
```

A ```diagnostics``` parameter can be added, and used for adding lines to the setup log file. All other parameters are considered user defined. The values of the user defined parameters can be modified from the command line by use of the ```-scriptpars:``` argument:

```
   set m1d="c:\Program Files (x86)\DHI\2019\bin\x64\DHI.Mike1D.Application.exe"
   %m1d% MySetup.m1dx -script=ScriptParameters.cs -scriptpars:addWaterDepth=true;alpha=1.5; -gui -close
```

The script method ```ModifySetup``` defines all the user defined parameters with a default value. In case the ```-scriptpars:``` is not used, or one of the parameters is not added to the ```-scriptpars:```, the default value is used. 

An example script with user defined script parameters can be found in [ScriptParameters.cs](ScriptParameters.cs)
