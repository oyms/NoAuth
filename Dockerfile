FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
COPY src/Skaar.NoAuth/ ./Skaar.NoAuth/
RUN dotnet publish ./Skaar.NoAuth -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0
LABEL org.opencontainers.image.description="A fake OAuth2/OpenID Connect server for testing purposes"
WORKDIR /app
COPY --from=build /app ./

# Listen on all interfaces inside the container. BaseUrl is the url clients use to reach the server.
ENV ASPNETCORE_URLS=http://+:8080 \
    NoAuth__BaseUrl=http://localhost:8080
EXPOSE 8080
USER $APP_UID

ENTRYPOINT ["dotnet", "Skaar.NoAuth.dll"]
