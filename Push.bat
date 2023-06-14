@echo off
echo PUSH PACKAGES TO NUGET
prompt
set nu=C:\Exe\nuget.exe
set src=-Source https://api.nuget.org/v3/index.json

%nu% push .\Cadmus.Graph\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.Ef\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.Ef.MySql\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.Ef.PgSql\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.MySql\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.Sql\bin\Debug\*.nupkg %src%
%nu% push .\Cadmus.Graph.Extras\bin\Debug\*.nupkg %src%
echo COMPLETED
echo on
