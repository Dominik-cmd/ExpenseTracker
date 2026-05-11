FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY ExpenseTracker.slnx .
COPY src/ExpenseTracker.Core/ExpenseTracker.Core.csproj src/ExpenseTracker.Core/
COPY src/ExpenseTracker.Application/ExpenseTracker.Application.csproj src/ExpenseTracker.Application/
COPY src/ExpenseTracker.Infrastructure/ExpenseTracker.Infrastructure.csproj src/ExpenseTracker.Infrastructure/
COPY src/ExpenseTracker.Api/ExpenseTracker.Api.csproj src/ExpenseTracker.Api/
RUN dotnet restore src/ExpenseTracker.Api/ExpenseTracker.Api.csproj
COPY src/ src/
RUN dotnet publish src/ExpenseTracker.Api/ExpenseTracker.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS runtime
WORKDIR /app
COPY --from=build /app/publish .
RUN mkdir -p /app/data-protection-keys /app/logs && chown -R app:app /app
USER $APP_UID
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ExpenseTracker.Api.dll"]
