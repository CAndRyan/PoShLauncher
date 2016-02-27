Param (
	# Username to check
	[Parameter(Mandatory=$True)]
	[string]
	$username
)
Import-Module ActiveDirectory
$returnValue = New-Object System.Collections.Generic.List[PSCustomObject]

# First try is to find the user exactly as entered
$partialName = $false
$tryEmail = $false
$foundValue = @()
if ($username -notlike "*@*") {
	try {
		$foundValue = Get-ADUser -Identity $username
	}
	catch {
		$partialName = $true
	}
}
else {		# Check the email
	$tryEmail = $true
	$foundValue = Get-ADUser -Filter "UserPrincipalName -eq '$username'"
	if (!($foundValue)) {
		$partialName = $true
	}
}

# Second try is to search for all SamAccountName's or principal name's like the one entered (wildcards at beginning and end)
$notSimilar = $false
if ($partialName) {
	$notSimilar1 = $false
	if (!($tryEmail)) {
		$foundValue = Get-ADUser -Filter "SamAccountName -like '*$username*'"
		if (!($foundValue)) {
			$notSimilar1 = $true
		}
	}
	else {
		$notSimilar1 = $true
	}
	
	# If either a partial email was detected or a partial SAM name was not found...
	if ($notSimilar1) {
		$foundValue = Get-ADUser -Filter "UserPrincipalName -like '*$username*'"
		if (!($foundValue)) {
			$notSimilar = $true
		}
	}
}

if ($foundValue) {
	foreach ($find in $foundValue) {
		$userObject = New-Object -TypeName PSObject
		
		$ou = @()
		$splitArray = $find.DistinguishedName.Split(",")
		$firm = ""
		foreach ($v in $splitArray) {
			if ($v -like "OU=*") {
				$ou += $v.subString(3)
			}
		}
		$firm = $ou[0]
		for ($i = 1; $i -lt ($ou.Count - 1); $i++) {
			$firm += " < $($ou[$i])"
		}
		
		$userObject | Add-Member -MemberType NoteProperty -Name Username -Value $find.SamAccountName -PassThru |
			Add-Member -MemberType NoteProperty -Name Firm -Value $firm
		
		$returnValue.add($userObject)
	}
}

$returnString = ""
if ($returnValue.Count -eq 1) {		# Valid user was found
	$returnString = "v~$($returnValue[0].Username)"
}
elseif ($returnValue.Count -eq 0) {
	$returnString = "i~No username(s) returned from search"
}
else {
	$returnString = "i~Similar users found:"
	foreach ($rv in $returnValue) {
		$returnString += "~$($rv.Username) from $($rv.Firm)"
	}
}

Return $returnString