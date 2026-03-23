$isPublish = ($args[0] -eq "publish") 
$isBuild = ($args[0] -eq "build") 
$buildTarget = $args[1]

if ($buildTarget -and ![System.IO.Path]::IsPathRooted($buildTarget)) {
    $buildTarget = (Join-Path -Path "ui" -ChildPath $buildTarget)
}

$kvPairs = @{}

Get-Content "properties.txt" | ForEach-Object {
    $line = $_.Trim() 
    if ($line -match "^([^=]+)=(.*)$") {
        $key = $matches[1].Trim()
        $value = $matches[2].Trim()
        $kvPairs[$key] = $value
    }
}

$name = $kvPairs["name"]
$instdir = $kvPairs["installdir"]
$exename = $kvPairs["exename"]
$version = $kvPairs["version"]
$buildpath = $buildTarget


if ($buildTarget) {
    Copy-Item "icons\icon.ico" -Destination "$buildTarget"
    Copy-Item "bundled\*" -Destination "$buildTarget"
}

if ($isPublish) {

    # delete unneccessary files
    $pdbFile = (Join-Path -Path ${buildpath} -ChildPath "${exename}.pdb")
    if (Test-Path $pdbFile) { Remove-Item $pdbFile -Force }

    # make portable
    Compress-Archive -Path "$buildpath\*" -DestinationPath "$name Portable.zip" -CompressionLevel Optimal -Force

    # make installer
    Copy-Item -Path "make_installer.nsi" -Destination "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-name", "$name" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-exename", "$exename" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-version", "$version" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-buildpath", "$buildpath" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-outfile", "$name Installer.exe" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "POWERSHELL_INSERTS_THIS-instdir", "$instdir" | Set-Content "make_installer_temp.nsi"
    (Get-Content "make_installer_temp.nsi") -replace "Quit ;REMOVED_BY_POWERSHELL", "" | Set-Content "make_installer_temp.nsi"
    Invoke-Expression "makensis make_installer_temp.nsi"
    Remove-Item "make_installer_temp.nsi"

    if (!(Test-Path "build")) { New-Item -Name "build" -ItemType "Directory" }
     
    Move-Item -Path "$name Installer.exe" -Destination "build\$name $version Installer.exe" -Force
    Move-Item -Path "$name Portable.zip" -Destination "build\$name $version Portable.zip" -Force

}