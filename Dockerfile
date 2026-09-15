# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /source

# Restore first, with only the project files copied, so the (slow) restore layer is
# cached until a dependency actually changes.
COPY src/PaymentGateway.Api/PaymentGateway.Api.csproj                 src/PaymentGateway.Api/
COPY src/PaymentGateway.Application/PaymentGateway.Application.csproj src/PaymentGateway.Application/
COPY src/PaymentGateway.Domain/PaymentGateway.Domain.csproj           src/PaymentGateway.Domain/
COPY src/PaymentGateway.Infrastructure/PaymentGateway.Infrastructure.csproj src/PaymentGateway.Infrastructure/
RUN dotnet restore src/PaymentGateway.Api/PaymentGateway.Api.csproj

COPY src/ src/
COPY .editorconfig .
RUN dotnet publish src/PaymentGateway.Api/PaymentGateway.Api.csproj \
    -c $BUILD_CONFIGURATION \
    -o /app/publish \
    --no-restore

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Kestrel listens on plain HTTP only; TLS is expected to be terminated upstream.
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Serilog writes here via a relative path; docker compose bind-mounts the repo's
# ./logs folder over it so the files land outside the container.
RUN mkdir -p /app/logs

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "PaymentGateway.Api.dll"]
