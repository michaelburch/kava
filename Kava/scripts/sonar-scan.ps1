#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs SonarQube analysis for this .NET solution with SonarScanner for .NET.
.PARAMETER SonarToken
    SonarQube authentication token. Falls back to $env:SONAR_TOKEN.
.PARAMETER SonarHostUrl
    SonarQube server URL. Defaults to https://sonar.burch.es.
.PARAMETER ProjectKey
    SonarQube project key. Defaults to kava.
.PARAMETER ProjectName
    SonarQube project display name. Defaults to Kava.
.PARAMETER Configuration
    Build configuration used during analysis. Defaults to Debug.
.PARAMETER SolutionPath
    Solution or project path to build during analysis. Defaults to Kava.slnx.
.PARAMETER CoverageExclusions
    Comma-separated Sonar coverage exclusion patterns for untested UI/integration layers.
#>
param(
    [string]$SonarToken = $env:SONAR_TOKEN,
    [string]$SonarHostUrl = "https://sonar.burch.es",
    [string]$ProjectKey = "kava",
    [string]$ProjectName = "Kava",
    [string]$Configuration = "Debug",
    [string]$SolutionPath = "Kava.slnx",
    [string]$CoverageExclusions = "src/Kava.Desktop/**/*.axaml,src/Kava.Desktop/App.axaml.cs,src/Kava.Desktop/FlyoutWindow.axaml.cs,src/Kava.Desktop/MainWindow.axaml.cs,src/Kava.Desktop/Program.cs,src/Kava.Desktop/ThemeHelper.cs,src/Kava.Desktop/TrayIconManager.cs",
    [int]$PollingIntervalSeconds = 5,
    [int]$PollingTimeoutSeconds = 300
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$script:SonarAuthHeader = "Basic {0}" -f [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("{0}:" -f $SonarToken))

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(Mandatory = $true)]
        [string[]]$ArgumentList,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Write-Host $Description -ForegroundColor Cyan
    & $FilePath @ArgumentList

    if ($LASTEXITCODE -ne 0) {
        throw "$Description failed with exit code $LASTEXITCODE"
    }
}

function Invoke-SonarApi {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [hashtable]$Query = @{}
    )

    $uriBuilder = [System.UriBuilder]::new($SonarHostUrl)
    $uriBuilder.Path = $Path.TrimStart('/')

    if ($Query.Count -gt 0) {
        $queryString = ($Query.GetEnumerator() | ForEach-Object {
            "{0}={1}" -f [Uri]::EscapeDataString([string]$_.Key), [Uri]::EscapeDataString([string]$_.Value)
        }) -join "&"

        $uriBuilder.Query = $queryString
    } else {
        $uriBuilder.Query = $null
    }

    Invoke-RestMethod -Method Get -Uri $uriBuilder.Uri -Headers @{ Authorization = $script:SonarAuthHeader }
}

function Read-KeyValueFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath
    )

    $values = @{}

    foreach ($line in Get-Content -Path $FilePath) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith("#")) {
            continue
        }

        $parts = $line -split "=", 2
        if ($parts.Count -eq 2) {
            $values[$parts[0]] = $parts[1]
        }
    }

    $values
}

function Get-ReportTaskPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectRoot
    )

    $candidates = @(
        (Join-Path $ProjectRoot ".sonarqube\out\.sonar\report-task.txt"),
        (Join-Path $ProjectRoot ".scannerwork\report-task.txt")
    )

    $existingCandidates = @(
        $candidates |
            Where-Object { Test-Path $_ } |
            Select-Object -Unique |
            Sort-Object {
                if ($_ -like "*.sonarqube\\out\\.sonar\\report-task.txt") { 0 } else { 1 }
            }, {
                (Get-Item $_).LastWriteTimeUtc
            } -Descending
    )

    if ($existingCandidates.Count -gt 0) {
        return $existingCandidates[0]
    }

    $discovered = Get-ChildItem -Path $ProjectRoot -Filter "report-task.txt" -Recurse -ErrorAction SilentlyContinue |
        Select-Object -First 1 -ExpandProperty FullName

    if ($discovered) {
        return $discovered
    }

    throw "Could not find SonarQube report-task.txt after analysis."
}

