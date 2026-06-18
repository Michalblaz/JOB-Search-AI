$ErrorActionPreference = "Stop"

$root = "C:\Users\micha\source\repos\MauiApp1"
$outputPath = Join-Path $root "Dokumentacja_Job_Search_AI.docx"
$workDir = Join-Path $root "docx_build"
$mediaDir = Join-Path $workDir "word\media"
$loginImagePath = Join-Path $root "MauiApp1.Web\docs\screen_login.png"

if (!(Test-Path $loginImagePath)) {
    throw "Brakuje screenshotu logowania: $loginImagePath"
}

if (Test-Path $workDir) {
    Remove-Item $workDir -Recurse -Force
}

New-Item -ItemType Directory -Path $workDir | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workDir "_rels") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workDir "docProps") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workDir "word") | Out-Null
New-Item -ItemType Directory -Path (Join-Path $workDir "word\_rels") | Out-Null
New-Item -ItemType Directory -Path $mediaDir | Out-Null

Copy-Item $loginImagePath (Join-Path $mediaDir "screen_login.png") -Force

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.IO.Compression.FileSystem

function New-PlaceholderImage {
    param(
        [string]$Path,
        [string]$Title,
        [string]$Subtitle
    )

    $bitmap = New-Object System.Drawing.Bitmap 1280, 760
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    $graphics.Clear([System.Drawing.Color]::FromArgb(250,250,250))
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(90,90,90), 4)
    $graphics.DrawRectangle($pen, 10, 10, 1260, 740)

    $titleFont = New-Object System.Drawing.Font("Arial", 28, [System.Drawing.FontStyle]::Bold)
    $subtitleFont = New-Object System.Drawing.Font("Arial", 18, [System.Drawing.FontStyle]::Regular)
    $brushDark = [System.Drawing.Brushes]::Black
    $brushGray = [System.Drawing.Brushes]::DimGray

    $graphics.DrawString($Title, $titleFont, $brushDark, 70, 220)
    $graphics.DrawString($Subtitle, $subtitleFont, $brushGray, 70, 290)
    $graphics.DrawString("Tutaj mo&#380;na wklei&#263; w&#322;asny zrzut ekranu z dzia&#322;ania aplikacji.", $subtitleFont, $brushGray, 70, 340)

    $bitmap.Save($Path, [System.Drawing.Imaging.ImageFormat]::Png)
    $graphics.Dispose()
    $bitmap.Dispose()
}

New-PlaceholderImage -Path (Join-Path $mediaDir "screen_search.png") -Title "Miejsce na screenshot - wyszukiwanie" -Subtitle "Przyk&#322;adowe wyszukiwanie: IT, student, bez do&#347;wiadczenia"
New-PlaceholderImage -Path (Join-Path $mediaDir "screen_profile.png") -Title "Miejsce na screenshot - profil" -Subtitle "Dane testowego u&#380;ytkownika Micha&#322;"
New-PlaceholderImage -Path (Join-Path $mediaDir "screen_settings.png") -Title "Miejsce na screenshot - ustawienia" -Subtitle "Widok ustawie&#324; aplikacji"
New-PlaceholderImage -Path (Join-Path $mediaDir "screen_favorites.png") -Title "Miejsce na screenshot - ulubione" -Subtitle "Lista zapisanych ofert pracy"

function Get-ImageSizeEmu {
    param(
        [string]$Path,
        [int64]$MaxWidthEmu = 5486400
    )

    $img = [System.Drawing.Image]::FromFile($Path)
    $widthPx = $img.Width
    $heightPx = $img.Height
    $img.Dispose()

    $widthEmu = $MaxWidthEmu
    $heightEmu = [int64]([double]$heightPx / [double]$widthPx * [double]$widthEmu)
    return @{ Width = $widthEmu; Height = $heightEmu }
}

$images = @(
    @{ File = "screen_login.png"; Name = "screen_login"; Caption = "Rysunek 1. Ekran logowania aplikacji webowej."; RelId = "rId1" },
    @{ File = "screen_search.png"; Name = "screen_search"; Caption = "Rysunek 2. Miejsce na zrzut ekranu z przyk&#322;adowego wyszukiwania."; RelId = "rId2" },
    @{ File = "screen_profile.png"; Name = "screen_profile"; Caption = "Rysunek 3. Miejsce na zrzut ekranu z widoku profilu."; RelId = "rId3" },
    @{ File = "screen_settings.png"; Name = "screen_settings"; Caption = "Rysunek 4. Miejsce na zrzut ekranu z ustawie&#324;."; RelId = "rId4" },
    @{ File = "screen_favorites.png"; Name = "screen_favorites"; Caption = "Rysunek 5. Miejsce na zrzut ekranu z listy ulubionych ofert."; RelId = "rId5" }
)

