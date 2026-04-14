FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

COPY src/FutureViewer.Domain/FutureViewer.Domain.csproj src/FutureViewer.Domain/
COPY src/FutureViewer.DomainServices/FutureViewer.DomainServices.csproj src/FutureViewer.DomainServices/
COPY src/FutureViewer.Infrastructure/FutureViewer.Infrastructure.csproj src/FutureViewer.Infrastructure/
COPY src/FutureViewer.Host/FutureViewer.Host.csproj src/FutureViewer.Host/
RUN dotnet restore src/FutureViewer.Host/FutureViewer.Host.csproj

COPY src/ src/
RUN dotnet publish src/FutureViewer.Host/FutureViewer.Host.csproj \
    -c Release \
    -o /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish ./
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "FutureViewer.Host.dll"]
