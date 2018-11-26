# Scripts
This folder contains a number of examples of scripts that can be loaded
and executed by the MIKE 1D engine.

## Enable a script
To enable a script there are different options:

### Automatic
Put the file beside the setup file and give it the same name, 
keeping the .cs extension, i.e. if you have a mySetup.mhydro, 
name this file mySetup.cs. Similar for a mySetup.mdb, mySetup.m1dx 
or mySetup.sim11.

For a MIKE URBAN setup, if the script has the same name as the .mdb 
file, it will be executed as part of any export step, i.e for all simulations. 
To execute for a single simulation only, and to enable it in the run
step only, give the script the same name as the exported .m1dx file.
 
### Command line
Enable through command line using the -script parameter:

```
   c:\Program Files (x86)\DHI\2017\bin\x64\DHI.Mike1D.Application.exe" MySetup.m1dx -script=AdditionalOutput.cs -gui -close
```
   If the .cs file is not in the same folder as the setup file (.m1dx) file, a relative 
   or full path must be provided
```
   "c:\Program Files (x86)\DHI\2017\bin\x64\DHI.Mike1D.Application.exe" MySetup.m1dx -script=..\myscripts\AdditionalOutput.cs -gui -close
```