function Wait-ForCeTask {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CeTaskId
    )

    $deadline = (Get-Date).AddSeconds($PollingTimeoutSeconds)

    do {
        $response = Invoke-SonarApi -Path "/api/ce/task" -Query @{ id = $CeTaskId }
        $task = $response.task

        switch ($task.status) {
            "SUCCESS" { return $task }
            "FAILED" { throw "SonarQube background task failed." }
            "CANCELED" { throw "SonarQube background task was canceled." }
        }

        Start-Sleep -Seconds $PollingIntervalSeconds
    } while ((Get-Date) -lt $deadline)

    throw "Timed out waiting for SonarQube background task $CeTaskId."
}

function Get-IssueFilePath {
    param(
        [string]$Component
    )

    if (-not $Component) {
        return $null
    }

    $separatorIndex = $Component.IndexOf(':')
    if ($separatorIndex -ge 0 -and $separatorIndex -lt ($Component.Length - 1)) {
        return $Component.Substring($separatorIndex + 1)
    }

    return $Component
}

function Get-AllOpenIssues {
    $page = 1
    $pageSize = 500
    $issues = @()

    do {
        $response = Invoke-SonarApi -Path "/api/issues/search" -Query @{
            componentKeys = $ProjectKey
            resolved = "false"
            p = $page
            ps = $pageSize
            additionalFields = "_all"
        }

        $pageIssues = @($response.issues)
        if ($pageIssues.Count -gt 0) {
            $issues += $pageIssues
        }

        $page += 1
    } while (@($issues).Count -lt [int]$response.total)

    $issues
}

function Get-AllSecurityHotspots {
    try {
        $page = 1
        $pageSize = 500
        $hotspots = @()

        do {
            $response = Invoke-SonarApi -Path "/api/hotspots/search" -Query @{
                projectKey = $ProjectKey
                status = "TO_REVIEW"
                p = $page
                ps = $pageSize
            }

            $pageHotspots = @($response.hotspots)
            if ($pageHotspots.Count -gt 0) {
                $hotspots += $pageHotspots
            }

            $page += 1
        } while (@($hotspots).Count -lt [int]$response.paging.total)

        return [ordered]@{
            available = $true
            error = $null
            hotspots = @($hotspots)
        }
    }
    catch {
        return [ordered]@{
            available = $false
            error = $_.Exception.Message
            hotspots = @()
        }
    }
}