$contentTypes = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml" ContentType="application/xml"/>
  <Default Extension="png" ContentType="image/png"/>
  <Override PartName="/word/document.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/docProps/core.xml" ContentType="application/vnd.openxmlformats-package.core-properties+xml"/>
  <Override PartName="/docProps/app.xml" ContentType="application/vnd.openxmlformats-officedocument.extended-properties+xml"/>
</Types>
'@

$rels = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="word/document.xml"/>
  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties" Target="docProps/core.xml"/>
  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/extended-properties" Target="docProps/app.xml"/>
</Relationships>
'@

$core = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<cp:coreProperties xmlns:cp="http://schemas.openxmlformats.org/package/2006/metadata/core-properties" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:dcterms="http://purl.org/dc/terms/" xmlns:dcmitype="http://purl.org/dc/dcmitype/" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <dc:title>Dokumentacja projektu Job Search AI</dc:title>
  <dc:subject>Dokumentacja studencka</dc:subject>
  <dc:creator>OpenAI Codex</dc:creator>
  <cp:keywords>Job Search AI, dokumentacja, projekt</cp:keywords>
  <dc:description>Rozbudowana dokumentacja projektu Job Search AI.</dc:description>
  <cp:lastModifiedBy>OpenAI Codex</cp:lastModifiedBy>
  <dcterms:created xsi:type="dcterms:W3CDTF">2026-04-22T00:00:00Z</dcterms:created>
  <dcterms:modified xsi:type="dcterms:W3CDTF">2026-04-22T00:00:00Z</dcterms:modified>
</cp:coreProperties>
'@

$app = @'
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Properties xmlns="http://schemas.openxmlformats.org/officeDocument/2006/extended-properties" xmlns:vt="http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes">
  <Application>Microsoft Office Word</Application>
</Properties>
'@

$docRelsLines = @(
    '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>'
    '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">'
)
foreach ($img in $images) {
    $docRelsLines += "  <Relationship Id=""$($img.RelId)"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"" Target=""media/$($img.File)""/>"
}
$docRelsLines += '</Relationships>'
$docRels = $docRelsLines -join "`n"

function New-TextParagraph {
    param(
        [string]$TextXml,
        [switch]$Bold,
        [switch]$Title,
        [switch]$Centered
    )

    $alignXml = ""
    if ($Centered) {
        $alignXml = "<w:pPr><w:jc w:val='center'/></w:pPr>"
    }

    if ($Title) {
        return "<w:p>$alignXml<w:r><w:rPr><w:b/><w:sz w:val='32'/></w:rPr><w:t xml:space='preserve'>$TextXml</w:t></w:r></w:p>"
    }

    if ($Bold) {
        return "<w:p>$alignXml<w:r><w:rPr><w:b/></w:rPr><w:t xml:space='preserve'>$TextXml</w:t></w:r></w:p>"
    }

    return "<w:p>$alignXml<w:r><w:t xml:space='preserve'>$TextXml</w:t></w:r></w:p>"
}

function New-PageBreakParagraph {
    return "<w:p><w:r><w:br w:type='page'/></w:r></w:p>"
}

function New-ImageParagraph {
    param(
        [string]$RelationshipId,
        [string]$FileName,
        [string]$FriendlyName
    )

    $size = Get-ImageSizeEmu -Path (Join-Path $mediaDir $FileName)
    return @"
<w:p>
  <w:r>
    <w:drawing>
      <wp:inline distT="0" distB="0" distL="0" distR="0" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing">
        <wp:extent cx="$($size.Width)" cy="$($size.Height)"/>
        <wp:docPr id="1" name="$FriendlyName"/>
        <a:graphic xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main">
          <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
            <pic:pic xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture">
              <pic:nvPicPr>
                <pic:cNvPr id="0" name="$FileName"/>
                <pic:cNvPicPr/>
              </pic:nvPicPr>
              <pic:blipFill>
                <a:blip r:embed="$RelationshipId" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"/>
                <a:stretch><a:fillRect/></a:stretch>
              </pic:blipFill>
              <pic:spPr>
                <a:xfrm>
                  <a:off x="0" y="0"/>
                  <a:ext cx="$($size.Width)" cy="$($size.Height)"/>
                </a:xfrm>
                <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
              </pic:spPr>
            </pic:pic>
          </a:graphicData>
        </a:graphic>
      </wp:inline>
    </w:drawing>
  </w:r>
</w:p>
"@
}

