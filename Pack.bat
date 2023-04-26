@echo off
echo BUILD Cadmus packages
del .\Cadmus.Graph\bin\Debug\*.*nupkg
del .\Cadmus.Graph.MySql\bin\Debug\*.*nupkg
del .\Cadmus.Graph.Sql\bin\Debug\*.*nupkg

cd .\Cadmus.Graph
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.MySql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Sql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Extras
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
pause
