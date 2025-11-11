FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 8080
COPY . .
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "EspApi.dll"]