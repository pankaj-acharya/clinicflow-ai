FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ./ClinicFlowAi.sln ./
COPY ./src/ClinicFlowAi.Domain/ClinicFlowAi.Domain.csproj ./src/ClinicFlowAi.Domain/
COPY ./src/ClinicFlowAi.Api/ClinicFlowAi.Api.csproj ./src/ClinicFlowAi.Api/

RUN dotnet restore ./src/ClinicFlowAi.Api/ClinicFlowAi.Api.csproj

COPY . .
WORKDIR /src/src/ClinicFlowAi.Api
RUN dotnet publish ClinicFlowAi.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ClinicFlowAi.Api.dll"]