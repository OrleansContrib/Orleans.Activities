$productversion = [IO.File]::ReadAllText(".\productversion.txt")
Set-AppveyorBuildVariable -Name productversion -Value $productversion