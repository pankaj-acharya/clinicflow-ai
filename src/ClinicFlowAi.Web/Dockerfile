FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./ClinicFlowAi.sln ./
COPY ./src/ClinicFlowAi.Web/ClinicFlowAi.Web.csproj ./src/ClinicFlowAi.Web/

RUN dotnet restore ./src/ClinicFlowAi.Web/ClinicFlowAi.Web.csproj

COPY . .
WORKDIR /src/src/ClinicFlowAi.Web
RUN dotnet publish ClinicFlowAi.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClinicFlowAi.Web.dll"]
