param(
    [string]$SourceDirectory = 'C:\Users\eaima\OneDrive\Desktop\localidades',
    [string]$WorkspaceRoot = 'E:\Proyectos\ventamap',
    [string]$OutputSqlFile = 'C:\Users\eaima\OneDrive\Desktop\localidades\argentine_localities_inserts.sql',
    [string]$OutputUnresolvedFile = 'C:\Users\eaima\OneDrive\Desktop\localidades\argentine_localities_unresolved.csv',
    [string]$GeorefCsvFile = 'E:\Proyectos\ventamap\.codextemp\georef_localidades.csv'
)

$ErrorActionPreference = 'Stop'

function Repair-Text([string]$Text) {
    if ([string]::IsNullOrEmpty($Text)) { return $Text }

    $charA = [string][char]0x00C3
    $charB = [string][char]0x00C2
    $charReplacement = [string][char]0xFFFD

    if ($Text.Contains($charA) -or $Text.Contains($charB) -or $Text.Contains($charReplacement)) {
        try {
            $bytes = [System.Text.Encoding]::GetEncoding(1252).GetBytes($Text)
            return [System.Text.Encoding]::UTF8.GetString($bytes)
        }
        catch {
            return $Text
        }
    }

    return $Text
}

function Normalize-Text([string]$Text) {
    if ([string]::IsNullOrWhiteSpace($Text)) { return '' }

    $value = (Repair-Text $Text).ToUpperInvariant()
    $value = $value.Normalize([System.Text.NormalizationForm]::FormD)

    $builder = New-Object System.Text.StringBuilder
    foreach ($char in $value.ToCharArray()) {
        $category = [Globalization.CharUnicodeInfo]::GetUnicodeCategory($char)
        if ($category -ne [Globalization.UnicodeCategory]::NonSpacingMark) {
            [void]$builder.Append($char)
        }
    }

    $value = $builder.ToString().Normalize([System.Text.NormalizationForm]::FormC)
    $value = $value -replace '\([^)]*\)', ' '
    $value = $value -replace '\bESTACION\b', ' '
    $value = $value -replace '\bEST\b', ' '
    $value = $value -replace '\bGRL\b', 'GENERAL '
    $value = $value -replace '\bGDOR\b', 'GOVERNOR '
    $value = $value -replace '\bPTE\b', 'PRESIDENTE '
    $value = $value -replace '\bCNEL\b', 'CORONEL '
    $value = $value -replace '\bTTE\b', 'TENIENTE '
    $value = $value -replace '[^A-Z0-9 ]', ' '
    $value = $value -replace '\s+', ' '

    return $value.Trim()
}

function Normalize-Province([string]$Province) {
    $value = Normalize-Text $Province

    switch ($value) {
        'CAPITAL FEDERAL' { return 'CIUDAD AUTONOMA DE BUENOS AIRES' }
        'CIUDAD DE BUENOS AIRES' { return 'CIUDAD AUTONOMA DE BUENOS AIRES' }
        default { return $value }
    }
}

function Simplify-Locality([string]$Locality) {
    $value = Normalize-Text $Locality
    $value = $value -replace '\bDE LOS\b', ' '
    $value = $value -replace '\bDE LAS\b', ' '
    $value = $value -replace '\bDE LA\b', ' '
    $value = $value -replace '\bDEL\b', ' '
    $value = $value -replace '\bDE\b', ' '
    $value = $value -replace '\bLA\b', ' '
    $value = $value -replace '\bLAS\b', ' '
    $value = $value -replace '\bLOS\b', ' '
    $value = $value -replace '\bEL\b', ' '
    $value = $value -replace '\s+', ' '
    return $value.Trim()
}

function Get-CategoryPriority([string]$Category) {
    $value = Normalize-Text $Category

    switch ($value) {
        'LOCALIDAD SIMPLE' { return 400 }
        'COMPONENTE DE LOCALIDAD COMPUESTA' { return 300 }
        'LOCALIDAD COMPUESTA' { return 250 }
        'ENTIDAD' { return 150 }
        default { return 0 }
    }
}

