@echo off
echo PRESS ANY KEY TO INSTALL TO LOCAL NUGET FEED
echo Remember to generate the up-to-date package.
c:\exe\nuget add .\Cadmus.Graph\bin\Debug\Cadmus.Graph.5.0.3.nupkg -source C:\Projects\_NuGet
echo You can now update Cadmus Core so that Cadmus.Index dependencies are updated
pause
c:\exe\nuget add .\Cadmus.Graph.Ef\bin\Debug\Cadmus.Graph.Ef.5.0.3.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.Ef.MySql\bin\Debug\Cadmus.Graph.Ef.MySql.5.0.3.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.Ef.PgSql\bin\Debug\Cadmus.Graph.Ef.PgSql.5.0.3.nupkg -source C:\Projects\_NuGet
c:\exe\nuget add .\Cadmus.Graph.Extras\bin\Debug\Cadmus.Graph.Extras.5.0.3.nupkg -source C:\Projects\_NuGet
pause
