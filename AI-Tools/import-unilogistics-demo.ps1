param(
    [string]$ApiBaseUrl = "https://localhost:6969/",
    [string]$ZipPath = "F:\Unilogistics.Web (2).zip",
    [Parameter(Mandatory = $true)][string]$Email,
    [Parameter(Mandatory = $true)][string]$Password,
    [switch]$Publish,
    [switch]$ReplaceSections
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.IO.Compression.FileSystem

if (-not $ApiBaseUrl.EndsWith("/")) {
    $ApiBaseUrl += "/"
}

if (-not (Test-Path -LiteralPath $ZipPath)) {
    throw "Demo zip not found: $ZipPath"
}

[System.Net.ServicePointManager]::ServerCertificateValidationCallback = { $true }

function To-JsonBody($body) {
    return ($body | ConvertTo-Json -Depth 40 -Compress)
}

function Invoke-Json($method, $path, $body = $null, $token = $null) {
    $headers = @{}
    if ($token) {
        $headers.Authorization = "Bearer $token"
    }

    $uri = "$ApiBaseUrl$path"
    $params = @{
        Method = $method
        Uri = $uri
        Headers = $headers
        ContentType = "application/json"
    }

    if ($null -ne $body) {
        $params.Body = To-JsonBody $body
    }

    try {
        Invoke-RestMethod @params
    }
    catch {
        Write-Host "FAILED: $method $path"
        if ($null -ne $body) {
            Write-Host "REQUEST BODY:"
            Write-Host (To-JsonBody $body)
        }

        $response = $_.Exception.Response
        if ($response -and $response.GetResponseStream()) {
            $reader = New-Object System.IO.StreamReader($response.GetResponseStream())
            Write-Host "RESPONSE BODY:"
            Write-Host $reader.ReadToEnd()
        }

        throw
    }
}

function Lang($text) {
    @{ en = $text; vi = $text; cn = $text }
}

function SectionStyle($background = "#ffffff", $textColor = "dark", $padding = "medium", $image = $null) {
    $style = @{
        backgroundType = $(if ($image) { "image" } else { "color" })
        backgroundColor = $background
        textColor = $textColor
        padding = $padding
        contentWidth = "normal"
        height = "auto"
        overlayOpacity = 0
    }

    $style
}

function HeroSection($title, $sub, $image) {
    @{
        type = "hero"
        visible = $true
        style = SectionStyle "#001a33" "light" "large" $image
        layout = "centered"
        heading = Lang $title
        subheading = Lang $sub
        headingSize = "large"
        contentAlignment = "left"
        buttons = @(
            @{
                label = Lang "Get a Quote"
                action = "openModal"
                href = "#quote"
                style = "filled"
                visible = $true
                order = 0
            },
            @{
                label = Lang "Talk to an Expert"
                action = "openModal"
                href = "#modal"
                style = "outline"
                visible = $true
                order = 1
            }
        )
    }
}

function ListSection($title, $columns, $items, $dark = $false) {
    @{
        type = "list"
        visible = $true
        style = SectionStyle $(if ($dark) { "#001a33" } else { "#f5f5f5" }) $(if ($dark) { "light" } else { "dark" }) "medium"
        layout = "cards"
        columns = $columns
        sectionTitle = Lang $title
        showIcon = $false
        items = @($items | ForEach-Object -Begin { $i = 0 } -Process {
            @{
                title = Lang $_.Title
                description = Lang $_.Description
                linkHref = $_.Href
                visible = $true
                order = $i++
            }
        })
    }
}

function StatsSection() {
    @{
        type = "stats"
        visible = $true
        style = SectionStyle "#ffffff" "dark" "medium"
        sectionTitle = Lang "Logistics in Sync With Your Growth"
        columns = 4
        durationMs = 1200
        items = @(
            @{ label = Lang "Shipments coordinated"; value = 12000; suffix = "+"; visible = $true; order = 0 },
            @{ label = Lang "Countries connected"; value = 30; suffix = "+"; visible = $true; order = 1 },
            @{ label = Lang "Warehouse partners"; value = 80; suffix = "+"; visible = $true; order = 2 },
            @{ label = Lang "Years of expertise"; value = 20; suffix = "+"; visible = $true; order = 3 }
        )
    }
}

function CarouselSection() {
    @{
        type = "carousel"
        visible = $true
        style = SectionStyle "#f5f5f5" "dark" "medium"
        sectionTitle = Lang "Insights"
        columns = 3
        autoplay = $true
        showDots = $true
        showArrows = $true
        items = @(
            @{ title = Lang "Supply Chain Resilience"; description = Lang "Practical ways to build resilient international logistics."; linkHref = "/insights"; visible = $true; order = 0 },
            @{ title = Lang "Warehouse Optimization"; description = Lang "Operational improvements for modern warehouse networks."; linkHref = "/insights"; visible = $true; order = 1 },
            @{ title = Lang "Cross-border Trade"; description = Lang "How customs and freight planning reduce delivery risk."; linkHref = "/insights"; visible = $true; order = 2 }
        )
    }
}

function NetworkMapSection() {
    @{
        type = "network-map"
        visible = $true
        style = SectionStyle "#ffffff" "dark" "medium"
        sectionTitle = Lang "Global Network"
        centerLat = 18.0
        centerLng = 105.0
        defaultZoom = 3
        pins = @(
            @{ label = "Thailand"; lat = 13.7563; lng = 100.5018; href = "/contact-network"; visible = $true; order = 0 },
            @{ label = "Vietnam"; lat = 10.8231; lng = 106.6297; href = "/contact-network"; visible = $true; order = 1 },
            @{ label = "China"; lat = 31.2304; lng = 121.4737; href = "/contact-network"; visible = $true; order = 2 },
            @{ label = "Japan"; lat = 35.6762; lng = 139.6503; href = "/contact-network"; visible = $true; order = 3 },
            @{ label = "USA"; lat = 34.0522; lng = -118.2437; href = "/contact-network"; visible = $true; order = 4 }
        )
    }
}

function HtmlSection($html) {
    @{
        type = "html"
        visible = $true
        style = SectionStyle "#ffffff" "dark" "medium"
        content = Lang $html
    }
}

function Ensure-Page($slug, $title, $token) {
    $pages = (Invoke-Json GET "api/admin/pages" $null $token).data
    $existing = @($pages | Where-Object { $_.slug -eq $slug }) | Select-Object -First 1

    if ($existing) {
        Invoke-Json PUT "api/admin/pages/$($existing.id)" @{ name = Lang $title; slug = $slug; visible = $true } $token | Out-Null
        return $existing.id
    }

    $created = Invoke-Json POST "api/admin/pages" @{ name = Lang $title; slug = $slug; visible = $true } $token
    $pageId = $created.data.id
    Invoke-Json PUT "api/admin/pages/$pageId" @{ name = Lang $title; slug = $slug; visible = $true } $token | Out-Null
    $pageId
}

function Clear-Sections($pageId, $token) {
    $sections = (Invoke-Json GET "api/admin/pages/$pageId/sections" $null $token).data
    foreach ($section in $sections) {
        Invoke-Json DELETE "api/admin/pages/$pageId/sections/$($section.id)" $null $token | Out-Null
    }
}

function Add-Sections($pageId, $sections, $token) {
    foreach ($section in $sections) {
        Invoke-Json POST "api/admin/pages/$pageId/sections" $section $token | Out-Null
    }
}

$login = Invoke-Json POST "api/auth/login" @{ email = $Email; password = $Password }
if (-not $login.success -or -not $login.data.token) {
    throw "Login failed: $($login.message)"
}
$token = $login.data.token

Write-Host "Logged in as $Email"

Invoke-Json PUT "api/admin/global/theme" @{
    colorPrimary = "#001a33"
    colorAccent = "#e5c076"
    colorBackground = "#f5f5f5"
    colorText = "#001a33"
    fontBody = "Inter"
    fontHeading = "Inter"
    borderRadius = "8px"
    textSizeBase = "16px"
} $token | Out-Null

Invoke-Json PUT "api/admin/global/branding" @{
    companyName = "U&I Logistics"
    href = "/"
} $token | Out-Null

$buttonRes = Invoke-Json GET "api/admin/global/buttons" $null $token
foreach ($button in @($buttonRes.data)) {
    Invoke-Json DELETE "api/admin/global/buttons/$($button.id)" $null $token | Out-Null
}
Invoke-Json POST "api/admin/global/buttons" @{ labelText = Lang "Login to SyncHub"; action = "OpenModal"; href = "#modal"; position = "HeaderRight" } $token | Out-Null
Invoke-Json POST "api/admin/global/buttons" @{ labelText = Lang "Get a Quote"; action = "OpenModal"; href = "#quote"; position = "HeaderRight" } $token | Out-Null

$solutionItems = @(
    @{ Title = "Sea & Air Freight"; Description = "Reliable international shipping for all your needs."; Href = "/solutions" },
    @{ Title = "Customs Brokerage"; Description = "Simplify customs with expert brokerage services."; Href = "/solutions" },
    @{ Title = "Warehousing"; Description = "Flexible storage solutions for your inventory."; Href = "/solutions" },
    @{ Title = "Last Mile Delivery"; Description = "Fast and accurate delivery to your customers."; Href = "/solutions" }
)

$industryItems = @(
    @{ Title = "Wood & Furniture"; Description = "Streamlined export of wood and furniture."; Image = "wood_furniture.png"; Href = "/industry" },
    @{ Title = "Pharma Logistics"; Description = "Controlled and compliant pharma logistics."; Image = "Pharma_Logistics.png"; Href = "/industry" },
    @{ Title = "Retail & FMCG"; Description = "Responsive fulfillment for fast-moving goods."; Image = "Retail_FMCG.png"; Href = "/industry" },
    @{ Title = "Electronics"; Description = "Secure logistics for sensitive electronics."; Image = "Electronics.png"; Href = "/industry" }
)

$pages = @(
    @{
        Slug = "home"
        Title = "Home"
        Sections = @(
            (HeroSection "LOGISTICS. In Sync With Your Business." "Clients' interests first - sustainable and symbiotic logistics partner." "hero-bg1.jpg"),
            (ListSection "Engineered for Performance" 4 $solutionItems),
            (StatsSection),
            (ListSection "Logistics Tailored to Your Industry" 4 $industryItems $true),
            (NetworkMapSection),
            (CarouselSection)
        )
    },
    @{
        Slug = "solutions"
        Title = "Solutions"
        Sections = @(
            (HeroSection "Solutions" "End-to-end logistics services engineered for performance." "hero-bg2.jpg"),
            (ListSection "Our Logistics Solutions" 3 $solutionItems)
        )
    },
    @{
        Slug = "industry"
        Title = "Industry"
        Sections = @(
            (HeroSection "Industry Expertise" "Specialized logistics for demanding verticals." "warehouse.jpg"),
            (ListSection "Tailored by Industry" 4 $industryItems $true)
        )
    },
    @{
        Slug = "technology"
        Title = "Technology"
        Sections = @(
            (HeroSection "Technology" "Digital tools that make supply chains visible and manageable." "symbiosis-bg.jpg"),
            (HtmlSection "<h2>SyncHub visibility</h2><p>Operational visibility, reporting, and collaboration tools for modern logistics teams.</p>")
        )
    },
    @{
        Slug = "sustainability"
        Title = "Sustainability"
        Sections = @(
            (HeroSection "Sustainability" "Responsible logistics designed for long-term growth." "symbiosis-bg.jpg"),
            (StatsSection)
        )
    },
    @{
        Slug = "insights"
        Title = "Insights"
        Sections = @(
            (HeroSection "Insights" "Ideas and resources for resilient logistics operations." "case_study_hero.jpg"),
            (CarouselSection)
        )
    },
    @{
        Slug = "contact-network"
        Title = "Contact Network"
        Sections = @(
            (HeroSection "Contact Network" "Connect with our regional logistics network." "hero-bg-contact.jpg"),
            (NetworkMapSection)
        )
    },
    @{
        Slug = "about"
        Title = "About Us"
        Sections = @(
            (HeroSection "About U&I Logistics" "A sustainable and symbiotic logistics partner for growing businesses." "warehouse.jpg"),
            (HtmlSection "<h2>Clients' interests first</h2><p>We combine operational expertise, international network coverage, and technology to keep goods moving.</p>")
        )
    }
)

foreach ($page in $pages) {
    Write-Host "Importing page: $($page.Slug)"
    $pageId = Ensure-Page $page.Slug $page.Title $token
    if ($ReplaceSections) {
        Clear-Sections $pageId $token
    }
    Add-Sections $pageId $page.Sections $token
    if ($Publish) {
        Invoke-Json POST "api/admin/pages/$pageId/publish" $null $token | Out-Null
    }
}

Write-Host "Done."
if (-not $Publish) {
    Write-Host "Draft pages created. Run again with -Publish to publish them."
}
