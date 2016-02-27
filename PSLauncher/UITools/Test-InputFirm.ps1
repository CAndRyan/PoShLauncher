Param (
	# Shortcode to check
	[Parameter(Mandatory=$True)]
	[string]
	$shortcode
)
Import-Module ActiveDirectory
$returnValue = New-Object System.Collections.Generic.List[PSCustomObject]

# First try is to test the shortcode exactly as entered
$partialName = $false
$foundValue = @()
$testServer = "$shortcode-XA65001"
try {
	$foundValue = Get-ADComputer -Identity $testServer
}
catch { 
	$partialName = $true
}

# Second try is to search server names like the one entered (wildcard at end - first 3 digits)
$notSimilar = $false
if ($partialName) {
	if ($shortcode.Length -gt 2) {
		$shortcode = $shortcode.Substring(0, 3)
	}

	$foundValue = Get-ADComputer -Filter "(Name -like '$shortcode*-XA*') -and (Name -notlike '*sql*')"
	if (!($foundValue)) {
	}
}

if ($foundValue) {
	foreach ($find in $foundValue) {
		$firmObject = New-Object -TypeName PSObject
		
		$ou = @()
		$splitArray = $find.DistinguishedName.Split(",")
		$firm = ""
		foreach ($v in $splitArray) {
			if ($v -like "OU=*") {
				$ou += $v.subString(3)
			}
		}
		$firm = $ou[0]
		
		$firmObject | Add-Member -MemberType NoteProperty -Name Username -Value $find.Name -PassThru |
			Add-Member -MemberType NoteProperty -Name Firm -Value $firm
		
		$returnValue.add($firmObject)
	}
}

# Clean up the list by removing extra servers
$restart = $false
do {
	$restart = $false
	for ($j = 1; $j -lt $returnValue.Count; $j++) {
		if ($returnValue[$j].Firm -eq $returnValue[$j - 1].Firm) {
			$returnValue.RemoveAt($j)
			$restart = $true
			break
		}
	}
} while ($restart)

$returnString = ""
if ($returnValue.Count -eq 1) {		# Valid firm was found
	$returnString = "v~$($returnValue[0].Firm)"
}
elseif ($returnValue.Count -eq 0) {
	$returnString = "i~No firm(s) returned from search"
}
else {
	$returnString = "i~Similar firms found:"
	foreach ($rv in $returnValue) {
		$returnString += "~$($rv.Firm)"
	}
}

Return $returnString