$paragraphs = @()

# Strona tytułowa
$paragraphs += New-TextParagraph -TextXml "Dokumentacja projektu Job Search AI" -Title -Centered
$paragraphs += New-TextParagraph -TextXml "Wersja rozszerzona" -Bold -Centered
$paragraphs += New-TextParagraph -TextXml "Autor dokumentacji: OpenAI Codex" -Centered
$paragraphs += New-TextParagraph -TextXml "Data opracowania: 22.04.2026" -Centered
$paragraphs += New-TextParagraph -TextXml "Typ dokumentu: dokumentacja studencka projektu aplikacji webowej do wyszukiwania ofert pracy." -Centered
$paragraphs += New-PageBreakParagraph

# Wstep
$paragraphs += New-TextParagraph -TextXml "1. Wst&#281;p" -Bold
$paragraphs += New-TextParagraph -TextXml "Projekt Job Search AI zosta&#322; przygotowany jako aplikacja wspomagaj&#261;ca wyszukiwanie ofert pracy z wielu &#378;r&#243;de&#322; zewn&#281;trznych. G&#322;&#243;wnym celem systemu jest zebranie ofert pracy w jednym miejscu, zapisanie ich w lokalnej bazie danych oraz udost&#281;pnienie wygodnego interfejsu do filtrowania, przegl&#261;dania i oceniania dopasowania ofert do preferencji konkretnego u&#380;ytkownika."
$paragraphs += New-TextParagraph -TextXml "Aplikacja zosta&#322;a przygotowana w technologii .NET z warstw&#261; webow&#261; opart&#261; o Blazor. W aktualnej wersji dane ofert s&#261; pobierane przez osobny importer, a nast&#281;pnie zapisywane do lokalnej bazy PostgreSQL. Oznacza to, &#380;e aplikacja u&#380;ytkownika nie obci&#261;&#380;a stale zewn&#281;trznych API i korzysta g&#322;&#243;wnie z danych zapisanych lokalnie."
$paragraphs += New-TextParagraph -TextXml "Dokumentacja ma charakter praktyczny i opisuje najwa&#380;niejsze elementy projektu, jego funkcje, przep&#322;yw danych, struktur&#281; bazy danych oraz przyk&#322;adowy scenariusz u&#380;ycia. Dokument zosta&#322; przygotowany w wersji rozszerzonej, ale nadal w mo&#380;liwie prosty i zrozumia&#322;y spos&#243;b."

$paragraphs += New-TextParagraph -TextXml "2. Cel i zakres projektu" -Bold
$paragraphs += New-TextParagraph -TextXml "Najwa&#380;niejszym celem projektu jest stworzenie systemu, kt&#243;ry pomaga u&#380;ytkownikowi szybciej wyszuka&#263; interesuj&#261;ce oferty pracy. Zamiast odwiedza&#263; wiele portali osobno, u&#380;ytkownik korzysta z jednego interfejsu, w kt&#243;rym mo&#380;e ustawi&#263; w&#322;asne preferencje i filtrowa&#263; og&#322;oszenia wed&#322;ug wybranych kryteri&#243;w."
$paragraphs += New-TextParagraph -TextXml "Zakres projektu obejmuje: import ofert pracy do lokalnej bazy, wy&#347;wietlanie ofert w aplikacji webowej, logowanie i rejestracj&#281; u&#380;ytkownik&#243;w, zarz&#261;dzanie profilem, zapisywanie ulubionych ofert, histori&#281; przegl&#261;dania oraz lokalne dopasowanie ofert do profilu i filtr&#243;w."
$paragraphs += New-TextParagraph -TextXml "W projekcie uwzgl&#281;dniono tak&#380;e mo&#380;liwo&#347;&#263; dalszej rozbudowy, na przyk&#322;ad o pe&#322;ne dopasowanie AI, bardziej rozbudowane raporty lub dodatkowe &#378;r&#243;d&#322;a danych. W aktualnym stanie system jest ju&#380; funkcjonalny i mo&#380;e by&#263; wykorzystywany jako prototyp albo baza do dalszego rozwoju."