function Get-CountsByProperty {
    param(
        [AllowEmptyCollection()]
        [object[]]$Items,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    $counts = [ordered]@{}

    if ($null -eq $Items -or $Items.Count -eq 0) {
        return $counts
    }

    foreach ($item in $Items) {
        $value = [string]$item.$PropertyName
        if ([string]::IsNullOrWhiteSpace($value)) {
            $value = "UNKNOWN"
        }

        if (-not $counts.Contains($value)) {
            $counts[$value] = 0
        }

        $counts[$value] += 1
    }

    $counts
}

function Get-MeasureMap {
    param(
        [object[]]$Measures
    )

    $map = [ordered]@{}
    foreach ($measure in $Measures) {
        $resolvedValue = $null

        $valueProperty = $measure.PSObject.Properties["value"]
        if ($null -ne $valueProperty) {
            $resolvedValue = $valueProperty.Value
        }

        if ([string]::IsNullOrWhiteSpace([string]$resolvedValue)) {
            $periodProperty = $measure.PSObject.Properties["period"]
            if ($null -ne $periodProperty -and $null -ne $periodProperty.Value) {
                $periodValueProperty = $periodProperty.Value.PSObject.Properties["value"]
                if ($null -ne $periodValueProperty) {
                    $resolvedValue = $periodValueProperty.Value
                }
            }
        }

        if ([string]::IsNullOrWhiteSpace([string]$resolvedValue)) {
            $periodsProperty = $measure.PSObject.Properties["periods"]
            if ($null -ne $periodsProperty -and $null -ne $periodsProperty.Value) {
                $firstPeriod = @($periodsProperty.Value) | Select-Object -First 1
                if ($null -ne $firstPeriod) {
                    $periodValueProperty = $firstPeriod.PSObject.Properties["value"]
                    if ($null -ne $periodValueProperty) {
                        $resolvedValue = $periodValueProperty.Value
                    }
                }
            }
        }

        if ($null -eq $resolvedValue) {
            $resolvedValue = "0"
        }

        $map[$measure.metric] = [string]$resolvedValue
    }

    $map
}

function Get-MeasureValue {
    param(
        [Parameter(Mandatory = $true)]
        $MeasureMap,

        [Parameter(Mandatory = $true)]
        [string]$MetricKey,

        [string]$DefaultValue = "0"
    )

    if ($null -ne $MeasureMap -and $MeasureMap.Contains($MetricKey)) {
        return [string]$MeasureMap[$MetricKey]
    }

    return $DefaultValue
}

function Get-DuplicationDetails {
    try {
        $page = 1
        $pageSize = 500
        $components = @()

        do {
            $response = Invoke-SonarApi -Path "/api/measures/component_tree" -Query @{
                component = $ProjectKey
                metricKeys = "duplicated_lines_density,duplicated_lines,duplicated_blocks,ncloc"
                qualifiers = "FIL"
                strategy = "leaves"
                p = $page
                ps = $pageSize
                s = "metric"
                metricSort = "duplicated_lines_density"
            }

            $pageComponents = @($response.components)
            if ($pageComponents.Count -gt 0) {
                $components += $pageComponents
            }

            $page += 1
        } while (@($components).Count -lt [int]$response.paging.total)

        $duplicatedFiles = @(
            foreach ($component in $components) {
                $measureMap = Get-MeasureMap -Measures @($component.measures)
                $duplicatedLinesDensity = Get-MeasureValue -MeasureMap $measureMap -MetricKey "duplicated_lines_density"
                $duplicatedLines = Get-MeasureValue -MeasureMap $measureMap -MetricKey "duplicated_lines"
                $duplicatedBlocks = Get-MeasureValue -MeasureMap $measureMap -MetricKey "duplicated_blocks"
                $ncloc = Get-MeasureValue -MeasureMap $measureMap -MetricKey "ncloc"

                if ([double]$duplicatedLinesDensity -le 0 -and [int]$duplicatedLines -le 0 -and [int]$duplicatedBlocks -le 0) {
                    continue
                }

                [ordered]@{
                    key = $component.key
                    file = Get-IssueFilePath -Component $component.key
                    duplicatedLinesDensity = $duplicatedLinesDensity
                    duplicatedLines = $duplicatedLines
                    duplicatedBlocks = $duplicatedBlocks
                    ncloc = $ncloc
                }
            }
        )

        return [ordered]@{
            available = $true
            error = $null
            totalFiles = @($duplicatedFiles).Count
            files = @(
                $duplicatedFiles |
                    Sort-Object {
                        [double]$_.duplicatedLinesDensity
                    }, {
                        [int]$_.duplicatedLines
                    } -Descending
            )
        }
    }
    catch {
        return [ordered]@{
            available = $false
            error = $_.Exception.Message
            totalFiles = 0
            files = @()
        }
    }
}

function Get-CoverageDetails {
    try {
        $page = 1
        $pageSize = 500
        $components = @()

        do {
            $response = Invoke-SonarApi -Path "/api/measures/component_tree" -Query @{
                component = $ProjectKey
                metricKeys = "coverage,new_coverage,uncovered_lines,new_uncovered_lines,lines_to_cover,new_lines"
                qualifiers = "FIL"
                strategy = "leaves"
                p = $page
                ps = $pageSize
                s = "metric"
                metricSort = "new_coverage"
            }

            $pageComponents = @($response.components)
            if ($pageComponents.Count -gt 0) {
                $components += $pageComponents
            }

            $page += 1
        } while (@($components).Count -lt [int]$response.paging.total)

        $files = @(
            foreach ($component in $components) {
                $measureMap = Get-MeasureMap -Measures @($component.measures)

                [ordered]@{
                    key = $component.key
                    file = Get-IssueFilePath -Component $component.key
                    coverage = Get-MeasureValue -MeasureMap $measureMap -MetricKey "coverage"
                    newCoverage = Get-MeasureValue -MeasureMap $measureMap -MetricKey "new_coverage"
                    linesToCover = Get-MeasureValue -MeasureMap $measureMap -MetricKey "lines_to_cover"
                    uncoveredLines = Get-MeasureValue -MeasureMap $measureMap -MetricKey "uncovered_lines"
                    newLines = Get-MeasureValue -MeasureMap $measureMap -MetricKey "new_lines"
                    newUncoveredLines = Get-MeasureValue -MeasureMap $measureMap -MetricKey "new_uncovered_lines"
                }
            }
        )

        $newCodeFiles = @(
            $files |
                Where-Object { [int]$_.newLines -gt 0 } |
                Sort-Object @(
                    @{ Expression = { [double]$_.newCoverage } },
                    @{ Expression = { [int]$_.newUncoveredLines }; Descending = $true },
                    @{ Expression = { [int]$_.newLines }; Descending = $true }
                )
        )

        return [ordered]@{
            available = $true
            error = $null
            totalFiles = @($files).Count
            files = $files
            newCodeFiles = $newCodeFiles
        }
    }
    catch {
        return [ordered]@{
            available = $false
            error = $_.Exception.Message
            totalFiles = 0
            files = @()
            newCodeFiles = @()
        }
    }
}

function Get-OptionalPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$PropertyName,

        $DefaultValue = $null
    )

    if ($null -eq $InputObject) {
        return $DefaultValue
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        return $DefaultValue
    }

    return $property.Value
}

