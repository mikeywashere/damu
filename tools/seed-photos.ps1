<#
.SYNOPSIS
    Downloads ~20 free Creative Commons JPEG photos from Wikimedia Commons
    into a local dev sample folder for use with dam-you.

.DESCRIPTION
    Run this once per developer machine to get sample photos.
    Photos are downloaded from Wikimedia Commons under CC licenses.
    The target folder can be added as a watched folder in dam-you.

.PARAMETER TargetPath
    Destination folder. Defaults to C:\dev\dam-you-sample-photos\

.EXAMPLE
    .\tools\seed-photos.ps1
    .\tools\seed-photos.ps1 -TargetPath "D:\Photos\TestLibrary"
#>

param(
    [string]$TargetPath = "C:\dev\dam-you-sample-photos"
)

$ErrorActionPreference = "Stop"

# Creative Commons JPEG photos from Wikimedia Commons (public domain / CC0 / CC BY)
# These are small/medium resolution images suitable for dev/testing.
$photos = @(
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/PNG_transparency_demonstration_1.png/280px-PNG_transparency_demonstration_1.png"; Name = "sample_01.png" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a7/Camponotus_flavomarginatus_ant.jpg/320px-Camponotus_flavomarginatus_ant.jpg"; Name = "sample_02.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/40/Sunflower_sky_backdrop.jpg/320px-Sunflower_sky_backdrop.jpg"; Name = "sample_03.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6d/Good_Food_Display_-_NCI_Visuals_Online.jpg/320px-Good_Food_Display_-_NCI_Visuals_Online.jpg"; Name = "sample_04.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3f/Biking_in_the_rain.jpg/320px-Biking_in_the_rain.jpg"; Name = "sample_05.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/2/2f/Culinary_fruits_front_view.jpg/320px-Culinary_fruits_front_view.jpg"; Name = "sample_06.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a3/Eq_it-na_pizza-margherita_sep2005_sml.jpg/320px-Eq_it-na_pizza-margherita_sep2005_sml.jpg"; Name = "sample_07.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9c/Golden_Gate_Bridge_from_Battery_Spencer.jpg/320px-Golden_Gate_Bridge_from_Battery_Spencer.jpg"; Name = "sample_08.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/1/14/Gatto_europeo4.jpg/320px-Gatto_europeo4.jpg"; Name = "sample_09.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d9/Collage_of_Nine_Dogs.jpg/320px-Collage_of_Nine_Dogs.jpg"; Name = "sample_10.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/50/Female_pair_-_Flickr_-_Lip_Kee_%281%29.jpg/320px-Female_pair_-_Flickr_-_Lip_Kee_%281%29.jpg"; Name = "sample_11.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e7/Everest_North_Face_toward_Base_Camp_Tibet_Luca_Galuzzi_2006.jpg/320px-Everest_North_Face_toward_Base_Camp_Tibet_Luca_Galuzzi_2006.jpg"; Name = "sample_12.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/41/Sunflower_from_Silesia2.jpg/320px-Sunflower_from_Silesia2.jpg"; Name = "sample_13.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3d/Bluebell_flowers_close_up.jpg/320px-Bluebell_flowers_close_up.jpg"; Name = "sample_14.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/61/De_Vliet_bij_Zoetermeer.jpg/320px-De_Vliet_bij_Zoetermeer.jpg"; Name = "sample_15.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/b/b9/Above_Gotham.jpg/320px-Above_Gotham.jpg"; Name = "sample_16.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/6/6b/American_Beaver.jpg/320px-American_Beaver.jpg"; Name = "sample_17.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/1/1a/24701-nature-natural-beauty.jpg/320px-24701-nature-natural-beauty.jpg"; Name = "sample_18.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/2/21/Simple_flower.jpg/320px-Simple_flower.jpg"; Name = "sample_19.jpg" },
    @{ Url = "https://upload.wikimedia.org/wikipedia/commons/thumb/1/10/Empire_State_Building_%28aerial_view%29.jpg/320px-Empire_State_Building_%28aerial_view%29.jpg"; Name = "sample_20.jpg" }
)

Write-Host "📸 dam-you sample photo seeder" -ForegroundColor Cyan
Write-Host "Target: $TargetPath" -ForegroundColor Gray
Write-Host ""

if (-not (Test-Path $TargetPath)) {
    New-Item -ItemType Directory -Force -Path $TargetPath | Out-Null
    Write-Host "Created folder: $TargetPath" -ForegroundColor Green
}

$success = 0
$failed = 0

foreach ($photo in $photos) {
    $dest = Join-Path $TargetPath $photo.Name
    if (Test-Path $dest) {
        Write-Host "  ⏭  $($photo.Name) (already exists)" -ForegroundColor Gray
        $success++
        continue
    }

    try {
        Invoke-WebRequest -Uri $photo.Url -OutFile $dest -UseBasicParsing -TimeoutSec 15
        Write-Host "  ✅ $($photo.Name)" -ForegroundColor Green
        $success++
    }
    catch {
        Write-Host "  ❌ $($photo.Name): $($_.Exception.Message)" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "Done. $success downloaded, $failed failed." -ForegroundColor Cyan
Write-Host ""
Write-Host "Add this folder to dam-you on first run:" -ForegroundColor Yellow
Write-Host "  $TargetPath" -ForegroundColor White