$paragraphs += New-TextParagraph -TextXml "3. Scenariusz przyk&#322;adowego u&#380;ytkownika" -Bold
$paragraphs += New-TextParagraph -TextXml "W celu lepszego pokazania sposobu dzia&#322;ania systemu przyj&#281;to prosty scenariusz testowy. U&#380;ytkownikiem jest Micha&#322;, czyli student szukaj&#261;cy pierwszej pracy lub sta&#380;u w bran&#380;y IT. Micha&#322; nie posiada jeszcze do&#347;wiadczenia zawodowego, ale zna j&#281;zyk angielski i chce wyszukiwa&#263; oferty zwi&#261;zane z informatyk&#261;, programowaniem, administracj&#261; system&#243;w lub wsparciem technicznym."
$paragraphs += New-TextParagraph -TextXml "W takim scenariuszu Micha&#322; po zalogowaniu uzupe&#322;nia w swoim profilu podstawowe preferencje, takie jak stanowisko docelowe, oczekiwania finansowe, poziom wykszta&#322;cenia, poziom do&#347;wiadczenia oraz j&#281;zyki. Nast&#281;pnie przechodzi do wyszukiwarki, gdzie wpisuje has&#322;o IT i ustawia lokalizacj&#281;, na przyk&#322;ad Rzesz&#243;w lub prac&#281; zdaln&#261;."
$paragraphs += New-TextParagraph -TextXml "System powinien w takim przypadku pokaza&#263; g&#322;&#243;wnie oferty z tytu&#322;ami typu Informatyk, Programista, Developer, Helpdesk, Administrator lub podobne. Je&#380;eli u&#380;ytkownik znajdzie interesuj&#261;ce oferty, mo&#380;e doda&#263; je do ulubionych, otworzy&#263; szczeg&#243;&#322;y oferty oraz zachowa&#263; histori&#281; przegl&#261;dania."

$paragraphs += New-PageBreakParagraph

$paragraphs += New-TextParagraph -TextXml "4. Technologie wykorzystane w projekcie" -Bold
$paragraphs += New-TextParagraph -TextXml "Projekt zosta&#322; wykonany przede wszystkim w ekosystemie .NET. Warstwa interfejsu dzia&#322;a jako aplikacja webowa oparta o Blazor, co umo&#380;liwia uruchamianie systemu lokalnie w przegl&#261;darce internetowej. Do obs&#322;ugi danych u&#380;ytkownika i ofert pracy zastosowano PostgreSQL."
$paragraphs += New-TextParagraph -TextXml "Dane o ofertach pracy s&#261; pobierane przez osobny importer. Importer korzysta z API takich serwis&#243;w jak Adzuna, Jooble, Remotive i Arbeitnow. Nast&#281;pnie normalizuje otrzymane dane do wsp&#243;lnego modelu i zapisuje je do lokalnej bazy danych."
$paragraphs += New-TextParagraph -TextXml "Do po&#322;&#261;czenia z PostgreSQL wykorzystano bibliotek&#281; Npgsql. Dodatkowo w projekcie wyst&#281;puje warstwa konfiguracji oparta o pliki appsettings.json. Pozwala to stosunkowo wygodnie zmienia&#263; connection string, ustawienia importera oraz konfiguracj&#281; modelu Gemini."

$paragraphs += New-TextParagraph -TextXml "5. Og&#243;lna architektura systemu" -Bold
$paragraphs += New-TextParagraph -TextXml "Architektura projektu jest podzielona na dwie g&#322;&#243;wne cz&#281;&#347;ci. Pierwsza z nich to importer, kt&#243;ry odpowiada za pobranie ofert z zewn&#281;trznych API i zapis do bazy danych. Druga cz&#281;&#347;&#263; to aplikacja webowa, kt&#243;ra odczytuje dane z lokalnej bazy PostgreSQL i prezentuje je u&#380;ytkownikowi."
$paragraphs += New-TextParagraph -TextXml "Takie rozdzielenie ma du&#380;&#261; zalet&#281;: aplikacja webowa nie musi przy ka&#380;dym wyszukaniu pyta&#263; zewn&#281;trznych serwis&#243;w o dane. Zamiast tego pracuje na bazie lokalnej, co poprawia szybko&#347;&#263;, stabilno&#347;&#263; oraz zmniejsza ryzyko problem&#243;w zwi&#261;zanych z limitami API."
$paragraphs += New-TextParagraph -TextXml "Warstwa logowania i profilu u&#380;ytkownika tak&#380;e zosta&#322;a stopniowo przenoszona z pliku lokalnego do bazy danych, co tworzy fundament pod dalsze rozszerzenie aplikacji o pe&#322;ne zarz&#261;dzanie kontami."

