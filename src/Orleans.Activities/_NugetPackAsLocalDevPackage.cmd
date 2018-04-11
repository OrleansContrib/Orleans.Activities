@echo off
for /f %%x in ('wmic path win32_localtime get /format:list ^| findstr "="') do set %%x
set Month=0%Month%
set Day=0%Day%
set Hour=0%Hour%
set Minute=0%Minute%
set suffix=dev%Year%%Month:~-2%%Day:~-2%%Hour:~-2%%Minute:~-2%
echo on
nuget pack Orleans.Activities.csproj -outputdirectory bin\Debug -suffix %suffix%
