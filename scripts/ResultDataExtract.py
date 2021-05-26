import sys

#region .NET imports

import clr

clr.AddReference("System")
import System
from System import Array, StringComparer

# The SetupLatest method will make your script find the MIKE assemblies at runtime.
# This is required for MIKE Version 2019 (17.0) and onwards. For previous versions, the
# next three lines must be removed.
clr.AddReference("DHI.Mike.Install, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c513450b5d0bf0bf")
from DHI.Mike.Install import MikeImport, MikeProducts
products = list(MikeImport.InstalledProducts())
product = products[0]
MikeImport.Setup(product);

clr.AddReference("DHI.Mike1D.ResultDataAccess")
clr.AddReference("DHI.Mike1D.Generic")
clr.AddReference("DHI.Generic.MikeZero.DFS")
clr.AddReference("DHI.Generic.MikeZero.EUM")
from DHI.Mike1D.ResultDataAccess import ResultData, ResultDataSearch, Filter, DataItemFilterName, ItemTypeGroup
from DHI.Mike1D.Generic import Diagnostics, Connection
from DHI.Generic.MikeZero import eumUnit, eumItem, eumQuantity
from DHI.Generic.MikeZero.DFS import DfsFactory, DfsBuilder, DfsSimpleType, DataValueType, StatType

#endregion .NET imports


def PrintUsage():
    usageStr = """
Usage: Extracts data from network result files to text file
    ipy.exe ResultDataExtract.py resultFile.res1d output.txt [extractPoints]*

Where: ExtractPoints is one or more of:
    node:WaterLevel:116
        Extracts water level from node 116
    reach:Discharge:113l1
        Extracts discharge from all Q grid points of reach 113l1
    reach:WaterLevel:102l1:123
        Extracts water level from reach 102l1, H grid point closest to chanage 123
    catchment:TotalRunoff:Catchment_2
        Extracts total runoff from catchment Catchment_2

The delimiter in the [extractPoints] can be changed to another character, in case id's
contain semicolons, e.g. using questionmark ?
    node?WaterLevel?hole:116
        Extracts water level from node with id hole:116

Output file can have extensions:
    txt : Write text file output
    csv : Write text file output in csv format
    dfs0: Write dfs0 file output

Example:
    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach:WaterLevel:102l1:0 node:WaterLevel:116

To check which nodes/reaches/catchments are available, leave out the rest
    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach

To check which quantities are available on a node/reach/catchment, use '-' as quantity
    ipy.exe ResultDataExtract.py DemoBase.res1d out.txt reach:-:VIDAA-NED

The ResultDataExtract supports a variety of network and RR result files, i.e
    res1d         : MIKE 1D network/RR result files
    res11         : MIKE 11 network/RR result files
    prf, trf      : MOUSE network result files
    crf, nrf, nof : MOUSE RR result files
"""
    print(usageStr)


#region Enums

class Constants(object):
    ALL_CHAINAGES = -999


class LocationType(object):
    REACH = 'Reach'
    NODE = 'Node'
    CATCHMENT = 'Catchment'


class OutputFileType(object):
    TXT = 'txt'
    CSV = 'csv'
    DFS0 = 'dfs0'
    ALL = '-'

#endregion Enums

#region Command line parsing

class ParsedArgument(object):
    """Class storing information about a parsed argument"""

    def __init__(self,
            locationType, quantityId, locationId, chainage,
            printAllLocations, printQuantities, cannotHandleArgument):

        self.locationType = locationType
        self.quantityId = quantityId
        self.locationId = locationId
        self.chainage = chainage
        self.printAllLocations = printAllLocations
        self.printQuantities = printQuantities
        self.cannotHandleArgument = cannotHandleArgument