$paragraphs += New-PageBreakParagraph

$paragraphs += New-TextParagraph -TextXml "6. Najwa&#380;niejsze funkcje aplikacji" -Bold
$paragraphs += New-TextParagraph -TextXml "6.1. Logowanie i rejestracja" -Bold
$paragraphs += New-TextParagraph -TextXml "Po wej&#347;ciu do aplikacji u&#380;ytkownik widzi ekran logowania. Mo&#380;e zalogowa&#263; si&#281; na istniej&#261;ce konto albo za&#322;o&#380;y&#263; nowe konto. Dane konta s&#261; zapisywane w bazie PostgreSQL, a nie tylko w lokalnym pliku."
$paragraphs += New-ImageParagraph -RelationshipId "rId1" -FileName "screen_login.png" -FriendlyName "Ekran logowania"
$paragraphs += New-TextParagraph -TextXml $images[0].Caption

$paragraphs += New-TextParagraph -TextXml "6.2. Wyszukiwanie ofert pracy" -Bold
$paragraphs += New-TextParagraph -TextXml "W g&#322;&#243;wnej wyszukiwarce u&#380;ytkownik mo&#380;e wpisa&#263; stanowisko lub s&#322;owa kluczowe. Nast&#281;pnie mo&#380;e ograniczy&#263; wyniki po lokalizacji, rodzaju pracy, wymaganym do&#347;wiadczeniu, wykszta&#322;ceniu i j&#281;zykach. Dla przyk&#322;adowego u&#380;ytkownika Micha&#322;a jednym z g&#322;&#243;wnych przypadk&#243;w jest wyszukiwanie pracy w IT dla studenta bez do&#347;wiadczenia."
$paragraphs += New-TextParagraph -TextXml "Wyszukiwanie zosta&#322;o poprawione tak, aby najpierw analizowa&#263; tytu&#322; oferty. Oznacza to, &#380;e przy ha&#347;le IT aplikacja w pierwszej kolejno&#347;ci szuka tytu&#322;&#243;w takich jak Informatyk, Programista, Developer, Helpdesk lub Administrator. Dopiero gdy nie ma pasuj&#261;cych tytu&#322;&#243;w, system mo&#380;e si&#281;gn&#261;&#263; po opis oferty."
$paragraphs += New-ImageParagraph -RelationshipId "rId2" -FileName "screen_search.png" -FriendlyName "Widok wyszukiwania"
$paragraphs += New-TextParagraph -TextXml $images[1].Caption

$paragraphs += New-TextParagraph -TextXml "6.3. Profil u&#380;ytkownika" -Bold
$paragraphs += New-TextParagraph -TextXml "W profilu u&#380;ytkownik mo&#380;e poda&#263; domy&#347;ln&#261; lokalizacj&#281;, stanowisko, oczekiwane wynagrodzenie, wykszta&#322;cenie, do&#347;wiadczenie, preferowane rodzaje um&#243;w oraz j&#281;zyki. Na tej podstawie mo&#380;liwe jest szybsze ustawienie filtr&#243;w i budowanie prostego dopasowania ofert."
$paragraphs += New-TextParagraph -TextXml "Dla przyk&#322;adu student Micha&#322; mo&#380;e ustawi&#263;: stanowisko IT, do&#347;wiadczenie Brak do&#347;wiadczenia, wykszta&#322;cenie Student / w trakcie oraz j&#281;zyk Angielski. Taki profil mo&#380;e potem pom&#243;c podczas wyszukiwania odpowiednich ofert."
$paragraphs += New-ImageParagraph -RelationshipId "rId3" -FileName "screen_profile.png" -FriendlyName "Widok profilu"
$paragraphs += New-TextParagraph -TextXml $images[2].Caption

