#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

RUN addgroup --system nonroot \
    && adduser --system --ingroup nonroot --shell /bin/sh nonroot

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Do not restore / install Husky hooks.
ENV HUSKY=0

COPY ["src/Pragmatic.TemplateApi.Api/Pragmatic.TemplateApi.Api.csproj", "src/Pragmatic.TemplateApi.Api/"]
RUN dotnet restore "src/Pragmatic.TemplateApi.Api/Pragmatic.TemplateApi.Api.csproj"
COPY /src /src
WORKDIR "/src/Pragmatic.TemplateApi.Api"
RUN dotnet build "Pragmatic.TemplateApi.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pragmatic.TemplateApi.Api.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER nonroot
ENTRYPOINT ["dotnet", "Pragmatic.TemplateApi.Api.dll"]