using System;
using System.IO;
using System.Runtime.Serialization;
using System.Xml;
using DHI.Mike1D.Generic;
using DHI.Mike1D.Mike1DDataAccess;

namespace DHI.Mike1D.Examples
{

  /// <summary>
  /// User defined type to store together with the MIKE 1D setup
  /// </summary>
  public class MyData
  {
    public string SValue { get; set; }
    public double DValue { get; set; }
  }
  
  /// <summary>
  /// Example on how to store user data on various location in a network, save it to disc
  /// and read it from disc again.
  /// </summary>
  public class UserDataExamples
  {

    /// <summary>
    /// Example of how to define data in a network, save it to disc, and read it again.
    /// <para>
    /// This example stores values of the complex type <see cref="MyData"/>, though simple
    /// types as string, int double are also supported.
    /// </para>
    /// </summary>
    public static void SaveNetworkData()
    {
      string filename = Path.Combine(ExampleBase.ExampleRoot, @"vidaUserData.myxml");

      // This block will store some data in a MIKE 1D data object, and save it to an xml file
      {
        // Create a NetworkData that can store a user defined type, and add some data at some locations
        NetworkData<MyData> myComplexData = new NetworkData<MyData>() {CanInterpolate = false};
        myComplexData.AddValue(new Location("SonderAa", 1034), new MyData() {SValue = "SA", DValue = 1.034});
        myComplexData.AddValue(new Location("LindSkov", 2860), new MyData() { SValue = "LS", DValue = 2.86 });
        myComplexData.AddValue("MyNodeId", new MyData() { SValue = "Node", DValue = -42 });

        // Xml writer settings for getting "pretty"/human readable xml text file
        XmlWriterSettings xmlWriterSettings = new XmlWriterSettings()
                                                {
                                                  Indent = true,
                                                  CheckCharacters = false,
                                                  CloseOutput = true,
                                                  NewLineHandling = NewLineHandling.Entitize
                                                };
        // Open the file for writing
        XmlDictionaryWriter writer = XmlDictionaryWriter.CreateDictionaryWriter(XmlWriter.Create(filename, xmlWriterSettings));

        // Create a DataContractSerializer. It must in this case "preserveObjectReferences". In many cases that is not
        // required, for more simple types than NetworkData<MyData>, it can be set to false, which gives much nicer xml. 
        Type[] knownTypes = new Type[] { typeof(NetworkData<double>) };
        var dcs = new DataContractSerializer(typeof(NetworkData<MyData>), knownTypes, int.MaxValue,  
                                             false /*ignoreExtensionDataObject*/, true /* preserveObjectReferences */, 
                                             null /*Surrogate*/);

        // Write data to file
        dcs.WriteObject(writer, myComplexData);
        writer.Close();
      }

      // This block will read the data agian
      {
        // Open the file for reading
        var fs = new FileStream(filename, FileMode.Open, FileAccess.Read, FileShare.Read);
        XmlDictionaryReader reader = XmlDictionaryReader.CreateTextReader(fs, new XmlDictionaryReaderQuotas());
        
        // Create a DataContractSerializer, matching the one used when writing the file
        Type[] knownTypes = new Type[] { typeof(NetworkData<double>) };
        var dcs = new DataContractSerializer(typeof(NetworkData<MyData>), knownTypes, int.MaxValue,
                                                     false /*ignoreExtensionDataObject*/, true /* preserveObjectReferences */,
                                                     null /*Surrogate*/);
        
        // Read file again
        NetworkData<MyData> myComplexData = (NetworkData<MyData>)dcs.ReadObject(reader, true); 
        reader.Close();

        // Now we can extract our data again
        MyData v1034;
        bool ok1034 = myComplexData.GetValue(new Location("SonderAa", 1034), out v1034);
        MyData v2860;
        bool ok2860 = myComplexData.GetValue(new Location("LindSkov", 2860), out v2860);
        MyData vNode;
        bool okNode = myComplexData.GetValue("MyNodeId", out vNode);
      }
    }
    
