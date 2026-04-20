FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for better layer caching
COPY SMEFLOWSystem.WebAPI/SMEFLOWSystem.WebAPI.csproj SMEFLOWSystem.WebAPI/
COPY SMEFLOWSystem.Application/SMEFLOWSystem.Application.csproj SMEFLOWSystem.Application/
COPY SMEFLOWSystem.Infrastructure/SMEFLOWSystem.Infrastructure.csproj SMEFLOWSystem.Infrastructure/
COPY SMEFLOWSystem.SharedKernel/SMEFLOWSystem.SharedKernel.csproj SMEFLOWSystem.SharedKernel/
COPY SMEFLOWSystem.Core/SMEFLOWSystem.Core.csproj SMEFLOWSystem.Core/
COPY VNPAY.NET/VNPAY.NET.csproj VNPAY.NET/

RUN dotnet restore SMEFLOWSystem.WebAPI/SMEFLOWSystem.WebAPI.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish SMEFLOWSystem.WebAPI/SMEFLOWSystem.WebAPI.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "SMEFLOWSystem.WebAPI.dll"]
