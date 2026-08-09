function Get-AuthenticodeContentSha256 {
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )

    [byte[]]$image = [IO.File]::ReadAllBytes([IO.Path]::GetFullPath($Path))
    if ($image.Length -lt 256 -or $image[0] -ne [byte][char]'M' -or
        $image[1] -ne [byte][char]'Z') {
        throw "Signing target is not a valid PE image: $Path"
    }

    $peOffset = [BitConverter]::ToInt32($image, 0x3c)
    if ($peOffset -lt 0x40 -or $peOffset + 24 -gt $image.Length -or
        $image[$peOffset] -ne [byte][char]'P' -or
        $image[$peOffset + 1] -ne [byte][char]'E' -or
        $image[$peOffset + 2] -ne 0 -or $image[$peOffset + 3] -ne 0) {
        throw "Signing target has an invalid PE header: $Path"
    }

    $optionalHeaderOffset = $peOffset + 24
    $optionalHeaderMagic = [BitConverter]::ToUInt16($image, $optionalHeaderOffset)
    $dataDirectoryOffset = switch ($optionalHeaderMagic) {
        0x10b { $optionalHeaderOffset + 96 }
        0x20b { $optionalHeaderOffset + 112 }
        default { throw "Signing target has an unsupported PE optional header: $Path" }
    }
    $checksumOffset = $optionalHeaderOffset + 64
    $certificateDirectoryOffset = $dataDirectoryOffset + (4 * 8)
    if ($certificateDirectoryOffset + 8 -gt $image.Length) {
        throw "Signing target has a truncated PE optional header: $Path"
    }

    $certificateOffset = [BitConverter]::ToUInt32($image, $certificateDirectoryOffset)
    $certificateSize = [BitConverter]::ToUInt32($image, $certificateDirectoryOffset + 4)
    if (($certificateOffset -eq 0) -ne ($certificateSize -eq 0)) {
        throw "Signing target has an inconsistent PE certificate table: $Path"
    }

    $normalizedLength = $image.Length
    if ($certificateSize -ne 0) {
        $certificateEnd = [uint64]$certificateOffset + [uint64]$certificateSize
        if ($certificateOffset -lt ($certificateDirectoryOffset + 8) -or
            $certificateEnd -ne [uint64]$image.Length) {
            throw "Signing target certificate table is not a single terminal PE table: $Path"
        }
        $normalizedLength = [int]$certificateOffset
    }

    [byte[]]$normalizedImage = [byte[]]::new($normalizedLength)
    [Array]::Copy($image, $normalizedImage, $normalizedLength)
    [Array]::Clear($normalizedImage, $checksumOffset, 4)
    [Array]::Clear($normalizedImage, $certificateDirectoryOffset, 8)
    return [Convert]::ToHexString(
        [Security.Cryptography.SHA256]::HashData($normalizedImage)).ToLowerInvariant()
}

function Get-ReleasePayloadInventoryItems {
    param(
        [Parameter(Mandatory)]
        [string]$PayloadRoot,
        [Parameter(Mandatory)]
        [string[]]$DirectoryNames
    )

    $payloadPath = [IO.Path]::GetFullPath($PayloadRoot)
    $items = [Collections.Generic.List[object]]::new()
    foreach ($directoryName in $DirectoryNames) {
        $rootDirectoryPath = Join-Path $payloadPath $directoryName
        if (-not (Test-Path -LiteralPath $rootDirectoryPath -PathType Container)) {
            throw "Release payload directory is missing: $rootDirectoryPath"
        }

        $pendingDirectories = [Collections.Generic.Stack[IO.DirectoryInfo]]::new()
        $pendingDirectories.Push([IO.DirectoryInfo]::new($rootDirectoryPath))
        while ($pendingDirectories.Count -gt 0) {
            $directory = $pendingDirectories.Pop()
            if (($directory.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "Release payload contains a directory reparse point: $($directory.FullName)"
            }

            $items.Add([pscustomobject]@{
                Kind = 'Directory'
                RelativePath = [IO.Path]::GetRelativePath($payloadPath, $directory.FullName).Replace('\', '/')
                FullName = $directory.FullName
            })
            foreach ($child in Get-ChildItem -LiteralPath $directory.FullName -Force) {
                if (($child.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Release payload contains a reparse point: $($child.FullName)"
                }
                if ($child.PSIsContainer) {
                    $pendingDirectories.Push([IO.DirectoryInfo]$child)
                } else {
                    $items.Add([pscustomobject]@{
                        Kind = 'File'
                        RelativePath = [IO.Path]::GetRelativePath($payloadPath, $child.FullName).Replace('\', '/')
                        FullName = $child.FullName
                    })
                }
            }
        }
    }

    return $items
}