$paragraphs += New-TextParagraph -TextXml "6.4. Ustawienia aplikacji" -Bold
$paragraphs += New-TextParagraph -TextXml "W aplikacji znajduje si&#281; tak&#380;e sekcja ustawie&#324;, kt&#243;ra pozwala na zmian&#281; wybranych parametr&#243;w dzia&#322;ania oraz dostosowanie interfejsu. W zale&#380;no&#347;ci od dalszego rozwoju projektu sekcja ta mo&#380;e by&#263; rozbudowana o kolejne opcje konfiguracyjne."
$paragraphs += New-ImageParagraph -RelationshipId "rId4" -FileName "screen_settings.png" -FriendlyName "Widok ustawien"
$paragraphs += New-TextParagraph -TextXml $images[3].Caption

$paragraphs += New-TextParagraph -TextXml "6.5. Ulubione i historia" -Bold
$paragraphs += New-TextParagraph -TextXml "U&#380;ytkownik mo&#380;e dodawa&#263; wybrane oferty do listy ulubionych. Dodatkowo system zapisuje histori&#281; przegl&#261;danych ofert. Obie te funkcje s&#261; istotne z punktu widzenia u&#380;yteczno&#347;ci, poniewa&#380; pozwalaj&#261; szybko wr&#243;ci&#263; do wcze&#347;niej ogl&#261;danych propozycji zatrudnienia."
$paragraphs += New-ImageParagraph -RelationshipId "rId5" -FileName "screen_favorites.png" -FriendlyName "Widok ulubionych"
$paragraphs += New-TextParagraph -TextXml $images[4].Caption

$paragraphs += New-PageBreakParagraph

$paragraphs += New-TextParagraph -TextXml "7. Baza danych" -Bold
$paragraphs += New-TextParagraph -TextXml "W lokalnej bazie PostgreSQL przechowywane s&#261; przede wszystkim oferty pracy oraz dane u&#380;ytkownik&#243;w. Tabele zwi&#261;zane z ofertami obejmuj&#261; mi&#281;dzy innymi: job_sources, job_import_runs, job_offers, job_offer_languages oraz job_offer_tags. Tabele zwi&#261;zane z u&#380;ytkownikiem obejmuj&#261; app_users, user_profiles, user_profile_skills, user_profile_languages, user_profile_contract_types oraz tabele pomocnicze dla ulubionych i historii."
$paragraphs += New-TextParagraph -TextXml "Zalet&#261; lokalnej bazy jest brak ogranicze&#324; typowych dla darmowych plan&#243;w us&#322;ug zewn&#281;trznych, wi&#281;ksza kontrola nad danymi oraz mo&#380;liwo&#347;&#263; rozwoju projektu bez konieczno&#347;ci p&#322;acenia za kolejne limity przechowywania."
$paragraphs += New-TextParagraph -TextXml "Warto zaznaczy&#263;, &#380;e z bazy zosta&#322;o usuni&#281;te pole raw_payload_json, poniewa&#380; zajmowa&#322;o zbyt du&#380;o miejsca. W praktyce do wy&#347;wietlenia oferty w aplikacji wystarcza skr&#243;cony opis oraz link do szczeg&#243;&#322;&#243;w."

$paragraphs += New-TextParagraph -TextXml "8. Przep&#322;yw danych" -Bold
$paragraphs += New-TextParagraph -TextXml "Przep&#322;yw danych w projekcie mo&#380;na opisa&#263; w kilku krokach. Najpierw importer pobiera dane z zewn&#281;trznych API. Nast&#281;pnie dane s&#261; normalizowane i zapisywane do lokalnego PostgreSQL. P&#243;&#378;niej aplikacja webowa odczytuje aktywne oferty z tej bazy i prezentuje je u&#380;ytkownikowi. Na koniec u&#380;ytkownik mo&#380;e zapisa&#263; ustawienia profilu, ulubione oferty i histori&#281;, kt&#243;re r&#243;wnie&#380; trafiaj&#261; do PostgreSQL."
$paragraphs += New-TextParagraph -TextXml "Takie podej&#347;cie rozdziela warstw&#281; pobierania danych od warstwy prezentacji. Dzięki temu projekt jest czytelniejszy, prostszy w rozwoju i bardziej odporny na chwilowe problemy z zewn&#281;trznymi API."

