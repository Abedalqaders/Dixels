# Local development image for Dixels.Web.
# Runs from the source tree (not a publish folder) because in Development the web module
# swaps embedded files for the physical ..\Dixels.Domain etc. folders next to the project.
FROM mcr.microsoft.com/dotnet/sdk:10.0
WORKDIR /src

# Restore first, in its own layer, so code changes don't re-download every package.
COPY common.props NuGet.Config ./
COPY src/Dixels.Domain.Shared/*.csproj src/Dixels.Domain.Shared/
COPY src/Dixels.Domain/*.csproj src/Dixels.Domain/
COPY src/Dixels.Application.Contracts/*.csproj src/Dixels.Application.Contracts/
COPY src/Dixels.Application/*.csproj src/Dixels.Application/
COPY src/Dixels.EntityFrameworkCore/*.csproj src/Dixels.EntityFrameworkCore/
COPY src/Dixels.HttpApi/*.csproj src/Dixels.HttpApi/
COPY src/Dixels.Web/*.csproj src/Dixels.Web/
RUN dotnet restore src/Dixels.Web/Dixels.Web.csproj

COPY src/ src/
RUN dotnet build src/Dixels.Web/Dixels.Web.csproj --no-restore

WORKDIR /src/src/Dixels.Web
EXPOSE 44334
ENTRYPOINT ["dotnet", "run", "--no-build", "--no-launch-profile"]
