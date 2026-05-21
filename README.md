# MIKE 1D SDK examples

This repository contains a number of examples of Plugins and [Scripts](DHI.Mike1D.Examples/Scripts) for MIKE 1D. 

Since MIKE 1D is an active code base, its API changes. The master branch contains 
example code for the most recent release of MIKE software. Code for older releases 
can be found in sub-branches.

## Content
* DHI.Mike1D.Examples: Various C# examples of working with MIKE 1D, plugins and scripts.
* data: A bit of test data to play around with. Used by the examples in DHI.Mike1D.Examples.
* m1daExamples: Examples of how to build up a MIKE 1D Additional parameter file, to automatically load and configure plugins.

# MIKE 1D API
An introduction to the MIKE 1D API can be found on:

http://docs.mikepoweredbydhi.com/engine_libraries/mike1d/mike1d_api/

And a overview of the entire MIKE 1D engine and API can be found here:

https://manuals.mikepoweredbydhi.help/latest/General/Class_Library/DHI_MIKE1D/html/R_Project_DHI_Mike1D.htm

# MIKE 1D scripts
A description of how to use a script with a MIKE+ installation can be found in this README:

https://github.com/DHI/Mike1D-SDK-examples/blob/master/DHI.Mike1D.Examples/Scripts/Readme.md

And a specific introduction to calculating bed resistance with a script is provided here as a replacement of the Bed Resistance Toolbox available for MIKE 11 engine:

https://github.com/DHI/Mike1D-SDK-examples/blob/master/DHI.Mike1D.Examples/Scripts/BedResistanceToolbox.md

# MIKE 1D with python
For interacting with MIKE 1D simulations with python, we refer to the MIKE+Py and MIKE IO 1D Python packages.

MIKE+Py is a Python package for interacting with MIKE+ model files to automating simulation execution:

https://github.com/DHI/mikepluspy

To inteact with input and output files in python the MIKE IO 1D package can be found here:

https://dhi.github.io/mikeio1d/
