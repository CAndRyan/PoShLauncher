Param (
	# Server(s) to check
	[Parameter(Mandatory=$True)]
	[string[]]
	$server
)
Import-Module ActiveDirectory
$returnValue = New-Object System.Collections.Generic.List[PSCustomObject]

foreach ($serv in $server) {
	$serv = $serv.Trim()
	$object = New-Object -TypeName PSObject
	$object |Add-Member -MemberType NoteProperty -Name Server -Value $serv -PassThru |
		Add-Member -MemberType NoteProperty -Name Validity -Value "v"
	try {
		Get-ADComputer -Identity $serv |Out-Null
	}
	catch { 
		$object.Validity = "i"
	}
	
	if ($object.Validity -eq "v") {
		$returnValue.add($object)
	}
}

$returnString = ""
if ($returnValue.Count -gt 0) {		# Valid server was found
	$returnString += "v"
	foreach ($rv in $returnValue) {
		$returnString += "~$($rv.Server)"
	}
}
else {		# Only spit out invalid message if all input servers are invalid
	$returnString += "i"
	foreach ($rv in $server) {
		$returnString += "~$rv is an invalid server"
	}
}

return $returnString