class CommandLineParser(object):
    """Class for parsing command line arguments"""

    def __init__(self, arguments):
        self.arguments = arguments
        self.parsedArguments = []
        self.resFileName = None
        self.outFileName = None
        self.outFileType = None

        self.printUsage = False
        self.printAllQuantities = False
        self.cannotHandleArgument = False

        self.Parse()

    def Parse(self):
        arguments = self.arguments
        argumentsCount = len(arguments)

        if argumentsCount <= 1:
            self.printUsage = True
            return

        self.ParseResultFileName()

        if 2 <= argumentsCount <= 3:
            self.printAllQuantities = True
            return

        self.ParseOutputFileName()

        # Parse command line arguments
        for i in range(3, argumentsCount):
            argument = arguments[i]
            cannotHandleArgument = False
            parsedArgument = None

            if argument.lower().startswith("reach"):
                parsedArgument = self.ParseLocation(i, LocationType.REACH, hasChainage=True)

            elif argument.lower().startswith("node"):
                parsedArgument = self.ParseLocation(i, LocationType.NODE)

            elif argument.lower().startswith("catchment"):
                parsedArgument = self.ParseLocation(i, LocationType.CATCHMENT)

            else:
                print("Could not handle argument %i, %s" % (i, argument))
                cannotHandleArgument = True

            self.parsedArguments.append(parsedArgument)
            self.cannotHandleArgument = cannotHandleArgument

            if (cannotHandleArgument or
                parsedArgument.printAllLocations or
                parsedArgument.cannotHandleArgument):
                break


    def ParseResultFileName(self):
        self.resFileName = self.arguments[1]

    def ParseOutputFileName(self):
        outFileName = self.arguments[2]

        # Figure out output file type from extension
        outFileType = OutputFileType.TXT
        if outFileName.lower().endswith(".txt"):
            outFileType = OutputFileType.TXT

        if outFileName.lower().endswith(".csv"):
            outFileType = OutputFileType.CSV

        if outFileName.lower().endswith(".dfs0"):
            outFileType = OutputFileType.DFS0

        if outFileName.lower().endswith("-"):
            outFileType = OutputFileType.ALL

        self.outFileName = outFileName
        self.outFileType = outFileType

    def ParseLocation(self, i, locationType, hasChainage=False):
        argument = self.arguments[i]
        quantityId = None
        locationId = None
        chainage = Constants.ALL_CHAINAGES
        printAllLocations = False
        printQuantities = False
        cannotHandleArgument = False

        parts = self.GetPartsOfArgument(argument, locationType)
        partsCount = len(parts)

        if partsCount < 3:
            printAllLocations = True

        elif partsCount == 3:
            # All grid points in reach
            quantityId = parts[1]
            locationId = parts[2]

        elif partsCount == 4 and hasChainage:
            # Search for chainage
            quantityId = parts[1]
            locationId = parts[2]
            chainage = 0

            if self.IsFloat(parts[3]):
                chainage = float(parts[3])
            else:
                # Could not parse chainage, so it is probably an id with a splitChar in it
                locationId  = parts[2] + splitChar + parts[3]
        else:
            cannotHandleArgument = True
            nParts = '3 or 4' if hasChainage else '3'
            print("%s argument must have %s parts: Could not handle argument %i, %s" % (locationType, nParts, i, argument))

        if locationId == '-':
            printAllLocations = True
        if quantityId == '-':
            printQuantities = True

        return ParsedArgument(locationType, quantityId, locationId, chainage, printAllLocations, printQuantities, cannotHandleArgument)

    def GetPartsOfArgument(self, argument, locationType):
        parts = []
        splitCharPosition = len(locationType)
        if len(argument) > splitCharPosition:
            splitChar = argument[splitCharPosition]
            parts = argument.split(splitChar, splitCharPosition-1)
        return parts

    def IsFloat(self, value):
        try:
            float(value)
            return True
        except:
            return False

#endregion Command line parsing

#region Result finder

class DataEntry(object):
    """Class storing a Mike1D data item and a corresponding element index"""

    def __init__(self, dataItem, elementIndex):
        self.dataItem = dataItem
        self.elementIndex = elementIndex


