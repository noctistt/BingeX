param([string]$SdkRoot='')
$ErrorActionPreference='Stop'
# Read-only. Does not install tools, accept terms, start adb, boot devices, or change Windows features.
$computer=Get-CimInstance Win32_ComputerSystem
$cpu=Get-CimInstance Win32_Processor | Select-Object -First 1
$os=Get-CimInstance Win32_OperatingSystem
$roots=@($SdkRoot,$env:ANDROID_HOME,$env:ANDROID_SDK_ROOT,(Join-Path $env:LOCALAPPDATA 'Android/Sdk')) | Where-Object {$_} | Select-Object -Unique
$found=$roots | Where-Object {Test-Path -LiteralPath $_ -PathType Container} | Select-Object -First 1
function Find-AndroidTool([string]$Relative,[string]$Name){
    if($found){$candidate=Join-Path $found $Relative;if(Test-Path -LiteralPath $candidate -PathType Leaf){return $candidate}}
    $command=Get-Command $Name -CommandType Application -ErrorAction SilentlyContinue | Select-Object -First 1
    if($command){return $command.Source}
    return $null
}
$adb=Find-AndroidTool 'platform-tools/adb.exe' 'adb.exe'
$emulator=Find-AndroidTool 'emulator/emulator.exe' 'emulator.exe'
$acceleration='Not checked: emulator tool unavailable'
if($emulator){
    $start=[Diagnostics.ProcessStartInfo]::new($emulator)
    $start.UseShellExecute=$false;$start.CreateNoWindow=$true;$start.RedirectStandardOutput=$true;$start.RedirectStandardError=$true
    $start.Arguments='-accel-check'
    $process=[Diagnostics.Process]::Start($start)
    try{
        $output=$process.StandardOutput.ReadToEndAsync();$errors=$process.StandardError.ReadToEndAsync()
        if(!$process.WaitForExit(10000)){$process.Kill($true);$acceleration='Acceleration check timed out'}
        else{$acceleration=if($process.ExitCode -eq 0){'Emulator reports acceleration available'}else{'Emulator reports acceleration unavailable; inspect its documented setup before changing Windows features'}}
        $null=$output.GetAwaiter().GetResult();$null=$errors.GetAwaiter().GetResult()
    }finally{$process.Dispose()}
}
[pscustomobject]@{
    Format='BingeX.AndroidPreflight';Version=1
    Cpu=$cpu.Name;TotalMemoryGB=[math]::Round($computer.TotalPhysicalMemory/1GB,1);FreeMemoryGB=[math]::Round($os.FreePhysicalMemory/1MB,1)
    HypervisorPresent=[bool]$computer.HypervisorPresent
    FirmwareVirtualizationReported=$cpu.VirtualizationFirmwareEnabled
    FirmwareReportCaveat='Firmware flags may be masked when a hypervisor is active; emulator acceleration check is authoritative for this experiment.'
    SdkFound=[bool]$found;AdbFound=[bool]$adb;EmulatorFound=[bool]$emulator;Acceleration=$acceleration
    RequiredToolsPresent=([bool]$adb -and [bool]$emulator);AndroidExecutionVerified=$false
    NextStep=if(!$adb -or !$emulator){'Prepare a dedicated Android test environment; SDK terms and system-image selection still need review.'}else{'Create an isolated test device and verify helper behavior and resource use before app integration.'}
} | ConvertTo-Json -Depth 3