function Get-LocalityAliases([string]$Locality) {
    $raw = Repair-Text $Locality
    $aliases = New-Object System.Collections.Generic.List[string]
    $aliases.Add($raw) | Out-Null

    $normalized = Normalize-Text $raw
    $simplified = Simplify-Locality $raw

    $candidateTexts = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)

    $manualAliases = @{
        'ACASSUSO' = @('Acasusso')
        'BOULOGNE' = @('Boulogne Sur Mer')
        'GUILLERMO E HUDSON' = @('Guillermo Enrique Hudson')
        'MAQUINISTA F SAVIO' = @('Maquinista Francisco Savio')
        'GENERAL O BRIEN' = @('General O''Brien')
        'O HIGGINS' = @('O''Higgins')
        'BARRIO BATAN' = @('Batan')
        'BALNEARIO CLAROMECO' = @('Claromeco')
        'BALNEARIO RETA' = @('Reta')
        'AEROPUERTO EZEIZA' = @('Ezeiza')
        'MERCADO CENTRAL' = @('Villa Celina')
        'LISANDRO OLMOS ETCHEVERRY' = @('Lisandro Olmos')
        'VILLA ICHO CRUZ' = @('Icho Cruz')
        'VILLA CIUDAD DE AMERICA' = @('Villa Ciudad Parque Los Reartes', 'Ciudad de America')
        'VILLA ESQUIU' = @('Villa Esquiu')
    }

    if ($manualAliases.ContainsKey($normalized)) {
        foreach ($alias in $manualAliases[$normalized]) {
            [void]$candidateTexts.Add($alias)
        }
    }

    if ($normalized -match '^BARRIO\s+(.+)$') { [void]$candidateTexts.Add($matches[1]) }
    if ($normalized -match '^BALNEARIO\s+(.+)$') { [void]$candidateTexts.Add($matches[1]) }
    if ($normalized -match '^AEROPUERTO\s+(.+)$') { [void]$candidateTexts.Add($matches[1]) }
    if ($normalized -match '^BASE AERONAVAL\s+(.+)$') { [void]$candidateTexts.Add($matches[1]) }

    $withoutParentheses = (($raw -replace '\([^)]*\)', ' ') -replace '\s+', ' ').Trim()
    if (-not [string]::IsNullOrWhiteSpace($withoutParentheses) -and $withoutParentheses -ne $raw) {
        [void]$candidateTexts.Add($withoutParentheses)
    }

    $withoutHyphen = (($withoutParentheses -replace '\s*-\s*', ' ') -replace '\s+', ' ').Trim()
    if (-not [string]::IsNullOrWhiteSpace($withoutHyphen)) {
        [void]$candidateTexts.Add($withoutHyphen)
    }

    if ($simplified -ne $normalized -and -not [string]::IsNullOrWhiteSpace($simplified)) {
        [void]$candidateTexts.Add($simplified)
    }

    foreach ($text in $candidateTexts) {
        if (-not [string]::IsNullOrWhiteSpace($text)) {
            $aliases.Add($text) | Out-Null
        }
    }

    return @($aliases | Select-Object -Unique)
}

function Get-ProvinceQueryName([string]$Province) {
    $normalized = Normalize-Province $Province

    switch ($normalized) {
        'CIUDAD AUTONOMA DE BUENOS AIRES' { return 'caba' }
        default { return (Repair-Text $Province) }
    }
}

function Escape-Sql([string]$Text) {
    return $Text.Replace("'", "''")
}

function Choose-Candidate($Candidates, [string]$Province, [string]$Locality) {
    if ($null -eq $Candidates -or $Candidates.Count -eq 0) { return $null }
    if ($Candidates.Count -eq 1) { return $Candidates[0] }

    $targetProvince = Normalize-Province $Province
    $targetLocality = Normalize-Text $Locality
    $targetSimple = Simplify-Locality $Locality

    $scored = @(
        foreach ($candidate in $Candidates) {
            $candidateProvince = Normalize-Province $candidate.provincia_nombre
            $candidateLocality = Normalize-Text $candidate.nombre
            $candidateSimple = Simplify-Locality $candidate.nombre
            $candidateGov = Normalize-Text $candidate.gobierno_local_nombre
            $candidateCensal = Normalize-Text $candidate.localidad_censal_nombre

            $score = 0
            if ($candidateProvince -eq $targetProvince) { $score += 100 }
            if ($candidateLocality -eq $targetLocality) { $score += 80 }
            if ($candidateSimple -eq $targetSimple) { $score += 40 }
            if ($candidateGov -eq $targetLocality) { $score += 40 }
            if ($candidateCensal -eq $targetLocality) { $score += 40 }
            if ($candidateLocality.StartsWith($targetLocality) -or $targetLocality.StartsWith($candidateLocality)) { $score += 10 }
            if ($candidateSimple.StartsWith($targetSimple) -or $targetSimple.StartsWith($candidateSimple)) { $score += 5 }
            $score += Get-CategoryPriority $candidate.categoria

            [pscustomobject]@{
                Score = $score
                Candidate = $candidate
            }
        }
    ) | Sort-Object Score -Descending

    if ($scored.Count -eq 0) { return $null }
    if ($scored[0].Score -le 0) { return $null }
    if ($scored.Count -gt 1 -and $scored[0].Score -eq $scored[1].Score) { return $null }

    return $scored[0].Candidate
}

