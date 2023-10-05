@echo off
echo BUILD Cadmus packages
del .\Cadmus.Graph\bin\Debug\*.*nupkg
del .\Cadmus.Graph.Ef\bin\Debug\*.*nupkg
del .\Cadmus.Graph.Ef.MySql\bin\Debug\*.*nupkg
del .\Cadmus.Graph.Ef.PgSql\bin\Debug\*.*nupkg
del .\Cadmus.Graph.Extras\bin\Debug\*.*nupkg

cd .\Cadmus.Graph
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Ef
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Ef.MySql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Ef.PgSql
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
cd .\Cadmus.Graph.Extras
dotnet pack -c Debug -p:IncludeSymbols=true -p:SymbolPackageFormat=snupkg
cd..
pause