function New-SummaryMarkdown {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Summary,

        [AllowEmptyCollection()]
        [object[]]$IssueRecords
    )

    $lines = @(
        "# SonarQube Summary",
        "",
        "Generated: $($Summary.generatedAt)",
        "Project: $($Summary.projectName) ($($Summary.projectKey))",
        "Server: $($Summary.serverUrl)",
        "Dashboard: $($Summary.dashboardUrl)",
        "Quality Gate: $($Summary.qualityGate.status)",
        "",
        "## Key Metrics",
        "",
        "| Metric | Value |",
        "| --- | --- |"
    )

    foreach ($metric in $Summary.measures.GetEnumerator()) {
        $lines += "| $($metric.Key) | $($metric.Value) |"
    }

    $lines += @(
        "",
        "## Coverage Drivers",
        ""
    )

    if ($Summary.coverageDetails.available) {
        $lines += "Files with new code: $(@($Summary.coverageDetails.newCodeFiles).Count)"

        if (@($Summary.coverageDetails.newCodeFiles).Count -gt 0) {
            $lines += @(
                "",
                "| File | New Coverage | New Lines | New Uncovered | Coverage | Uncovered | Lines To Cover |",
                "| --- | --- | --- | --- | --- | --- | --- |"
            )

            foreach ($file in @($Summary.coverageDetails.newCodeFiles | Select-Object -First 20)) {
                $filePath = if ($file.file) { $file.file.Replace("|", "\|") } else { "" }
                $lines += "| $filePath | $($file.newCoverage)% | $($file.newLines) | $($file.newUncoveredLines) | $($file.coverage)% | $($file.uncoveredLines) | $($file.linesToCover) |"
            }

            if (@($Summary.coverageDetails.newCodeFiles).Count -gt 20) {
                $lines += ""
                $lines += "Only the first 20 new-code coverage drivers are shown here. See sonar-summary.json for the full file list."
            }
        }
    }
    else {
        $lines += "Coverage driver details unavailable: $($Summary.coverageDetails.error)"
    }

    $lines += @(
        "",
        "## Open Issues",
        "",
        "Total: $($Summary.issueSummary.total)",
        "",
        "### By Severity",
        ""
    )

    foreach ($severity in $Summary.issueSummary.bySeverity.GetEnumerator()) {
        $lines += "- $($severity.Key): $($severity.Value)"
    }

    $lines += @(
        "",
        "### By Type",
        ""
    )

    foreach ($type in $Summary.issueSummary.byType.GetEnumerator()) {
        $lines += "- $($type.Key): $($type.Value)"
    }

    $lines += @(
        "",
        "## Duplications",
        ""
    )

    if ($Summary.duplicationDetails.available) {
        $lines += "Overall duplicated lines density: $($Summary.measures.duplicated_lines_density)%"
        $lines += "Files with duplication: $($Summary.duplicationDetails.totalFiles)"

        if ($Summary.duplicationDetails.totalFiles -gt 0) {
            $lines += @(
                "",
                "| File | Density | Duplicated Lines | Blocks | NCLOC |",
                "| --- | --- | --- | --- | --- |"
            )

            foreach ($file in @($Summary.duplicationDetails.files | Select-Object -First 20)) {
                $filePath = if ($file.file) { $file.file.Replace("|", "\|") } else { "" }
                $lines += "| $filePath | $($file.duplicatedLinesDensity)% | $($file.duplicatedLines) | $($file.duplicatedBlocks) | $($file.ncloc) |"
            }

            if (@($Summary.duplicationDetails.files).Count -gt 20) {
                $lines += ""
                $lines += "Only the first 20 duplicated files are shown here. See sonar-summary.json for the full duplication list."
            }
        }
    }
    else {
        $lines += "Duplication details unavailable: $($Summary.duplicationDetails.error)"
    }

    $lines += @(
        "",
        "## Security Hotspots",
        ""
    )

    if ($Summary.securityHotspots.available) {
        $lines += "Total TO_REVIEW: $($Summary.securityHotspots.total)"

        if ($Summary.securityHotspots.total -gt 0) {
            $lines += @(
                "",
                "### By Probability",
                ""
            )

            foreach ($probability in $Summary.securityHotspots.byProbability.GetEnumerator()) {
                $lines += "- $($probability.Key): $($probability.Value)"
            }

            $lines += @(
                "",
                "### Sample Hotspots",
                "",
                "| Probability | File | Line | Rule | Message |",
                "| --- | --- | --- | --- | --- |"
            )

            foreach ($hotspot in @($Summary.securityHotspots.hotspots | Select-Object -First 20)) {
                $message = ($hotspot.message -replace "\r?\n", " ").Replace("|", "\|")
                $file = if ($hotspot.file) { $hotspot.file.Replace("|", "\|") } else { "" }
                $lines += "| $($hotspot.vulnerabilityProbability) | $file | $($hotspot.line) | $($hotspot.rule) | $message |"
            }

            if (@($Summary.securityHotspots.hotspots).Count -gt 20) {
                $lines += ""
                $lines += "Only the first 20 security hotspots are shown here. See sonar-summary.json for the full hotspot list."
            }
        }
    }
    else {
        $lines += "Security hotspot details unavailable: $($Summary.securityHotspots.error)"
    }

    $lines += @(
        "",
        "### Sample Findings",
        "",
        "| Severity | Type | File | Line | Rule | Message |",
        "| --- | --- | --- | --- | --- | --- |"
    )

    if (@($IssueRecords).Count -eq 0) {
        $lines += "| - | - | - | - | - | No open issues |"
    }
    else {
        foreach ($issue in @($IssueRecords | Select-Object -First 50)) {
            $message = ($issue.message -replace "\r?\n", " ").Replace("|", "\|")
            $file = if ($issue.file) { $issue.file.Replace("|", "\|") } else { "" }
            $lines += "| $($issue.severity) | $($issue.type) | $file | $($issue.line) | $($issue.rule) | $message |"
        }
    }

    if (@($IssueRecords).Count -gt 50) {
        $lines += ""
        $lines += "Only the first 50 issues are shown here. See sonar-summary.json for the full issue list."
    }

    $lines -join [Environment]::NewLine
}

