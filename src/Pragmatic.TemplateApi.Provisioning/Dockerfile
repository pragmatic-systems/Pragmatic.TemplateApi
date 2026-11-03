#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

RUN addgroup --system nonroot \
    && adduser --system --ingroup nonroot --shell /bin/sh nonroot

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY ["src/Pragmatic.TemplateApi.Provisioning/Pragmatic.TemplateApi.Provisioning.csproj", "src/Pragmatic.TemplateApi.Provisioning/"]
RUN dotnet restore "src/Pragmatic.TemplateApi.Provisioning/Pragmatic.TemplateApi.Provisioning.csproj"
COPY /src /src
WORKDIR "/src/Pragmatic.TemplateApi.Provisioning"
RUN dotnet build "Pragmatic.TemplateApi.Provisioning.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pragmatic.TemplateApi.Provisioning.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER nonroot
ENTRYPOINT ["dotnet", "Pragmatic.TemplateApi.Provisioning.dll"]