class ResultFinder(object):
    """Class storing a Mike1D data item and a corresponding element index"""

    def __init__(self, filename, useFilter=None, outputDataItem=True):
        # Load result file
        self.diagnostics = Diagnostics("Loading file")
        self.resultData = ResultData()
        self.resultData.Connection = Connection.Create(filename)
        self.useFilter = useFilter
        self.outputDataItem = outputDataItem

        if useFilter:
            self.SetupFilter()
        else:
            self.Load()

        # Searcher is helping to find reaches, nodes and catchments
        self.searcher = ResultDataSearch(self.resultData)

    def SetupFilter(self):
        """
        Setup the filter for result data object.
        """
        if not self.useFilter:
            return

        self.resultData.LoadHeader(True, self.diagnostics)

        self.dataFilter = Filter()
        self.dataSubFilter = DataItemFilterName(self.resultData)
        self.dataFilter.AddDataItemFilter(self.dataSubFilter)

        self.resultData.Parameters.Filter = self.dataFilter

    def Load(self):
        """
        Load the data from the result file into memory
        """
        if self.useFilter:
            self.resultData.LoadData(self.diagnostics)
        else:
            self.resultData.Load(self.diagnostics)

    def AddLocation(self, locationType, locationId):
        if locationType == LocationType.REACH:
            self.AddReach(locationId)

        if locationType == LocationType.NODE:
            self.AddNode(locationId)

        if locationType == LocationType.CATCHMENT:
            self.AddCatchment(locationId)

    def AddReach(self, reachId):
        self.dataSubFilter.Reaches.Add(reachId)

    def AddNode(self, nodeId):
        self.dataSubFilter.Nodes.Add(nodeId)

    def AddCatchment(self, catchmentId):
        self.dataSubFilter.Catchments.Add(catchmentId)

    def FindQuantity(self, dataSet, quantityId):
        """
        Find a given quantity from an IRes1DDataSet
        """
        numItems = dataSet.DataItems.Count
        dataItems = list(dataSet.DataItems)
        for j in range(numItems):
            dataItem = dataItems[j]
            if StringComparer.OrdinalIgnoreCase.Equals(dataItem.Quantity.Id, quantityId):
                return dataItem
        return None

    def FindQuantityInLocation(self, locationType, quantityId, locationId, chainage=Constants.ALL_CHAINAGES):
        data = None

        if locationType == LocationType.REACH:
            if chainage == Constants.ALL_CHAINAGES:
                data = self.FindReachQuantityAllChainages(quantityId, locationId)
            else:
                data = self.FindReachQuantity(quantityId, locationId, chainage)

        if locationType == LocationType.NODE:
            data = self.FindNodeQuantity(quantityId, locationId)

        if locationType == LocationType.CATCHMENT:
            data = self.FindCatchmentQuantity(quantityId, locationId)

        return data

    def FindReachQuantityAllChainages(self, quantityId, reachId):
        # There can be more than one reach with this reachId, check all
        reaches = self.searcher.FindReaches(reachId)
        if reaches.Count == 0:
            print("Could not find reach '%s'"  % (reachId))
            return None

        dataEntries = []
        # All elements of all reaches having that quantity
        for reach in reaches:
            dataItem = self.FindQuantity(reach, quantityId)
            if dataItem == None:
                continue

            for j in range(dataItem.NumberOfElements):
                dataEntry = self.ConvertDataItemElementToList(dataItem, j)
                dataEntries.append(dataEntry)

        if len(dataEntries) == 0:
            print("Could not find quantity '%s' on reach '%s'."  % (quantityId, reachId))

        return dataEntries

    def FindReachQuantity(self, quantityId, reachId, chainage):
        """
        Find a given quantity on the reach with the given reachId.
        The grid point closest to the given chainage is used.
        """
        # There can be more than one reach with this reachId, check all
        reaches = self.searcher.FindReaches(reachId)
        if reaches.Count == 0:
            print("Could not find reach '%s'"  % (reachId))
            return None

        # Find grid point closest to given chainage
        minDist = 999999
        minDataItem = None
        minElmtIndex = -1
        for reach in reaches:
            dataItem = self.FindQuantity(reach, quantityId)
            if dataItem != None:
                # Loop over all grid points in reach dataItem
                for j in range(dataItem.NumberOfElements):
                    indexList = list(dataItem.IndexList)
                    gridPoints = list(reach.GridPoints)
                    dist = abs(gridPoints[indexList[j]].Chainage-chainage)
                    if dist < minDist:
                        minDist = dist
                        minDataItem = dataItem
                        minElmtIndex = j

        if minDataItem == None:
            print("Could not find quantity '%s' on reach '%s'."  % (quantityId, reachId))

        return [self.ConvertDataItemElementToList(dataItem, minElmtIndex)]

    def FindNodeQuantity(self, quantityId, nodeId):
        """
        Find a given quantity on the node with the given nodeId
        """
        node = self.searcher.FindNode(nodeId)

        if node == None:
            print("Could not find node '%s'"  % (nodeId))
            return None

        dataItem = self.FindQuantity(node, quantityId)
        if dataItem == None:
            print("Could not find quantity '%s' in node '%s'."  % (quantityId, nodeId))

        return [self.ConvertDataItemElementToList(dataItem, 0)]

    def FindCatchmentQuantity(self, quantityId, catchId):
        """
        Find a given quantity on the catchment with the given catchId
        """
        catchment = self.searcher.FindCatchment(catchId)
        if catchment == None:
            print("Could not find catchment '%s'"  % (catchId))
            return None

        dataItem = self.FindQuantity(catchment, quantityId)
        if dataItem == None:
            print("Could not find quantity '%s' in catchment '%s'."  % (quantityId, catchId))

        return [self.ConvertDataItemElementToList(dataItem, 0)]

    def PrintAllQuantities(self):
        """
        Print out quantities on IRes1DDataSet
        """
        print("Available quantity IDs:")
        resultData = self.resultData
        quantities = list(resultData.Quantities)
        for quantity in quantities:
            print("  %s"  % (quantity.Id))

    def PrintQuantities(self, locationType, locationId, chainage=0):
        dataSet = None

        if locationType == LocationType.REACH:
            dataSet = self.searcher.FindReaches(locationId)[0]

        if locationType == LocationType.NODE:
            dataSet = self.searcher.FindNode(locationId)

        if locationType == LocationType.CATCHMENT:
            dataSet = self.searcher.FindCatchment(locationId)

        if dataSet is None:
            return

        dataItems = list(dataSet.DataItems)
        for dataItem in dataItems:
            print("'%s'" % dataItem.Quantity.Id)

    def PrintAllLocations(self, locationType):
        if locationType == LocationType.NODE:
            self.PrintAllNodes()

        if locationType == LocationType.REACH:
            self.PrintAllReaches()

        if locationType == LocationType.CATCHMENT:
            self.PrintAllCatchments()

    def PrintAllReaches(self):
        resultData = self.resultData
        for j in range (resultData.Reaches.Count):
            reach = list(resultData.Reaches)[j]
            gridPoints = list(reach.GridPoints)
            startChainage = gridPoints[0].Chainage
            endChainage = gridPoints[-1].Chainage
            print("'%-30s (%9.2f - %9.2f)'" % (reach.Name, startChainage, endChainage))

    def PrintAllNodes(self):
        resultData = self.resultData
        for j in range (resultData.Nodes.Count):
            node = list(resultData.Nodes)[j]
            print("'%s'" % node.Id)

    def PrintAllCatchments(self):
        resultData = self.resultData
        for j in range (resultData.Catchments.Count):
            catchment = list(resultData.Catchments)[j]
            print("'%s'" % catchment.Id)

    def ConvertDataItemElementToList(self, dataItem, elementIndex):
        """
        Convert dataItem element to list of numbers.
        """
        if self.outputDataItem:
            return DataEntry(dataItem, elementIndex)

        if dataItem is None:
            return None

        data = []
        for j in range(dataItem.NumberOfTimeSteps):
            data.append(dataItem.GetValue(j, elementIndex))
        return data

    def GetTimes(self, toTicks=True):
        """
        Get a list of times
        """
        timesList = list(self.resultData.TimesList)
        if toTicks:
            return list(map(lambda x: x.Ticks, timesList))
        else:
            return timesList

