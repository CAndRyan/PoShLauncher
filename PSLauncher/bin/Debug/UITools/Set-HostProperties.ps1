# Set-HostProperties.ps1
#
# This script will set the properties of a powershell console
#
# @Author:	CandRyan
# @Date:	September 20th, 2015
#
# @PSVers:	2.0+
# @Syntax:	.\Set-HostProperties.ps1

# Read in parameters passed to script
param([String]$scriptToRun, [String]$winTitle, [Switch]$short)

$console = $host.UI.RawUI
$console.ForegroundColor = "white"
$console.BackgroundColor = "black"

$size = $console.WindowSize
$size.Width = 100
if ($short) {
	$size.Height = 30
}
else {
	$size.Height = 56
}

$buffer = $console.BufferSize
$buffer.Width = 100
$buffer.Height = 400

if ($console.BufferSize.Width -gt $size.Width) {
	$console.WindowSize = $size
	$console.BufferSize = $buffer
}
else {
	$console.BufferSize = $buffer
	$console.WindowSize = $size
}

if ($winTitle) {
	$console.WindowTitle = $winTitle
}

Clear-Host
if ($scriptToRun) {
	& $scriptToRun
}