$paragraphs += New-TextParagraph -TextXml "9. Wa&#380;niejsze miejsca w kodzie" -Bold
$paragraphs += New-TextParagraph -TextXml "Poni&#380;ej przedstawiono kilka najwa&#380;niejszych plik&#243;w projektu wraz z ich rol&#261;:"
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\testowe\\JobSearchService.cs - g&#322;&#243;wna logika wyszukiwania, filtrowania, dopasowania i pracy z profilem."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\testowe\\PostgresJobReader.cs - odczyt ofert z lokalnej bazy PostgreSQL."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\testowe\\PostgresUserStore.cs - zapis i odczyt u&#380;ytkownik&#243;w, ulubionych ofert, historii i danych profilu."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\Components\\Pages\\Home.razor - widok wynik&#243;w wyszukiwania wraz z filtrami bocznymi."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\Components\\Pages\\Login.razor - ekran logowania i rejestracji."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Web\\Components\\Pages\\Profile.razor - edycja profilu i preferencji u&#380;ytkownika."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Importer\\SourceClients.cs - pobieranie ofert z Adzuna, Jooble, Remotive i Arbeitnow."
$paragraphs += New-TextParagraph -TextXml "- MauiApp1.Importer\\PostgresJobRepository.cs - zapis znormalizowanych ofert do lokalnego PostgreSQL."

$paragraphs += New-PageBreakParagraph

$paragraphs += New-TextParagraph -TextXml "10. Przyk&#322;adowy przebieg u&#380;ycia systemu" -Bold
$paragraphs += New-TextParagraph -TextXml "Przyk&#322;adowy przebieg pracy z systemem mo&#380;e wygl&#261;da&#263; nast&#281;puj&#261;co:"
$paragraphs += New-TextParagraph -TextXml "1. U&#380;ytkownik Micha&#322; zak&#322;ada konto i loguje si&#281; do aplikacji."
$paragraphs += New-TextParagraph -TextXml "2. W profilu ustawia domy&#347;ln&#261; lokalizacj&#281;, stanowisko zwi&#261;zane z IT, wykszta&#322;cenie Student / w trakcie oraz j&#281;zyk Angielski."
$paragraphs += New-TextParagraph -TextXml "3. Po zapisaniu profilu przechodzi do wyszukiwarki i wpisuje has&#322;o IT."
$paragraphs += New-TextParagraph -TextXml "4. Aplikacja wy&#347;wietla oferty, kt&#243;re w pierwszej kolejno&#347;ci pasuj&#261; tytu&#322;em do bran&#380;y IT."
$paragraphs += New-TextParagraph -TextXml "5. Micha&#322; dodaje interesuj&#261;ce oferty do ulubionych i otwiera szczeg&#243;&#322;y wybranych og&#322;osze&#324;."
$paragraphs += New-TextParagraph -TextXml "6. Przy kolejnym logowaniu nadal ma dost&#281;p do swoich ulubionych ofert, historii oraz ustawie&#324; profilu."

$paragraphs += New-TextParagraph -TextXml "11. Zalety aktualnego rozwi&#261;zania" -Bold
$paragraphs += New-TextParagraph -TextXml "Do najwa&#380;niejszych zalet aktualnego rozwi&#261;zania nale&#380;&#261;: lokalna baza danych, szybsze dzia&#322;anie wyszukiwania, brak konieczno&#347;ci sta&#322;ego odpytwania zewn&#281;trznych API, mo&#380;liwo&#347;&#263; przechowywania danych u&#380;ytkownika oraz modularna architektura pozwalaj&#261;ca na stopniow&#261; rozbudow&#281; projektu."
$paragraphs += New-TextParagraph -TextXml "Istotne jest tak&#380;e to, &#380;e aplikacja posiada ju&#380; podstawy pod bardziej zaawansowane mechanizmy, takie jak dopasowanie ofert przez AI, rozwini&#281;te profile u&#380;ytkownik&#243;w czy dodatkowe raporty i rekomendacje."

$paragraphs += New-TextParagraph -TextXml "12. Wady i ograniczenia" -Bold
$paragraphs += New-TextParagraph -TextXml "Projekt nadal ma pewne ograniczenia. Nie wszystkie dane pochodz&#261;ce z API s&#261; idealnie ujednolicone, dlatego filtrowanie po do&#347;wiadczeniu czy wykszta&#322;ceniu czasami opiera si&#281; na heurystykach. Dodatkowo cz&#281;&#347;&#263; screenshot&#243;w w dokumentacji zosta&#322;a przygotowana jako placeholdery, aby mo&#380;na by&#322;o p&#243;&#378;niej podmieni&#263; je na finalne zrzuty."
$paragraphs += New-TextParagraph -TextXml "W przysz&#322;o&#347;ci warto r&#243;wnie&#380; dopracowa&#263; pe&#322;ne logowanie do bazy z bezpieczniejszym zarz&#261;dzaniem has&#322;ami, bardziej rozbudowanym systemem r&#243;l i lepszym zarz&#261;dzaniem sesj&#261;."

