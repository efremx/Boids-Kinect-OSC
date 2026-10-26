$ErrorActionPreference = 'Stop'
$boidsUnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.2.13f1\Editor\Unity.exe'
$boidsRuleName = 'GesturalBoids-Unity-UDP7777-LAN'
$boidsLogPath = Join-Path $PSScriptRoot '../Logs/BoidsFirewallFix.log'
try {
    if (!(Test-Path -LiteralPath $boidsUnityPath)) { throw 'The expected Unity executable was not found.' }
    $boidsExisting = Get-NetFirewallRule -Name $boidsRuleName -ErrorAction SilentlyContinue
    if ($boidsExisting) {
        Set-NetFirewallRule -Name $boidsRuleName -Enabled True -Direction Inbound -Action Allow -Profile Private,Public -Program $boidsUnityPath -Protocol UDP -LocalPort 7777 -RemoteAddress LocalSubnet | Out-Null
    } else {
        New-NetFirewallRule -Name $boidsRuleName -DisplayName 'Gestural Boids - Unity UDP 7777 local network' -Enabled True -Direction Inbound -Action Allow -Profile Private,Public -Program $boidsUnityPath -Protocol UDP -LocalPort 7777 -RemoteAddress LocalSubnet | Out-Null
    }
    $boidsDisabled = @()
    Get-NetFirewallRule -Enabled True -Direction Inbound -Action Block | Where-Object { [string]$_.Profile -eq 'Public' } | ForEach-Object {
        $boidsCandidate = $_
        $boidsApp = $boidsCandidate | Get-NetFirewallApplicationFilter
        if ($boidsApp.Program -ieq $boidsUnityPath) {
            $boidsDisabled += $boidsCandidate.Name
            $boidsCandidate | Disable-NetFirewallRule | Out-Null
        }
    }
    @('SUCCESS: Unity UDP 7777 allowed from LocalSubnet on Private/Public networks.', 'Disabled conflicting Public block rule IDs:', ($boidsDisabled -join ', '), 'All other firewall profiles and application rules were left unchanged.') | Set-Content -LiteralPath $boidsLogPath
    exit 0
} catch {
    ('FAILED: ' + $_.Exception.Message) | Set-Content -LiteralPath $boidsLogPath
    exit 1
}
