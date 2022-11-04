FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["Beis.Ebss.Document.Api/Beis.Ebss.Document.Api.csproj", "Beis.Ebss.Document.Api/"]
RUN dotnet restore "Beis.Ebss.Document.Api/Beis.Ebss.Document.Api.csproj"
COPY . .
WORKDIR "/src/Beis.Ebss.Document.Api"
RUN dotnet build "Beis.Ebss.Document.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Beis.Ebss.Document.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Beis.Ebss.Document.Api.dll"]


#FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS base
#WORKDIR /app
#EXPOSE 80
#EXPOSE 443
#
#FROM base AS final
#WORKDIR /app
#COPY ./Beis.Ebss.Document.Api .
#ENTRYPOINT ["dotnet", "Beis.Ebss.Document.Api.dll"]


#HEALTHCHECK CMD curl --fail http://localhost:8008/healthz || exit