function Invoke-GeorefLookup([string]$Province, [string]$Locality) {
    $provinceQuery = [System.Uri]::EscapeDataString((Get-ProvinceQueryName $Province))
    $localityQuery = [System.Uri]::EscapeDataString((Repair-Text $Locality))
    $uri = 'https://apis.datos.gob.ar/georef/api/v2.0/localidades?provincia=' + $provinceQuery + '&nombre=' + $localityQuery + '&max=5'

    try {
        $response = Invoke-RestMethod -Uri $uri -Method Get
    }
    catch {
        return $null
    }

    if ($null -eq $response.localidades) { return $null }
    return Choose-Candidate -Candidates $response.localidades -Province $Province -Locality $Locality
}

$provFile = Join-Path $SourceDirectory 'provincias.sql'
$locFile = Join-Path $SourceDirectory 'localidades.sql'

$provincesText = [System.IO.File]::ReadAllText($provFile, [System.Text.Encoding]::UTF8)
$localitiesText = [System.IO.File]::ReadAllText($locFile, [System.Text.Encoding]::UTF8)
$catalogText = [System.IO.File]::ReadAllText((Join-Path $WorkspaceRoot 'VentaMap.Web\Data\ArgentineLocalityCatalog.cs'), [System.Text.Encoding]::UTF8)
$georefCsvText = [System.IO.File]::ReadAllText($GeorefCsvFile, [System.Text.Encoding]::UTF8)

