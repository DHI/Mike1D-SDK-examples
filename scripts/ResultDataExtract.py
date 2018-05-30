# Download and install IronPython
#   http://ironpython.net/download/
#
# Run from command line like (for the 2.7 version): 
#   "c:\Program Files (x86)\IronPython 2.7\ipy.exe" ResultDataExtract.py
# Then check which command line arguments are required (or check below)

import sys
import clr
from math import *
import array

#===========================================================
# Various helper functions

# Find a given quantity from an IRes1DDataSet
def FindQuantity(dataSet, quantityId):
    numItems = dataSet.DataItems.Count;
    for j in range(numItems):
        if (StringComparer.OrdinalIgnoreCase.Equals(dataSet.DataItems[j].Quantity.Id, quantityId)):
            return dataSet.DataItems[j];
    return None;

# Print out quantities on IRes1DDataSet
def PrintQuantities(dataSet):
    numItems = dataSet.DataItems.Count;
    for j in range(numItems):
        print "'%s'"  % (dataSet.DataItems[j].Quantity.Id);
    return None;

# Find a given quantity on the node with the given nodeId
def FindNodeQuantity(quantityToFind, nodeId):
    node = searcher.FindNode(nodeId);
    if (node != None):
        if (quantityToFind == "-"):
            PrintQuantities(node);
            sys.exit();
        dataItem = FindQuantity(node, quantityToFind);
        if (dataItem != None):
            dataItems.append(dataItem);
            elmtIndex.append(0);
        else:
            print "Could not find quantity '%s' in node '%s'. Available quantities:"  % (quantityToFind, nodeId);
            dataItem = PrintQuantities(node);
            sys.exit();
    else:
        print "Could not find node '%s'"  % (nodeId);
        sys.exit();

# Find a given quantity on the catchment with the given catchId
def FindCatchmentQuantity(quantityToFind, catchId):
    catchment = searcher.FindCatchment(catchId);
    if (catchment != None):
        if (quantityToFind == "-"):
            PrintQuantities(catchment);
            sys.exit();
        dataItem = FindQuantity(catchment, quantityToFind);
        if (dataItem != None):
            dataItems.append(dataItem);
            elmtIndex.append(0);
        else:
            print "Could not find quantity '%s' in catchment '%s'. Available quantities:"  % (quantityToFind, catchId);
            dataItem = PrintQuantities(catchment);
            sys.exit();
    else:
        print "Could not find catchment '%s'"  % (catchId);
        sys.exit();

# Find a given quantity on the reach with the given reachId
# If chainage is -999, all grid points are selected. Otherwise
# the grid point closest to the given chainage is used.
def FindReachQuantity(quantityToFind, reachId, chainage):
    # There can be more than one reach with this reachId, check all
    reaches = searcher.FindReaches(reachId);
    if (reaches.Count == 0):
        print "Could not find reach '%s'"  % (reachId);
        sys.exit();
    if (quantityToFind == "-"):
        PrintQuantities(reaches[0]);
        sys.exit();
        
    diCount = 0;
    if (chainage == -999):
        # All elements of all reaches having that quantity
        for reach in reaches:
            dataItem = FindQuantity(reach, quantityToFind);
            if (dataItem != None):
                for j in range(dataItem.NumberOfElements):
                    dataItems.append(dataItem);
                    elmtIndex.append(j);
                    diCount += 1;
    else:
        # Find grid point closest to given chainage
        minDist = 999999;
        minDataItem = None;
        minElmtIndex = -1;
        for reach in reaches:
            dataItem = FindQuantity(reach, quantityToFind);
            if (dataItem != None):
                # Loop over all grid points in reach dataItem
                for j in range(dataItem.NumberOfElements):
                    dist = abs(reach.GridPoints[dataItem.IndexList[j]].Chainage-chainage);
                    if (dist < minDist):
                        minDist = dist;
                        minDataItem = dataItem;
                        minElmtIndex = j
        if (minDataItem != None):
            dataItems.append(minDataItem);
            elmtIndex.append(minElmtIndex);
            diCount += 1;
    if (diCount == 0):
        print "Could not find quantity '%s' on reach '%s'. Available quantities:"  % (quantityToFind,reachId);
        dataItem = PrintQuantities(reach);
        sys.exit();

