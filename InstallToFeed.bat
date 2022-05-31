@echo off
echo PRESS ANY KEY TO INSTALL Cadmus Libraries TO LOCAL NUGET FEED
echo Remember to generate the up-to-date package.
pause
c:\exe\nuget add .\Cadmus.Graph\bin\Debug\Cadmus.Graph.0.0.3.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.Sql\bin\Debug\Cadmus.Graph.Sql.0.0.3.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.MySql\bin\Debug\Cadmus.Graph.MySql.0.0.3.nupkg -source C:\Projects\_NuGet
pause
