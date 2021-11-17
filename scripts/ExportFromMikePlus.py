"""
This is an example script showing how to export
from Mike+ setup file (mupp/slite) to Mike1D m1dx file.
"""

import clr
import sys

clr.AddReference("DHI.Mike.Install, Version=1.0.0.0, Culture=neutral, PublicKeyToken=c513450b5d0bf0bf")
from DHI.Mike.Install import MikeImport, MikeProducts


def SetupMikePlusInstallation():
    products = list(MikeImport.InstalledProducts())
    for product in products:
        if product.Product == MikeProducts.MikePlus:
            break

    if product.Product != MikeProducts.MikePlus:
        print("Could not find Mike+ installation")
        sys.exit()

    print('Using product: ' + product.Product)
    MikeImport.Setup(product)
    print('Found MIKE in: ' + MikeImport.ActiveProduct().InstallRoot)


SetupMikePlusInstallation()

clr.AddReference("DHI.Mike1D.Generic")
from DHI.Mike1D.Generic import Connection, FilePath, Diagnostics

clr.AddReference("DHI.Mike1D.Mike1DDataAccess")
from DHI.Mike1D.Mike1DDataAccess import Mike1DBridge


def CreateConnection(inputFileName, simulationId=None):
    connection = Connection.Create(inputFileName)
    # Choose a particular simulation ID from Mike+ setup
    # If this is not set then the active simulation is exported
    if simulationId is not None:
        connection.Options.Add("simulationId", simulationId);

    return connection


def CreateMike1DData(inputFileName, simulationId=None):
    diagnostics = Diagnostics("Export diagnostics")
    connection = CreateConnection(inputFileName, simulationId)

    mike1DBridge = Mike1DBridge()
    mike1DData = mike1DBridge.Open(connection, diagnostics)

    return mike1DData


def ExportMike1DData(mike1DData, m1dxFileName):
    m1dxFilePath= FilePath(m1dxFileName)
    mike1DData.Connection.FilePath = m1dxFilePath
    mike1DData.Connection.BridgeName = "m1dx"
    Mike1DBridge.Save(mike1DData)


# Create Mike1DData object
inputFileName = "MySetup.sqlite"
simulationId = "MySimulation"
mike1DData = CreateMike1DData(inputFileName, simulationId)

# Export Mike1DData object to m1dx file
m1dxFileName = "MySetup.m1dx"
ExportMike1DData(mike1DData, m1dxFileName)