if (-not $SonarToken) {
    Write-Error "SONAR_TOKEN is not set. Pass -SonarToken or set `$env:SONAR_TOKEN."
    exit 1
}

$projectRoot = Split-Path $PSScriptRoot -Parent
$resolvedSolutionPath = Join-Path $projectRoot $SolutionPath

if (-not (Test-Path $resolvedSolutionPath)) {
    Write-Error "Solution path not found: $resolvedSolutionPath"
    exit 1
}

$toolPath = Join-Path $projectRoot ".sonar\tools"
$outputPath = Join-Path $projectRoot ".sonar\output"
$testResultsPath = Join-Path $projectRoot "TestResults"
$invocationRoot = (Get-Location).Path
$null = New-Item -ItemType Directory -Path $toolPath -Force
$null = New-Item -ItemType Directory -Path $outputPath -Force
$null = New-Item -ItemType Directory -Path $testResultsPath -Force

$scannerExecutable = Join-Path $toolPath "dotnet-sonarscanner.exe"
if (-not (Test-Path $scannerExecutable)) {
    $scannerExecutable = Join-Path $toolPath "dotnet-sonarscanner"
}

if (Test-Path $scannerExecutable) {
    Invoke-Step -FilePath "dotnet" -ArgumentList @("tool", "update", "--tool-path", $toolPath, "dotnet-sonarscanner") -Description "Updating SonarScanner for .NET"
} else {
    Invoke-Step -FilePath "dotnet" -ArgumentList @("tool", "install", "--tool-path", $toolPath, "dotnet-sonarscanner") -Description "Installing SonarScanner for .NET"
}

