using System.IO;
using DHI.Mike1D.CrossSectionModule;
using DHI.Mike1D.Examples.PluginStruc;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;
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
    public static string ExampleRoot;

    static ExampleBase()
    {

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

  }
}