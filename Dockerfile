FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["InkPulse.Worker.csproj", "./"]
RUN dotnet restore "InkPulse.Worker.csproj"
COPY . .
RUN dotnet publish "InkPulse.Worker.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InkPulse.Worker.dll"]