    /// <summary>
    /// This is basically the same example as the SaveNetworkData above, though
    /// this time the data is stored as a part of the MIKE 1D setup in the
    /// standard .m1dx file.
    /// </summary>
    /// <param name="vidaaFilepath">Path and name of the Vidaa example</param>
    public static void SaveDoubleNetworkDataWithSetup(string vidaaFilepath)
    {
      {
        // Creates a new Mike 1D controller and load the setup
        Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");
        IMike1DController controller = controllerFactory.OpenAndCreate(Connection.Create(vidaaFilepath), diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Loading errors, aborting");

        // Create a NetworkData that can store a user defined type, and add some data at some locations
        NetworkDataDouble myDoubleData = new NetworkDataDouble() { CanInterpolate = false };
        myDoubleData.AddValue(new Location("SonderAa", 1034), 1.034);
        myDoubleData.AddValue(new Location("LindSkov", 2860), 2.86);
        myDoubleData.AddValue("MyNodeId", -42);

        // Set the NetworkData as additional data, using the string "UserData" as key
        controller.Mike1DData.SetAdditionalData("UserData", myDoubleData);

        // Saving the file again (same filename, new extension)
        controller.Mike1DData.Connection.BridgeName = "m1dx";
        controller.Mike1DData.Connection.FilePath.Extension = ".m1dx";
        Mike1DBridge.Save(controller.Mike1DData);
        controller.Finish();
      }

      {
        // Load the setup again, changing the extension to .m1dx
        FilePath vidaaFile = new FilePath(vidaaFilepath);
        vidaaFile.Extension = ".m1dx";

        // Load the setup
        Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");
        IMike1DController controller = controllerFactory.OpenAndCreate(Connection.Create(vidaaFile.Path), diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Loading errors, aborting");

        // Get the additional data using the string "UserData" as key, which we know is a NetworkData with MyData
        NetworkDataDouble myComplexData = (NetworkDataDouble)controller.Mike1DData.GetAdditionalData("UserData");

        // Now we can extract our data again
        double v1034;
        bool ok1034 = myComplexData.GetValue(new Location("SonderAa", 1034), out v1034);
        double v2860;
        bool ok2860 = myComplexData.GetValue(new Location("LindSkov", 2860), out v2860);
        double vNode;
        bool okNode = myComplexData.GetValue("MyNodeId", out vNode);

        controller.Finish();
      }
    }

    /// <summary>
    /// This is basically the same example as the SaveNetworkData above, though
    /// this time the data is stored as a part of the MIKE 1D setup in the
    /// standard .m1dx file.
    /// <para>
    /// This is ONLY recommended for advanced users and use: 
    /// The disadvantage of this approach is that the resulting .m1dx file can only be
    /// read again by a similar approach, i.e. it WILL ONLY run using the default engine
    /// when the custom assembly is available, hence it CAN NOT be read by others unless 
    /// they are also provided with the custom assembly.
    /// </para>
    /// </summary>
    /// <param name="vidaaFilepath">Path and name of the Vidaa example</param>
    /// <param name="customAssemblyFile"></param>
    public static void SaveComplexNetworkDataWithSetup(string vidaaFilepath, string customAssemblyFile)
    {
      {
        // Creates a new Mike 1D controller and load the setup
        Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");
        IMike1DController controller = controllerFactory.OpenAndCreate(Connection.Create(vidaaFilepath), diagnostics);
        if (diagnostics.ErrorCountRecursive > 0)
          throw new Exception("Loading errors, aborting");

        // Create a NetworkData that can store a user defined type, and add some data at some locations
        NetworkData<MyData> myComplexData = new NetworkData<MyData>() { CanInterpolate = false };
        myComplexData.AddValue(new Location("SonderAa", 1034), new MyData() { SValue = "SA", DValue = 1.034 });
        myComplexData.AddValue(new Location("LindSkov", 2860), new MyData() { SValue = "LS", DValue = 2.86 });
        myComplexData.AddValue("MyNodeId", new MyData() { SValue = "Node", DValue = -42 });

        // Set the NetworkData as additional data, using the string "UserData" as key
        controller.Mike1DData.SetAdditionalData("UserData", myComplexData);
        controller.Mike1DData.SetAdditionalData("UserData2", new MyData() { SValue = "Upstream", DValue = 15.03 });

        // Saving the file again (same filename, new extension)
        controller.Mike1DData.Connection.BridgeName = "m1dx";
        controller.Mike1DData.Connection.FilePath.Extension = ".m1dx";

        // We need to add two custom types, in order to store data to .m1dx
        // The types are found in the DHI.Mike1D.Examples.dll, which cannot be found by the MIKE 1D engine unless an explicit path is provided
        // Make the path relative to the setup.
        string relativeAssemblyFile = FilePath.GetRelativeFilePath(customAssemblyFile, Path.GetDirectoryName(Path.GetFullPath(vidaaFilepath)));
        controller.Mike1DData.CustomTypes.AssemblyFiles.Add(relativeAssemblyFile);
        controller.Mike1DData.CustomTypes.Add(myComplexData.GetType());
        controller.Mike1DData.CustomTypes.Add(typeof(MyData));

        Mike1DBridge.Save(controller.Mike1DData);

        // Close any log files
        controller.Finish();
      }

      {

        // Load the setup again, changing the extension to .m1dx
        FilePath vidaaFile = new FilePath(vidaaFilepath);
        vidaaFile.Extension = ".m1dx";

        Mike1DControllerFactory controllerFactory = new Mike1DControllerFactory();
        Diagnostics diagnostics = new Diagnostics("My Diagnostics");
        IMike1DController controller = controllerFactory.OpenAndCreate(Connection.Create(vidaaFile), diagnostics);

        Mike1DData mike1DData = controller.Mike1DData;

        // Get the additional data using the string "UserData" as key, which we know is a NetworkData with MyData
        NetworkData<MyData> myComplexData = (NetworkData<MyData>)mike1DData.GetAdditionalData("UserData");
        MyData myComplexData2 = (MyData)mike1DData.GetAdditionalData("UserData2");

        // Now we can extract our data again
        MyData v1034;
        bool ok1034 = myComplexData.GetValue(new Location("SonderAa", 1034), out v1034);
        MyData v2860;
        bool ok2860 = myComplexData.GetValue(new Location("LindSkov", 2860), out v2860);
        MyData vNode;
        bool okNode = myComplexData.GetValue("MyNodeId", out vNode);

        controller.Finish();
      }
    }
  
  
  }
}
