FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY Cadmus.Graph ./Cadmus.Graph
COPY CadmusGraphDemo ./CadmusGraphDemo
RUN dotnet restore Cadmus.Graph/Cadmus.Graph.csproj
RUN dotnet restore CadmusGraphDemo/CadmusGraphDemo.csproj
RUN dotnet build CadmusGraphDemo/CadmusGraphDemo.csproj -c Release -o /app/build

FROM build AS publish
RUN dotnet publish CadmusGraphDemo/CadmusGraphDemo.csproj -c Release -o /app/publish

FROM nginx:alpine AS final
WORKDIR /usr/share/nginx/html
COPY --from=publish /app/publish/wwwroot .
COPY nginx.conf /etc/nginx/nginx.conf
RUN rm /etc/nginx/conf.d/default.conf
EXPOSE 80