$provincePattern = 'INSERT INTO `ma_Provincias` \(`Id`, `Nombre`\) VALUES \((\d+), ''((?:''''|[^''])*)''\);'
$localityPattern = 'INSERT INTO `ma_Localidades` \(`Id`, `ProvinciaId`, `Nombre`, `CodigoPostal`\) VALUES \((\d+), (\d+), ''((?:''''|[^''])*)'', ''((?:''''|[^''])*)''\);'
$catalogPattern = 'Create\("([^"]+)", "([^"]+)"'

$provinceMatches = [regex]::Matches($provincesText, $provincePattern)
$localityMatches = [regex]::Matches($localitiesText, $localityPattern)
$catalogMatches = [regex]::Matches($catalogText, $catalogPattern)

$provinceMap = @{}
foreach ($match in $provinceMatches) {
    $provinceId = [int]$match.Groups[1].Value
    $provinceName = Repair-Text($match.Groups[2].Value.Replace("''", "'"))
    $provinceMap[$provinceId] = $provinceName
}

$existingPairs = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)
foreach ($match in $catalogMatches) {
    $catalogLocality = $match.Groups[1].Value
    $catalogProvince = $match.Groups[2].Value
    [void]$existingPairs.Add((Normalize-Province $catalogProvince) + '|' + (Normalize-Text $catalogLocality))
}

$georefRows = $georefCsvText | ConvertFrom-Csv
$georefByKey = @{}
foreach ($row in $georefRows) {
    $provinceName = Repair-Text $row.provincia_nombre
    $provinceKey = Normalize-Province $provinceName
    $nameVariants = @(
        (Repair-Text $row.nombre),
        (Repair-Text $row.localidad_censal_nombre),
        (Repair-Text $row.gobierno_local_nombre)
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Select-Object -Unique

    foreach ($nameVariant in $nameVariants) {
        $key = $provinceKey + '|' + (Normalize-Text $nameVariant)

        if (-not $georefByKey.ContainsKey($key)) {
            $georefByKey[$key] = New-Object System.Collections.ArrayList
        }

        [void]$georefByKey[$key].Add($row)
    }
}

$sqlBuilder = New-Object System.Text.StringBuilder
[void]$sqlBuilder.AppendLine('-- Generated automatically on 2026-08-12')
[void]$sqlBuilder.AppendLine('-- Coordinate source: GeorefAR https://apis.datos.gob.ar/georef/api/v2.0/localidades.csv')
[void]$sqlBuilder.AppendLine('-- Input source: provincias.sql + localidades.sql')
[void]$sqlBuilder.AppendLine('')

$unresolved = New-Object System.Collections.Generic.List[object]
$seenOutputPairs = New-Object 'System.Collections.Generic.HashSet[string]' ([System.StringComparer]::OrdinalIgnoreCase)

$sortOrder = 1000
$insertCount = 0
$resolvedFromCsv = 0
$resolvedFromApi = 0
$skippedExisting = 0
$skippedDuplicate = 0

foreach ($match in $localityMatches) {
    $provinceId = [int]$match.Groups[2].Value
    $provinceName = $provinceMap[$provinceId]
    if ([string]::IsNullOrWhiteSpace($provinceName)) { continue }

    $sourceLocalityId = [int]$match.Groups[1].Value
    $localityName = Repair-Text($match.Groups[3].Value.Replace("''", "'"))
    $postalCode = $match.Groups[4].Value.Replace("''", "'")

    $provinceKey = Normalize-Province $provinceName
    $pairKey = $provinceKey + '|' + (Normalize-Text $localityName)
    if ($existingPairs.Contains($pairKey)) {
        $skippedExisting++
        continue
    }

    if (-not $seenOutputPairs.Add($pairKey)) {
        $skippedDuplicate++
        continue
    }

    $candidate = $null
    $aliasInputs = Get-LocalityAliases $localityName
    foreach ($aliasInput in $aliasInputs) {
        $aliasKey = $provinceKey + '|' + (Normalize-Text $aliasInput)
        if ($georefByKey.ContainsKey($aliasKey)) {
            $candidate = Choose-Candidate -Candidates $georefByKey[$aliasKey] -Province $provinceName -Locality $aliasInput
            if ($null -ne $candidate) {
                $resolvedFromCsv++
                break
            }
        }
    }

    if ($null -eq $candidate) {
        foreach ($aliasInput in $aliasInputs) {
            $candidate = Invoke-GeorefLookup -Province $provinceName -Locality $aliasInput
            if ($null -ne $candidate) {
                $resolvedFromApi++
                break
            }
        }
    }

    if ($null -eq $candidate -or [string]::IsNullOrWhiteSpace($candidate.centroide_lat) -or [string]::IsNullOrWhiteSpace($candidate.centroide_lon)) {
        $unresolved.Add([pscustomobject]@{
            SourceLocalityId = $sourceLocalityId
            Province = $provinceName
            Locality = $localityName
            PostalCode = $postalCode
        }) | Out-Null
        continue
    }

    $latitude = [double]::Parse([string]$candidate.centroide_lat, [System.Globalization.CultureInfo]::InvariantCulture)
    $longitude = [double]::Parse([string]$candidate.centroide_lon, [System.Globalization.CultureInfo]::InvariantCulture)

    $safeLocality = Escape-Sql $localityName
    $safeProvince = Escape-Sql $provinceName
    $latitudeText = $latitude.ToString('0.000000', [System.Globalization.CultureInfo]::InvariantCulture)
    $longitudeText = $longitude.ToString('0.000000', [System.Globalization.CultureInfo]::InvariantCulture)

    [void]$sqlBuilder.AppendLine("INSERT INTO `ArgentineLocalities` (`Locality`, `Province`, `Latitude`, `Longitude`, `SortOrder`, `IsActive`) VALUES ('$safeLocality', '$safeProvince', $latitudeText, $longitudeText, $sortOrder, 1);")

    $sortOrder++
    $insertCount++
}

[System.IO.File]::WriteAllText($OutputSqlFile, $sqlBuilder.ToString(), [System.Text.UTF8Encoding]::new($false))

$unresolved |
    Sort-Object Province, Locality |
    Export-Csv -Path $OutputUnresolvedFile -NoTypeInformation -Encoding UTF8

[pscustomobject]@{
    OutputSqlFile = $OutputSqlFile
    OutputUnresolvedFile = $OutputUnresolvedFile
    TotalSourceLocalities = $localityMatches.Count
    InsertCount = $insertCount
    ResolvedFromCsv = $resolvedFromCsv
    ResolvedFromApi = $resolvedFromApi
    UnresolvedCount = $unresolved.Count
    SkippedExisting = $skippedExisting
    SkippedDuplicate = $skippedDuplicate
} | ConvertTo-Json -Compress