#endregion Result finder

#region Extractor classes

class Extractor(object):
    """Base class for data extractors to specified file format"""

    def __init__(self, outFileName, outputData, resultData, timeStepSkippingNumber=1):
        self.outFileName = outFileName
        self.outputData = outputData
        self.resultData = resultData
        self.timeStepSkippingNumber = timeStepSkippingNumber

    @staticmethod
    def Create(outFileType, outFileName, outputData, resultData, timeStepSkippingNumber=1):
        if outFileType == OutputFileType.TXT:
            return ExtractorTxt(outFileName, outputData, resultData, timeStepSkippingNumber)

        elif outFileType == OutputFileType.CSV:
            return ExtractorCsv(outFileName, outputData, resultData, timeStepSkippingNumber)

        elif outFileType == OutputFileType.DFS0:
            return ExtractorDfs0(outFileName, outputData, resultData, timeStepSkippingNumber)

        elif outFileType == OutputFileType.ALL:
            return ExtractorAll(outFileName, outputData, resultData, timeStepSkippingNumber)

        return None


class ExtractorAll(object):
    """Class which extracts data into all supported file formats"""

    def __init__(self, outFileName, outputData, resultData, timeStepSkippingNumber=1):
        self.allExtractors = [
            ExtractorTxt(outFileName.replace(".-", ".txt"), outputData, resultData, timeStepSkippingNumber),
            ExtractorCsv(outFileName.replace(".-", ".csv"), outputData, resultData, timeStepSkippingNumber),
            ExtractorDfs0(outFileName.replace(".-", ".dfs0"), outputData, resultData, timeStepSkippingNumber)
        ]

    def Export(self):
        for extractor in self.allExtractors:
            extractor.Export()