$scannerExecutable = Join-Path $toolPath "dotnet-sonarscanner.exe"
if (-not (Test-Path $scannerExecutable)) {
    $scannerExecutable = Join-Path $toolPath "dotnet-sonarscanner"
}

if (-not (Test-Path $scannerExecutable)) {
    Write-Error "Could not locate dotnet-sonarscanner after installation."
    exit 1
}

$analysisStarted = $false
$analysisFinalized = $false

try {
    Push-Location $projectRoot

    $sonarBeginArguments = @(
        "begin",
        "/k:$ProjectKey",
        "/n:$ProjectName",
        "/d:sonar.host.url=$SonarHostUrl",
        "/d:sonar.token=$SonarToken",
        "/d:sonar.cs.opencover.reportsPaths=**/coverage.opencover.xml"
    )

    if (-not [string]::IsNullOrWhiteSpace($CoverageExclusions)) {
        $sonarBeginArguments += "/d:sonar.coverage.exclusions=$CoverageExclusions"
    }

    Invoke-Step -FilePath $scannerExecutable -ArgumentList $sonarBeginArguments -Description "Starting SonarQube analysis"

    $analysisStarted = $true

    Invoke-Step -FilePath "dotnet" -ArgumentList @("build", $resolvedSolutionPath, "-c", $Configuration) -Description "Building solution for analysis"

    Get-ChildItem -Path $projectRoot -Filter "coverage.opencover.xml" -Recurse -ErrorAction SilentlyContinue |
        Remove-Item -Force -ErrorAction SilentlyContinue

    Invoke-Step -FilePath "dotnet" -ArgumentList @(
        "test",
        $resolvedSolutionPath,
        "-c",
        $Configuration,
        "--no-build",
        "--results-directory",
        $testResultsPath,
        "--collect:XPlat Code Coverage;Format=opencover"
    ) -Description "Running tests with coverage collection"

    Invoke-Step -FilePath $scannerExecutable -ArgumentList @(
        "end",
        "/d:sonar.token=$SonarToken"
    ) -Description "Finalizing SonarQube analysis"

    $analysisFinalized = $true

    $reportTaskPath = Get-ReportTaskPath -ProjectRoot $projectRoot
    $reportTask = Read-KeyValueFile -FilePath $reportTaskPath

    if (-not $reportTask.ContainsKey("ceTaskId")) {
        throw "SonarQube report-task.txt did not contain ceTaskId."
    }

    Write-Host "Waiting for SonarQube background processing..." -ForegroundColor Cyan
    $ceTask = Wait-ForCeTask -CeTaskId $reportTask["ceTaskId"]

    $qualityGate = if ($ceTask.analysisId) {
        Invoke-SonarApi -Path "/api/qualitygates/project_status" -Query @{ analysisId = $ceTask.analysisId }
    } else {
        Invoke-SonarApi -Path "/api/qualitygates/project_status" -Query @{ projectKey = $ProjectKey }
    }

    $measuresResponse = Invoke-SonarApi -Path "/api/measures/component" -Query @{
        component = $ProjectKey
        metricKeys = "alert_status,bugs,vulnerabilities,code_smells,coverage,new_coverage,lines_to_cover,uncovered_lines,new_lines,new_uncovered_lines,duplicated_lines_density,new_duplicated_lines_density,ncloc,reliability_rating,security_rating,sqale_rating"
    }

    $issues = @(Get-AllOpenIssues)
    $securityHotspots = Get-AllSecurityHotspots
    $duplicationDetails = Get-DuplicationDetails
    $coverageDetails = Get-CoverageDetails
    $issueRecords = @(
        foreach ($issue in $issues) {
            [ordered]@{
                key = $issue.key
                severity = $issue.severity
                type = $issue.type
                rule = $issue.rule
                status = $issue.status
                file = Get-IssueFilePath -Component $issue.component
                line = $issue.line
                message = $issue.message
                effort = $issue.effort
                cleanCodeAttribute = $issue.cleanCodeAttribute
                tags = @($issue.tags)
                creationDate = $issue.creationDate
                updateDate = $issue.updateDate
            }
        }
    )
    $securityHotspotRecords = @(
        foreach ($hotspot in @($securityHotspots.hotspots)) {
            [ordered]@{
                key = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "key"
                vulnerabilityProbability = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "vulnerabilityProbability" -DefaultValue "UNKNOWN"
                status = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "status" -DefaultValue "UNKNOWN"
                resolution = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "resolution"
                rule = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "ruleKey"
                file = Get-IssueFilePath -Component (Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "component")
                line = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "line"
                message = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "message" -DefaultValue ""
                creationDate = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "creationDate"
                updateDate = Get-OptionalPropertyValue -InputObject $hotspot -PropertyName "updateDate"
            }
        }
    )

    $summary = [ordered]@{
        generatedAt = (Get-Date).ToString("o")
        projectKey = $ProjectKey
        projectName = $ProjectName
        serverUrl = $SonarHostUrl
        dashboardUrl = $reportTask["dashboardUrl"]
        ceTask = [ordered]@{
            id = $ceTask.id
            status = $ceTask.status
            submittedAt = $ceTask.submittedAt
            startedAt = $ceTask.startedAt
            executedAt = $ceTask.executedAt
            analysisId = $ceTask.analysisId
        }
        qualityGate = [ordered]@{
            status = $qualityGate.projectStatus.status
            conditions = @($qualityGate.projectStatus.conditions)
        }
        measures = Get-MeasureMap -Measures $measuresResponse.component.measures
        issueSummary = [ordered]@{
            total = @($issueRecords).Count
            bySeverity = Get-CountsByProperty -Items $issueRecords -PropertyName "severity"
            byType = Get-CountsByProperty -Items $issueRecords -PropertyName "type"
        }
        coverageDetails = $coverageDetails
        duplicationDetails = $duplicationDetails
        securityHotspots = [ordered]@{
            available = $securityHotspots.available
            error = $securityHotspots.error
            total = @($securityHotspotRecords).Count
            byProbability = if ($securityHotspots.available) {
                Get-CountsByProperty -Items $securityHotspotRecords -PropertyName "vulnerabilityProbability"
            }
            else {
                [ordered]@{}
            }
            byStatus = if ($securityHotspots.available) {
                Get-CountsByProperty -Items $securityHotspotRecords -PropertyName "status"
            }
            else {
                [ordered]@{}
            }
            hotspots = $securityHotspotRecords
        }
        issues = $issueRecords
    }

    $summaryJsonPath = Join-Path $outputPath "sonar-summary.json"
    $summaryMarkdownPath = Join-Path $outputPath "sonar-summary.md"

    $summary | ConvertTo-Json -Depth 8 | Set-Content -Path $summaryJsonPath -Encoding utf8
    New-SummaryMarkdown -Summary $summary -IssueRecords $issueRecords | Set-Content -Path $summaryMarkdownPath -Encoding utf8

    Write-Host "Wrote SonarQube summary to $summaryMarkdownPath" -ForegroundColor Green
}
catch {
    if ($analysisStarted -and -not $analysisFinalized) {
        try {
            & $scannerExecutable end "/d:sonar.token=$SonarToken" | Out-Null
        }
        catch {
        }
    }

    Write-Error $_
    exit 1
}
finally {
    if ((Get-Location).Path -ne $invocationRoot) {
        Pop-Location
    }
}

Write-Host "SonarQube scan completed successfully." -ForegroundColor Green
