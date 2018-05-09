@echo off
set csc32=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
set csc64=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
set csc=%csc64%
set sdkBin=C:\Program Files (x86)\DHI\2016\MIKE SDK\bin
set sdkBin=..\..\..\..\..\bin
set m1dRef=/r:"%sdkBin%\DHI.Mike1D.Generic.dll" /r:"%sdkBin%\DHI.Mike1D.ResultDataAccess.dll"  /r:"%sdkBin%\DHI.Mike1D.Mike1DDataAccess.dll"
%csc% /target:library /platform:x64 %m1dRef% DisableOutput.cs
pause