class ExtractorTxt(Extractor):
    """Class which extracts data to text file"""

    def Export(self):
        self.f = open(self.outFileName, 'w')
        self.SetOutputFormat()
        self.WriteItemType()
        self.WriteQuantity()
        self.WriteName()
        self.WriteChainage()
        self.WriteDataItems()
        self.f.close()

    def SetOutputFormat(self):
        self.header1Format = "%-20s"
        self.header2Format = "%15s"
        self.chainageFormat = "%15s"
        self.chainageFormatcs = "{0,15:0.00}"
        self.dataFormat = "%15s"
        self.dataFormatcs = "{0,15:0.000000}"

    def WriteItemType(self):
        outputData, f = self.outputData, self.f
        header1Format, header2Format = self.header1Format, self.header2Format

        f.write(header1Format % "Type")
        for dataEntry in outputData:
            itemTypeGroup = dataEntry.dataItem.ItemTypeGroup

            if itemTypeGroup == ItemTypeGroup.ReachItem:
                f.write(header2Format % "Reach")

            elif itemTypeGroup == ItemTypeGroup.NodeItem:
                f.write(header2Format % "Node")

            elif itemTypeGroup == ItemTypeGroup.CatchmentItem:
                f.write(header2Format % "Catchment")

            else:
                f.write(header2Format % itemTypeGroup)
        f.write("\n")

    def WriteQuantity(self):
        outputData, f = self.outputData, self.f
        header1Format, header2Format = self.header1Format, self.header2Format

        f.write(header1Format % "Quantity")
        for dataEntry in outputData:
            f.write(header2Format % dataEntry.dataItem.Quantity.Id),
        f.write("\n")

    def WriteName(self):
        outputData, f = self.outputData, self.f
        resultData = self.resultData
        header1Format, header2Format = self.header1Format, self.header2Format

        f.write(header1Format % "Name"),
        nodes = list(resultData.Nodes)
        reaches = list(resultData.Reaches)
        catchments = list(resultData.Catchments)
        for dataEntry in outputData:
            dataItem = dataEntry.dataItem
            itemTypeGroup = dataItem.ItemTypeGroup
            numberWithinGroup = dataItem.NumberWithinGroup

            if itemTypeGroup == ItemTypeGroup.ReachItem:
                f.write(header2Format % reaches[numberWithinGroup].Name),

            elif itemTypeGroup == ItemTypeGroup.NodeItem:
                f.write(header2Format % nodes[numberWithinGroup].Id),

            elif itemTypeGroup == ItemTypeGroup.CatchmentItem:
                f.write(header2Format % catchments[numberWithinGroup].Id),

            else:
                f.write(header2Format % "-"),
        f.write("\n")

    def WriteChainage(self):
        outputData, f = self.outputData, self.f
        resultData = self.resultData
        header1Format, header2Format = self.header1Format, self.header2Format
        chainageFormat, chainageFormatcs = self.chainageFormat, self.chainageFormatcs

        f.write(header1Format % "Chainage"),
        for dataEntry in outputData:
            dataItem = dataEntry.dataItem
            elementIndex = dataEntry.elementIndex

            if dataItem.ItemTypeGroup != ItemTypeGroup.ReachItem or dataItem.IndexList is None:
                f.write(header2Format % "-"),
                continue

            indexList = list(dataItem.IndexList)
            reaches = list(resultData.Reaches)
            gridPoints = list(reaches[dataItem.NumberWithinGroup].GridPoints)
            gridPointIndex = indexList[elementIndex]
            f.write(chainageFormat % System.String.Format(chainageFormatcs, gridPoints[gridPointIndex].Chainage)),

        f.write("\n")

    def WriteDataItems(self):
        outputData, f = self.outputData, self.f
        resultData = self.resultData
        header1Format, dataFormat, dataFormatcs = self.header1Format, self.dataFormat, self.dataFormatcs

        times = list(resultData.TimesList)
        # Write data
        for timeStepIndex in range(resultData.NumberOfTimeSteps):
            if (timeStepIndex % self.timeStepSkippingNumber != 0):
                continue

            time = times[timeStepIndex]
            f.write(header1Format  % (time.ToString("yyyy-MM-dd HH:mm:ss"))),
            for dataEntry in outputData:
                dataItem = dataEntry.dataItem
                elementIndex = dataEntry.elementIndex
                value = dataItem.GetValue(timeStepIndex, elementIndex)
                f.write(dataFormat % System.String.Format(dataFormatcs, value)),
            f.write("\n")


