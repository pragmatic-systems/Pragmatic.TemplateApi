#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8082
EXPOSE 8082

RUN addgroup --system nonroot \
    && adduser --system --ingroup nonroot --shell /bin/sh nonroot

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Do not restore / install Husky hooks.
ENV HUSKY=0

COPY ["src/Pragmatic.TemplateApi.Consumer/Pragmatic.TemplateApi.Consumer.csproj", "src/Pragmatic.TemplateApi.Consumer/"]
RUN dotnet restore "src/Pragmatic.TemplateApi.Consumer/Pragmatic.TemplateApi.Consumer.csproj"
COPY /src /src
WORKDIR "/src/Pragmatic.TemplateApi.Consumer"
RUN dotnet build "Pragmatic.TemplateApi.Consumer.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Pragmatic.TemplateApi.Consumer.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
USER nonroot
ENTRYPOINT ["dotnet", "Pragmatic.TemplateApi.Consumer.dll"]
