@echo off
echo PRESS ANY KEY TO INSTALL TO LOCAL NUGET FEED
echo Remember to generate the up-to-date package.
c:\exe\nuget add .\Cadmus.Graph\bin\Debug\Cadmus.Graph.2.0.5.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.Sql\bin\Debug\Cadmus.Graph.Sql.2.0.5.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.MySql\bin\Debug\Cadmus.Graph.MySql.2.0.5.nupkg -source C:\Projects\_NuGet
pause