class ExtractorCsv(ExtractorTxt):
    """Class which extracts data to comma separated value (CSV) file"""

    separator = ';'

    def SetOutputFormat(self):
        self.header1Format = "%s;"
        self.header2Format = "%s;"
        self.chainageFormat = "%s;"
        self.chainageFormatcs = "{0:g}"
        self.dataFormat = "%s;"
        self.dataFormatcs = "{0:g}"

    def WriteItemType(self):
        # Write CSV separator type
        self.f.write("sep=%s\n" % self.separator)
        ExtractorTxt.WriteItemType(self)


class ExtractorDfs0(Extractor):
    """Class which extracts data to dfs0 file format"""

    def Export(self):
        self.factory = DfsFactory()
        self.builder = self.CreateDfsBuilder()
        self.DefineDynamicDataItems()
        self.WriteDataItems()

    def CreateDfsBuilder(self):
        resultData = self.resultData
        factory = self.factory

        builder = DfsBuilder.Create("ResultDataExtractor-script", "MIKE SDK", 100)

        # Set up file header
        builder.SetDataType(1)
        builder.SetGeographicalProjection(factory.CreateProjectionUndefined())
        builder.SetTemporalAxis(factory.CreateTemporalNonEqCalendarAxis(eumUnit.eumUsec, resultData.StartTime))
        builder.SetItemStatisticsType(StatType.NoStat)

        return builder

    def DefineDynamicDataItems(self):
        outputData = self.outputData
        resultData = self.resultData
        builder = self.builder

        for dataEntry in outputData:
            dataItem = dataEntry.dataItem
            elementIndex = dataEntry.elementIndex

            quantity = dataItem.Quantity
            itemTypeGroup = dataItem.ItemTypeGroup
            numberWithinGroup = dataItem.NumberWithinGroup

            reaches = list(resultData.Reaches)
            nodes = list(resultData.Nodes)
            catchments = list(resultData.Catchments)

            if itemTypeGroup == ItemTypeGroup.ReachItem:
                reach = reaches[numberWithinGroup]
                gridPointIndex = dataItem.IndexList[elementIndex]
                gridPoints = list(reach.GridPoints)
                chainage = gridPoints[gridPointIndex].Chainage
                itemName = "reach:%s:%s:%.3f" % (quantity.Id, reach.Name, chainage)

            elif itemTypeGroup == ItemTypeGroup.NodeItem:
                node = nodes[numberWithinGroup]
                itemName = "node:%s:%s" % (quantity.Id, node.Id)

            elif itemTypeGroup == ItemTypeGroup.CatchmentItem:
                catchment = catchments[numberWithinGroup]
                itemName = "catchment:%s:%s" % (quantity.Id, catchment.Id)

            else:
                itemName = "%s:%s:%s" % (itemTypeGroup, quantityId, dataItem.Id)

            item = builder.CreateDynamicItemBuilder()
            item.Set(itemName, dataItem.Quantity.EumQuantity, DfsSimpleType.Float)
            item.SetValueType(DataValueType.Instantaneous)
            item.SetAxis(self.factory.CreateAxisEqD0())
            builder.AddDynamicItem(item.GetDynamicItemInfo())

    def WriteDataItems(self):
        outputData = self.outputData
        resultData = self.resultData
        builder = self.builder

        # Create file
        builder.CreateFile(self.outFileName)
        dfsfile = builder.GetFile()
        times = list(resultData.TimesList)

        # Write data to file
        val = Array.CreateInstance(System.Single, 1)
        for timeStepIndex in range(resultData.NumberOfTimeSteps):
            if (timeStepIndex % self.timeStepSkippingNumber != 0):
                continue

            time = times[timeStepIndex].Subtract(resultData.StartTime).TotalSeconds
            for dataEntry in outputData:
                dataItem = dataEntry.dataItem
                elementIndex = dataEntry.elementIndex

                val[0] = dataItem.GetValue(timeStepIndex, elementIndex)
                dfsfile.WriteItemTimeStepNext(time, val)

        dfsfile.Close()

