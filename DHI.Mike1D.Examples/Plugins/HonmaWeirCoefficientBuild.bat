set csc32=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set csc64=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set csc=%csc64%
set sdkBin=..\..\bin
set m1dRef=/r:"%sdkBin%\DHI.Mike1D.Engine.dll" /r:"%sdkBin%\DHI.Mike1D.Generic.dll" /r:"%sdkBin%\DHI.Mike1D.Mike1DDataAccess.dll" /r:"%sdkBin%\DHI.Mike1D.NetworkDataAccess.dll" /r:"%sdkBin%\DHI.Mike1D.StructureModule.dll"
%csc% /target:library /platform:x64 %m1dRef% HonmaWeirCoefficient.cs
pause