def PrintAllReaches():
    for j in range (resultData.Reaches.Count):
        reach = resultData.Reaches[j];
        gridPoints = reach.GridPoints;
        print "%-30s (%9.2f - %9.2f)"  % ("'%s'" % reach.Name, gridPoints[0].Chainage, gridPoints[gridPoints.Count - 1].Chainage);
def PrintAllNodes():
    for j in range (resultData.Nodes.Count):
        node = resultData.Nodes[j];
        print "%s"  % ("'%s'" % node.Id);
def PrintAllCatchments():
    for j in range (resultData.Catchments.Count):
        catchment = resultData.Catchments[j];
        print "%s"  % ("'%s'" % catchment.Id);
#===========================================================

#clr.AddReferenceToFileAndPath("C:\\Work\\main\\Products\\Source\\Mike1D\\src\\bin\\debug\\DHI.Mike1D.ResultDataAccess.dll");
#clr.AddReferenceToFileAndPath("C:\\Work\\main\\Products\\Source\\Mike1D\\src\\bin\\debug\\DHI.Mike1D.Generic.dll");
clr.AddReference("DHI.Mike1D.ResultDataAccess");
clr.AddReference("DHI.Mike1D.Generic");
clr.AddReference("DHI.Generic.MikeZero.DFS");
clr.AddReference("DHI.Generic.MikeZero.EUM");
clr.AddReference("System");
import System
from System import Array, StringComparer
from DHI.Mike1D.ResultDataAccess import *
from DHI.Mike1D.Generic import *
from DHI.Generic.MikeZero import eumUnit, eumItem, eumQuantity
from DHI.Generic.MikeZero.DFS import *

# To use invariant culture (always dots)
#System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

if (len(sys.argv) <= 2):
    print ""
    print "Usage: Extracts data from network result files to text file"
    print "    ipy.exe ResultDataExtract.py resultFile.res1d output.txt [extractPoints]*"
    print ""
    print "Where: ExtractPoints is one or more of:"
    print "    node:WaterLevel:116"
    print "        Extracts water level from node 116"
    print "    reach:Discharge:113l1"
    print "        Extracts discharge from all Q grid points of reach 113l1"
    print "    reach:WaterLevel:102l1:123"
    print "        Extracts water level from reach 102l1, H grid point closest to chanage 123"
    print "    catchment:TotalRunoff:Catchment_2"
    print "        Extracts total runoff from catchment Catchment_2"
    print ""
    print "Output file can have extensions:";
    print "    txt : Write text file output";
    print "    csv : Write text file output in csv format";
    print "    dfs0: Write dfs0 file output";
    print ""
    print "Example:"
    print "    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach:WaterLevel:102l1:0 node:WaterLevel:116"
    print ""
    print "To check which nodes/reaches/catchments are available, leave out the rest"
    print "    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach"
    print ""
    print "To check which quantities are available on a node/reach/catchment, use '-' as quantity"
    print "    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach:-:VIDAA-NED"
    print ""
    print "The ResultDataExtract supports a variety of network and RR result files, i.e "
    print "    res1d         : MIKE 1D network/RR result files"
    print "    res11         : MIKE 11 network/RR result files"
    print "    prf, trf      : MOUSE network result files"
    print "    crf, nrf, nof : MOUSE RR result files"
    sys.exit()

resfilename = sys.argv[1];
outfilename = sys.argv[2];

# Load result file
diagnostics = Diagnostics("Loading file");
resultData = ResultData();
resultData.Connection = Connection.Create(resfilename);
resultData.Load(diagnostics);

# Searcher is helping to find reaches, nodes and catchments
searcher = ResultDataSearch(resultData);

# Vectors with DataItems and element-indeces to extract
dataItems = [];
elmtIndex = [];