#endregion Extractor classes


def Main(arguments):
    parser = CommandLineParser(arguments)

    # Print usage of the tool if there are no arguments
    if parser.printUsage:
        PrintUsage()
        return

    # Setup result finder
    resultFinder = ResultFinder(parser.resFileName, useFilter=True)

    # Print all quantities if there are no quantities specified for output
    if parser.printAllQuantities:
        print("All quantities in file:")
        resultFinder.PrintAllQuantities()
        return

    if parser.cannotHandleArgument:
        return

    # Create a location filter, which is used to load a result file
    for p in parser.parsedArguments:
        locationType, locationId, chainage  = p.locationType, p.locationId, p.chainage

        if p.printAllLocations:
            print("Available %s names in file:" % locationType)
            resultFinder.PrintAllLocations(locationType)
            return

        if p.printQuantities:
            print("Available %s quantities in %s:" % (locationType, locationId))
            resultFinder.PrintQuantities(locationType, locationId, chainage)
            return

        resultFinder.AddLocation(locationType,  locationId)

    # Load the actual data into memory
    resultFinder.Load()

    # Create a list of Mike1D data items based on parsed arguments
    outputData = []
    for p in parser.parsedArguments:
        locationType, quantityId, locationId, chainage  = p.locationType, p.quantityId, p.locationId, p.chainage

        dataEntries = resultFinder.FindQuantityInLocation(locationType, quantityId, locationId, chainage)
        for dataEntry in dataEntries:
            outputData.append(dataEntry)

    # Export the data in a wanted format
    exporter = Extractor.Create(parser.outFileType, parser.outFileName, outputData, resultFinder.resultData)
    exporter.Export()


if __name__ == "__main__":
    Main(sys.argv)
