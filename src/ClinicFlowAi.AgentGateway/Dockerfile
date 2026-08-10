FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./ClinicFlowAi.sln ./
COPY ./src/ClinicFlowAi.AgentGateway/ClinicFlowAi.AgentGateway.csproj ./src/ClinicFlowAi.AgentGateway/

RUN dotnet restore ./src/ClinicFlowAi.AgentGateway/ClinicFlowAi.AgentGateway.csproj

COPY . .
WORKDIR /src/src/ClinicFlowAi.AgentGateway
RUN dotnet publish ClinicFlowAi.AgentGateway.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClinicFlowAi.AgentGateway.dll"]