# Example on how to take one of the result file types that is supported
# by MIKE 1D, and convert to res1d file. This example is converting
# a MOUSE RR file (.crf) to res1d.

import sys
import clr

# The SetupLatest method will make your script find the latest MIKE assemblies at runtime.
clr.AddReference("DHI.Mike.Install, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c513450b5d0bf0bf")
from DHI.Mike.Install import MikeImport, MikeProducts
MikeImport.SetupLatest();
print('Found MIKE in: ' + MikeImport.ActiveProduct().InstallRoot)

clr.AddReference("DHI.Mike1D.ResultDataAccess")
clr.AddReference("DHI.Mike1D.Generic")
from DHI.Mike1D.ResultDataAccess import ResultData, ResultDataSearch
from DHI.Mike1D.Generic import Connection

resultData = ResultData();
resultData.Connection = Connection.Create("DemoBase.crf");
resultData.Load();

# For crf files, set Type to "" if None (null) - work-around for a bug
for c in resultData.Catchments: 
    if (c.Type is None): 
        c.Type = "";

resultData.Connection = Connection.Create("DemoBase-crf.res1d");
resultData.Save();
