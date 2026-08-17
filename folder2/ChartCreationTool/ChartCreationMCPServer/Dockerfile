# ── Stage 1: build the .NET app ──
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY ["ChartCreationMCPServer.csproj", "./"]
RUN dotnet restore "ChartCreationMCPServer.csproj"

COPY . .
RUN dotnet publish "ChartCreationMCPServer.csproj" -c Release -o /app/publish

# ── Stage 2: runtime with .NET + Python ──
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Python + the chart packages, baked into the image
RUN apt-get update \
    && apt-get install -y --no-install-recommends python3 python3-pip \
    && pip3 install --no-cache-dir --break-system-packages matplotlib pandas numpy openpyxl \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# The server resolves Python via this env var
ENV PYTHON_PATH=python3

# Everything is in the image — disable startup auto-install
ENV Python__AutoInstallPython=false
ENV Python__AutoInstallPackages=false

COPY --from=build /app/publish .

# Container Apps routes traffic to this port
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ChartCreationMCPServer.dll"]