# Parse command line arguments
for i in range(3,sys.argv.Count):
    parts = sys.argv[i].split(":");
    failed = True;
    if (parts.Count > 0):
        failed = False;
        if (parts[0] == "reach"):
            if (parts.Count < 3):
                PrintAllReaches();
                sys.exit();
            elif (parts.Count == 3):
                # All grid points in reach
                quantityToFind = parts[1];
                reachId        = parts[2];
                if (reachId == '-'):
                    PrintAllReaches();
                    sys.exit();
                FindReachQuantity(quantityToFind, reachId, -999);
            elif (parts.Count == 4):
                # Search for chainage
                quantityToFind = parts[1];
                reachId        = parts[2];
                if (reachId == '-'):
                    PrintAllReaches();
                    sys.exit();
                FindReachQuantity(quantityToFind, reachId, float(parts[3]));
            else:
                print "Reach argument must have 3 or 4 parts: Could not handle argument %i, %s" % (i,sys.argv[i]);
                sys.exit();
        elif (parts[0] == "node"):
            if (parts.Count < 3):
                PrintAllNodes();
                sys.exit();
            if (parts.Count == 3):
                quantityToFind = parts[1];
                nodeId         = parts[2];
                if (nodeId == "-"):
                    PrintAllNodes();
                    sys.exit();
                FindNodeQuantity(quantityToFind, nodeId);
            else:
                print "Node argument must have 3 parts: Could not handle argument %i, %s" % (i,sys.argv[i]);
                sys.exit();
        elif (parts[0] == "catchment"):
            if (parts.Count < 3):
                PrintAllCatchments();
                sys.exit();
            if (parts.Count == 3):
                quantityToFind = parts[1];
                catchId        = parts[2]
                if (catchId == '-'):
                    PrintAllCatchments();
                    sys.exit();
                FindCatchmentQuantity(quantityToFind, catchId);
            else:
                print "Catchment argument must have 3 parts: Could not handle argument %i, %s" % (i,sys.argv[i]);
                sys.exit();
        else:
            failed = True;
    if (failed):
        print "Could not handle argument %i, %s" % (i,sys.argv[i]);
        sys.exit();

numtimes = resultData.NumberOfTimeSteps;

# Figure out output file type from extension
outFileType = 0; # txt file
if (outfilename.ToLower().EndsWith(".txt")):
    outFileType = 0; # txt file
if (outfilename.ToLower().EndsWith(".csv")):
    outFileType = 1; # csv file
if (outfilename.ToLower().EndsWith(".dfs0")):
    outFileType = 2; # dfs0 file

# Write results to new file

if (outFileType <= 1):
    # Write to text file
    header1Format = "%-20s";
    header2Format = "%15s";
    #chainageFormat = "%15.2f";
    chainageFormat = "%15s";
    chainageFormatcs = "{0,15:0.00}";
    #dataFormat = "%15.6f";
    dataFormat = "%15s";
    dataFormatcs = "{0,15:0.000000}";
    # Only differences between txt and csv are the format specifyers
    if (outFileType == 1): # Change format specifyiers for csv output.
        header1Format = "%s;";
        header2Format = "%s;";
        #chainageFormat = "%.2f;";
        chainageFormat = "%s;";
        chainageFormatcs = "{0:g}";
        #dataFormat = "%.6g;";
        dataFormat = "%s;";
        dataFormatcs = "{0:g}";

    f = open(outfilename, 'w');

    # Write header

    # Type line
    f.write(header1Format % "Type");
    for j in range (dataItems.Count):
        if   (dataItems[j].ItemTypeGroup == ItemTypeGroup.NodeItem):
            f.write(header2Format % "Node"),
        elif (dataItems[j].ItemTypeGroup == ItemTypeGroup.ReachItem):
            f.write(header2Format % "Reach"),
        elif (dataItems[j].ItemTypeGroup == ItemTypeGroup.CatchmentItem):
            f.write(header2Format % "Catchment"),
        else:
            f.write(header2Format % dataItems[j].ItemTypeGroup);
    f.write("\n");
    # Quantity line
    f.write(header1Format % "Quantity")
    for j in range (dataItems.Count):
        f.write(header2Format % dataItems[j].Quantity.Id),
    f.write("\n");
    # Name line
    f.write(header1Format % "Name"),
    for j in range (dataItems.Count):
        if   (dataItems[j].ItemTypeGroup == ItemTypeGroup.NodeItem):
            f.write(header2Format % resultData.Nodes[dataItems[j].NumberWithinGroup].Id),
        elif (dataItems[j].ItemTypeGroup == ItemTypeGroup.ReachItem):
            f.write(header2Format % resultData.Reaches[dataItems[j].NumberWithinGroup].Name),
        elif (dataItems[j].ItemTypeGroup == ItemTypeGroup.CatchmentItem):
            f.write(header2Format % resultData.Catchments[dataItems[j].NumberWithinGroup].Id),
        else:
            f.write(header2Format % "-"),
    f.write("\n");
    # Chainage line
    f.write(header1Format % "Chainage"),
    for j in range (dataItems.Count):
        dataItem = dataItems[j];
        elmti = elmtIndex[j];
        if (dataItem.ItemTypeGroup == ItemTypeGroup.ReachItem):
            f.write(chainageFormat % System.String.Format(chainageFormatcs, resultData.Reaches[dataItem.NumberWithinGroup].GridPoints[dataItem.IndexList[elmti]].Chainage)),
        else:
            f.write(header2Format % "-"),
    f.write("\n");

    # Write data
    for i in range(numtimes):
        f.write(header1Format  % (resultData.TimesList[i].ToString("yyyy-MM-dd HH:mm:ss"))),
        for j in range (dataItems.Count):
            f.write(dataFormat % System.String.Format(dataFormatcs, dataItems[j].GetValue(i,elmtIndex[j]))),
        f.write("\n");
    f.close();

