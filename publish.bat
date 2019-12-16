@echo off

:: Snagged from https://stackoverflow.com/a/24665214/2396111
:: Check privileges 
net file 1>NUL 2>NUL
if not '%errorlevel%' == '0' (
    powershell Start-Process -FilePath "%0" -ArgumentList "%cd%" -verb runas >NUL 2>&1
    exit /b
)

:: Change directory with passed argument. Processes started with
:: "runas" start with forced C:\Windows\System32 workdir
cd /d %1

:: Actual work

appcmd stop site /site.name:DogPark
dotnet publish --output C:\inetpub\wwwroot /p:PublishProfile=Properties\PublishProfiles\CustomProfile.pubxml
appcmd start site /site.name:DogPark