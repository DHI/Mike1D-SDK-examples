using System;
using System.IO;
using DHI.Mike1D.CrossSectionModule;
using DHI.Mike1D.Examples.PluginStruc;
using DHI.Mike1D.Examples.Scripts;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
using DHI.Mike1D.ResultDataAccess;
using NUnit.Framework;

namespace DHI.Mike1D.Examples
{
  /// <summary>
  /// Helper class, providing the base path folder to the provided examples,
  /// and a method for running all the examples using NUnit
  /// </summary>
  public class ExampleBase
  {
    
    /// <summary>
    /// Path to the data folder.
    /// </summary>
    public static string ExampleRoot = @"C:\Work\GitHub\Mike1D-SDK-examples\data\";

    /// <summary>
    /// Static constructor, setting up search paths for MIKE assemblies
    /// </summary>
    static ExampleBase()
    {
      // The setup method will make your application find the MIKE assemblies at runtime.
      // The first call of the setup method takes precedense. Any subsequent calls will be ignored.
      // It must be called BEFORE any method using MIKE libraries is called, i.e. it is not sufficient
      // to call it as the first thing in that method using the MIKE libraries. Often this can be achieved
      // by having this code in the static constructor.
      // This is not required by plugins and scripts, only by standalone applications using MIKE 1D components
      if (!DHI.Mike.Install.MikeImport.Setup(19, DHI.Mike.Install.MikeProducts.Mike1D))
        throw new Exception("Could not find a MIKE installation");
    }

    [Test]
    public void RunExamplesControllerRunTest()
    {
      string sim11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"UnitFlow\UnitFlow.sim11");
      RunExamples.ControllerRun(sim11Filepath);
    }

