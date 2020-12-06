FROM mcr.microsoft.com/dotnet/sdk:5.0
WORKDIR /app

COPY . .
RUN dotnet restore build/build.proj
