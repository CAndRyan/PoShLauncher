Param (
	# Array of functions and/or scripts to retrieve help for
	[Parameter(mandatory="true")]
	[string[]]
	$commands
)
$helpObjects = New-Object System.Collections.Generic.List[PSCustomObject] 
ForEach ($c in $commands) {
	if ($c -like "*.ps1") {
		if (!(Test-Path -Path $c)) {
			Write-Error "Unable to locate script!"
			continue
		}
	
		$object = Get-Help -Name "$($c.Replace('~', ' '))" |
			Select-Object -Property Name, Synopsis, `
				@{ NAME='Detail'; EXPRESSION={$d = ''; $_.description.Text | ForEach {$d += ($_.Trim() + ' ')}; return $d} }, `
				@{ NAME='Param'; EXPRESSION={$d = ''; $_.parameters.parameter.Name | ForEach {$d += ($_.Trim() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				@{ NAME='ParamReq'; EXPRESSION={$d = ''; $_.parameters.parameter.Required | ForEach {$d += ($_.Trim() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				@{ NAME='Single'; EXPRESSION={$d = ''; $_.parameters.parameter.type.Name | ForEach {$d += ((!$_.Contains('[')).ToString().ToLower() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				Role, Category 
		if (($object.Role -notlike "*Private*")) {
			$helpObjects.add($object)
		}
	}
	elseif ($c -like "*.psm1") {
		if (!(Test-Path -Path $c)) {
			Write-Error "Unable to locate module!"
			continue
		}
	
		$regex = "\\([^\\]+)\.psm1$"
		Import-Module "$($c.Replace('~', ' '))"
		"$c" -Match $regex |Out-Null
		$moduleName = $Matches[1]
		$functions = Get-Command -Module $moduleName
		foreach ($f in $functions) {
			$object = Get-Help -Name "$f" |
				Select-Object -Property Name, Synopsis, `
				@{ NAME='Detail'; EXPRESSION={$d = ''; $_.description.Text | ForEach {$d += ($_.Trim() + ' ')}; return $d} }, `
				@{ NAME='Param'; EXPRESSION={$d = ''; $_.parameters.parameter.Name | ForEach {$d += ($_.Trim() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				@{ NAME='ParamReq'; EXPRESSION={$d = ''; $_.parameters.parameter.Required | ForEach {$d += ($_.Trim() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				@{ NAME='Single'; EXPRESSION={$d = ''; $_.parameters.parameter.type.Name | ForEach {$d += ((!$_.Contains('[')).ToString().ToLower() + '~')}; return $d.SubString(0, $d.Length - 1)} }, `
				Role, Category 
			if (($object.Role -notlike "*Private*")) {
				$helpObjects.add($object)
			}
		}
	}
} 
return $helpObjects