    [Test]
    public void CrossSectionExamplesNavigation()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\Vida96-1.xns11");
      CrossSectionExamples.Navigation(xns11Filepath);
    }

    [Test]
    public void CrossSectionExamplesLoopAllCrossSections()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"CrossSection\CrossSections.xns11");

      // Load cross section data
      Diagnostics diagnostics = new Diagnostics("Errors");
      CrossSectionDataFactory csDataFactory = new CrossSectionDataFactory();
      CrossSectionData csData = csDataFactory.Open(Connection.Create(xns11Filepath), diagnostics);

      // Loop over all cross sections
      CrossSectionExamples.LoopAllCrossSections(csData);

      // Save the cross section as a new file name
      csData.Connection.FilePath.FileNameWithoutExtension += "-csLoop";
      CrossSectionDataFactory.Save(csData);
    }

    [Test]
    public void CrossSectionExamplesAddCrossSection()
    {
      string sim11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"CrossSection\Simulation.sim11");
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"CrossSection\CrossSections.xns11");

      CrossSectionExamples.AddCrossSectionAndSave(xns11Filepath);
      CrossSectionExamples.AddCrossSectionAndRun(sim11Filepath);
    }

    [Test]
    public void CrossSectionExamplesModifyCrossSectionRaw()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"CrossSection\CrossSections.xns11");

      CrossSectionExamples.ModifyCrossSectionRawAndSave(xns11Filepath);
    }

    [Test]
    public void CrossSectionExamplesModifyCrossSectionProcessed()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"CrossSection\CrossSections.xns11");

      CrossSectionExamples.ModifyCrossSectionProcessedAndSave(xns11Filepath);
    }

    [Test]
    public void CrossSectionImExportSimpleExport()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-1.xns11");
      string txtFilepath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-1.xns11.txt");

      CrossSectionImExportSimple.Export(xns11Filepath, txtFilepath);
    }

    [Test]
    public void CrossSectionImExportSimpleImport()
    {
      string xns11Filepath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-1.imported.xns11");
      string txtFilepath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-1.xns11.txt");
      if (File.Exists(xns11Filepath))
        File.Delete(xns11Filepath);

      CrossSectionImExportSimple.Import(txtFilepath, xns11Filepath);
    }

    [Test]
    public void ResultDataExamplesFirstExample()
    {
      string resultFilePath = Path.Combine(ExampleBase.ExampleRoot, @"Results\vida96-3.res1d");

      ResultDataExamples.FirstExample(resultFilePath);
    }

    [Test]
    public void UerDataExamplesSaveNetworkData()
    {
      UserDataExamples.SaveNetworkData();
    }

    [Test]
    public void UserDataExamplesSaveDoubleData()
    {
      string resultFilePath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-3.sim11");

      UserDataExamples.SaveDoubleNetworkDataWithSetup(resultFilePath);
    }

    [Test]
    public void UserDataExamplesSaveComplexData()
    {
      string resultFilePath = Path.Combine(ExampleBase.ExampleRoot, @"Vida\vida96-3.sim11");
      string assemblyFile = Path.GetFullPath(Path.Combine(ExampleBase.ExampleRoot, @"..\DHI.Mike1D.Examples\bin\Debug\DHI.Mike1D.Examples.dll")); 

      UserDataExamples.SaveComplexNetworkDataWithSetup(resultFilePath, assemblyFile);
    }


    /// <summary>
    /// Save setup containing user structure.
    /// <para>
    /// This will not enable the control of the weir coefficient, for that the 
    /// plugin must be loaded explicitly.
    /// </para>
    /// </summary>
    [Test]
    public void UserStructureSaveData()
    {
      string setupFilePath = Path.Combine(ExampleBase.ExampleRoot, @"UnitFlow\UnitFlowStruc.sim11");

      Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
      Diagnostics diagnostics = new Diagnostics("My Diagnostics");
      IMike1DController controller = controllerFactory.OpenAndCreate(Connection.Create(setupFilePath), diagnostics);

      // Add custom assembly and type - required for the standalong engine to load it again.
      controller.Mike1DData.CustomTypes.AssemblyFiles.Add(@"..\..\DHI.Mike1D.Examples\bin\Debug\DHI.Mike1D.Examples.dll");
      controller.Mike1DData.CustomTypes.Add(typeof(MyStructure));

      // Save to .m1dx file
      controller.Mike1DData.Connection.FilePath.Extension = ".m1dx";
      controller.Mike1DData.Connection.FilePath.FileNameWithoutExtension = "UnitFlowStruc-2";
      controller.Mike1DData.Connection.BridgeName = "m1dx";
      controller.Mike1DData.ResultSpecifications.ForEach(rs => rs.Connection.FilePath.FileNameWithoutExtension += "-2");
      Mike1DBridge.Save(controller.Mike1DData);
    }

    [Test]
    [Explicit("Data is not provided")]
    public void ResultDataConvertSWMM5InfToRes1D()
    {
      string filename = @"C:\Work\Support\SS_RDII_2YR.txt";

      // Register bridge to MIKE 1D ResultData
      ResultBridgeFactories.AddFactory("swmmInterfaceFile", new SWMM5InterfaceFileBridgeFactory(true));

      // load result file
      IResultData resultData = new ResultData();
      resultData.Connection = Connection.Create(filename);
      resultData.Connection.BridgeName = "swmmInterfaceFile";
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.Load(resultDiagnostics);

      // Save to res1d
      resultData.Connection.BridgeName = "res1d";
      resultData.Connection.FilePath.Extension = "res1d";
      resultData.Save();


    }

    [Test]
    [Explicit("Data is not provided")]
    public void ResultDataConvertCRFToRes1D()
    {
      string filename = @"C:\Work\Support\SS_RDII_2YR.CRF";

      // load result file
      IResultData resultData = new ResultData();
      resultData.Connection            = Connection.Create(filename);
      Diagnostics resultDiagnostics = new Diagnostics("Example");
      resultData.Load(resultDiagnostics);

      // Save to res1d
      resultData.Connection.BridgeName         = "res1d";
      resultData.Connection.FilePath.Extension = "res1d";
      resultData.Save();
    }
  }
}