$paragraphs += New-TextParagraph -TextXml "13. Kierunki dalszego rozwoju" -Bold
$paragraphs += New-TextParagraph -TextXml "W dalszej kolejno&#347;ci projekt mo&#380;e zosta&#263; rozbudowany o: bardziej inteligentne dopasowanie AI, eksport wybranych ofert do PDF, powiadomienia o nowych ofertach, rozbudowany panel administracyjny, dodatkowe statystyki dotycz&#261;ce rynku pracy oraz integracj&#281; z kolejnymi API."
$paragraphs += New-TextParagraph -TextXml "Mo&#380;liwe jest tak&#380;e przygotowanie pe&#322;nej wersji wdro&#380;eniowej hostowanej jako us&#322;uga sieciowa, a nie tylko lokalnie. Dzięki obecnemu podzia&#322;owi na importer, baz&#281; danych i aplikacj&#281; webow&#261; projekt ma ju&#380; dobr&#261; podstaw&#281; do takiego kierunku rozwoju."

$paragraphs += New-TextParagraph -TextXml "14. Podsumowanie" -Bold
$paragraphs += New-TextParagraph -TextXml "Job Search AI jest projektem, kt&#243;ry realizuje praktyczny problem wyszukiwania ofert pracy z wielu &#378;r&#243;de&#322; w jednym miejscu. Aktualna wersja systemu umo&#380;liwia import danych do lokalnej bazy PostgreSQL, filtrowanie i przegl&#261;danie ofert, zapis ulubionych i historii oraz budowanie profilu u&#380;ytkownika."
$paragraphs += New-TextParagraph -TextXml "Z punktu widzenia projektu studenckiego jest to rozwi&#261;zanie kompletne na poziomie funkcjonalnego prototypu. Projekt posiada realn&#261; warto&#347;&#263; praktyczn&#261;, pokazuje integracj&#281; wielu technologii oraz pozostawia dobre mo&#380;liwo&#347;ci do dalszej rozbudowy."

$documentXml = @"
<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"
 xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
 xmlns:o="urn:schemas-microsoft-com:office:office"
 xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"
 xmlns:m="http://schemas.openxmlformats.org/officeDocument/2006/math"
 xmlns:v="urn:schemas-microsoft-com:vml"
 xmlns:wp14="http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing"
 xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
 xmlns:w10="urn:schemas-microsoft-com:office:word"
 xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
 xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml"
 xmlns:wpg="http://schemas.microsoft.com/office/word/2010/wordprocessingGroup"
 xmlns:wpi="http://schemas.microsoft.com/office/word/2010/wordprocessingInk"
 xmlns:wne="http://schemas.microsoft.com/office/word/2006/wordml"
 xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
 mc:Ignorable="w14 wp14">
  <w:body>
    $($paragraphs -join "`n")
    <w:sectPr>
      <w:pgSz w:w="11906" w:h="16838"/>
      <w:pgMar w:top="1440" w:right="1440" w:bottom="1440" w:left="1440" w:header="708" w:footer="708" w:gutter="0"/>
    </w:sectPr>
  </w:body>
</w:document>
"@

$contentTypes | Out-File -LiteralPath (Join-Path $workDir "[Content_Types].xml") -Encoding utf8
$rels | Out-File -LiteralPath (Join-Path $workDir "_rels\.rels") -Encoding utf8
$core | Out-File -LiteralPath (Join-Path $workDir "docProps\core.xml") -Encoding utf8
$app | Out-File -LiteralPath (Join-Path $workDir "docProps\app.xml") -Encoding utf8
$documentXml | Out-File -LiteralPath (Join-Path $workDir "word\document.xml") -Encoding utf8
$docRels | Out-File -LiteralPath (Join-Path $workDir "word\_rels\document.xml.rels") -Encoding utf8

if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

$zipPath = [System.IO.Path]::ChangeExtension($outputPath, ".zip")
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}

[System.IO.Compression.ZipFile]::CreateFromDirectory($workDir, $zipPath)
Move-Item $zipPath $outputPath -Force

Write-Output $outputPath