else:

    # Write to dfs0 file

    factory = DfsFactory();
    builder = DfsBuilder.Create("ResultDataExtractor-script", "MIKE SDK", 100);

    # Set up file header
    builder.SetDataType(1);
    builder.SetGeographicalProjection(factory.CreateProjectionUndefined());
    builder.SetTemporalAxis(factory.CreateTemporalNonEqCalendarAxis(eumUnit.eumUsec, resultData.StartTime));
    builder.SetItemStatisticsType(StatType.NoStat);

    # Set up items
    for j in range (dataItems.Count): 
        dataItem = dataItems[j];
        elmti = elmtIndex[j];
        if   (dataItem.ItemTypeGroup == ItemTypeGroup.NodeItem):
            itemName = "Node:%s:%s" % (dataItem.Quantity.Id, resultData.Nodes[dataItem.NumberWithinGroup].Id);
        elif (dataItem.ItemTypeGroup == ItemTypeGroup.ReachItem):
            reach = resultData.Reaches[dataItem.NumberWithinGroup]
            itemName = "reach:%s:%s:%.3f" % (dataItem.Quantity.Id, reach.Name,reach.GridPoints[dataItem.IndexList[elmti]].Chainage);
        elif (dataItem.ItemTypeGroup == ItemTypeGroup.CatchmentItem):
            catchment = resultData.Catchments[dataItem.NumberWithinGroup]
            itemName = "catchment:%s:%s" % (dataItem.Quantity.Id, catchment.Id);
        else:
            itemName = "%s:%s:%s" % (dataItem.ItemTypeGroup, dataItem.Quantity.Id, dataItem.Id);
        item = builder.CreateDynamicItemBuilder();
        item.Set(itemName, dataItem.Quantity.EumQuantity, DfsSimpleType.Float);
        item.SetValueType(DataValueType.Instantaneous);
        item.SetAxis(factory.CreateAxisEqD0());
        builder.AddDynamicItem(item.GetDynamicItemInfo());

    # Create file
    builder.CreateFile(outfilename);
    dfsfile = builder.GetFile();

    # Write data to file
    val = Array.CreateInstance(System.Single, 1)
    for i in range(numtimes):
        time = resultData.TimesList[i].Subtract(resultData.StartTime).TotalSeconds;
        for j in range (dataItems.Count):
            val[0] = dataItems[j].GetValue(i,elmtIndex[j]);
            dfsfile.WriteItemTimeStepNext(time, val);
    dfsfile.Close();

