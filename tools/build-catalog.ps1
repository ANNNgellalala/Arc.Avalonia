param(
    [string]$Parsed = '.recon\catalog-parsed.json',
    [string]$Out = 'docs\arc-catalog.json'
)

$ErrorActionPreference = 'Stop'
$items = Get-Content $Parsed -Raw | ConvertFrom-Json
$bySlug = @{}
foreach ($item in $items) { $slug = $item.slug -replace '^blocks/',''; $bySlug[$slug] = $item }

# Sidebar taxonomy from https://uiarc.dev/docs/introduction (23 categories, 128 components).
$categories = [ordered]@{
    'Buttons'          = @('button','action-button','split-button','button-group','floating-button-group','expanding-button-group','copy-button','action-swap','icon-morph','confirm-morph','theme-switch')
    'Gestures'         = @('hold-to-confirm','swipe-actions','reactions')
    'Menus'            = @('dropdown-menu','context-menu','user-menu')
    'Text fields'      = @('input','textarea','password-field','password-strength','search-field','expanding-search','inline-edit')
    'Special inputs'   = @('number-field','money-input','phone-input','scrub-input','tag-input','mention-input','shortcut-recorder')
    'Selects'          = @('select','combobox','multi-select','morph-select','chip-group')
    'Toggles'          = @('radio-cards','billing-toggle','checkbox','radio-group','switch','segmented-control','rating')
    'Sliders'          = @('slider','elastic-slider','range-histogram-slider')
    'Pickers'          = @('calendar','date-picker','date-range-picker','time-picker','color-picker','emoji-picker')
    'Editors'          = @('rich-text-editor','signature-pad','file-dropzone','avatar-upload')
    'Navigation'       = @('tabs','breadcrumb','pagination','mega-menu-morph')
    'Expand'           = @('scroll-area','accordion','expandable-card','resizable-panels','expandable-text')
    'Overlays'         = @('dialog','drawer','bottom-sheet','popover','hover-card','tooltip','share-access')
    'Messages'         = @('alert','toast','toast-stack','announcement-bar','cookie-consent')
    'Progress'         = @('progress','skeleton','stepper','countdown','usage-meter','onboarding-checklist')
    'Avatars'          = @('avatar','avatar-group','badge','presence-stack')
    'Cards'            = @('card','metric-card','empty-state')
    'Charts'           = @('line-chart','bar-chart','donut-chart','streamgraph','brush-chart','waffle-chart','slope-chart','sparkline','gauge','activity-heatmap','animated-counter','ridgeline','treemap','split-bar')
    'Advanced charts'  = @('sunburst','sankey-flow','funnel-chart','radar-chart','realtime-stream','race-bar-chart','data-grid')
    'Tables'           = @('sortable-data-table','tree-table','tree-view','reorderable-list','filter-toolbar','code-block','json-viewer')
    'Activity'         = @('timeline','comment-thread','chat-thread')
    'Media'            = @('image-compare','carousel','card-stack')
    'Text effects'     = @('text-reveal','in-view-title','text-morph','text-shimmer')
}

# Special components, grouped for implementation and navigation.
$specialGroups = [ordered]@{
    'Glass and surfaces' = @('glass-menu','glass-segmented','glass-card','glass-tabbar','holo-card','wallet-stack','sheet-stack','control-center','card-to-article','link-unfurl','share-sheet')
    'Motion and interaction' = @('action-morph','morph-nav','dock','liquid-tab-bar','orbit-menu','orbit-logos','booking-pill','keycap-keyboard','slot-text','sand-text','typewriter-terminal')
    'Media and input' = @('product-gallery','lightbox-gallery','photo-grid','cover-flow','voice-orb','now-playing','voice-recorder','page-curl','sticker-peel','dot-grid','gravity-well')
    'AI widgets' = @('agent-status','tool-call','agent-plan','agent-timeline','context-window','ink-fluid','activity-rings','stretch-refresh','morph-loader','skeleton-morph','black-hole')
    'AI explainers' = @('data-flow','payment-flow','multi-region-failover','vector-search','agent-loop','mixture-of-experts','speculative-decoding','prompt-cache','reasoning-tree','time-dial','date-reel')
}

# Blocks, grouped for implementation and navigation.
$blockGroups = [ordered]@{
    'Marketing and site' = @('site-header','site-footer','hero-section','faq-section','contact-section','blog-grid','comparison-table','stats-band','cta-section','newsletter-signup','logo-marquee','page-header','hero-signup','spotlight-grid','scroll-story','feature-bento','plan-comparison','pricing-calculator')
    'Commerce' = @('product-listing','product-detail','cart-drawer','checkout-summary','usage-pricing','usage-billing','wallet-card','invoice-studio','delivery-tracker','cancel-flow','integrations','help-center','support-widget','support-conversation','changelog-feed','empty-states','billing-overview','checkout-live')
    'App shell and settings' = @('workspace-sidebar','sidebar-rail','settings-page','settings-command','docs-sidebar','inbox-sidebar','inbox-triage','command-palette','notification-center','selection-toolbar','file-upload','otp-input','multi-step-form','signup-form','sign-in','login-centered','login-immersive','login-split','security-settings','search-results','media-player')
    'Team and collaboration' = @('team-directory','team-showcase','testimonial-stage','team-members','invite-people','availability-picker','project-board','kanban-board','week-calendar','api-keys','webhooks','roles-permissions','env-variables','release-readiness','onboarding-illustration','cohort-retention')
    'Data and charts' = @('metrics-dashboard','revenue-explorer','journey-flow','usage-forecast','mrr-waterfall','metric-explorer','activity-terrain','revenue-globe','customer-galaxy','semantic-zoom','layout-morph','budget-variance','choropleth-explorer','small-multiples','connected-scatter','horizon-chart','line-replay','exploded-view','kpi-drilldown','market-terminal')
    'AI' = @('ai-composer','ai-chat','ai-side-panel','research-run','ai-answer','model-picker','model-arena','prompt-studio','document-extract','agent-memory','agent-tasks','agent-approvals','agent-routines','agent-orchestrator','mcp-servers','knowledge-base','eval-suite','agent-run')
}

# Validate: every block and special appears exactly once.
$allSpecials = @($bySlug.Keys | Where-Object { $bySlug[$_].section -eq 'Special' })
$allBlocks = @($bySlug.Keys | Where-Object { $bySlug[$_].section -eq 'H2:Blocks' })
$specialAssigned = @($specialGroups.Values | ForEach-Object { $_ })
$blockAssigned = @($blockGroups.Values | ForEach-Object { $_ })
$specialDupes = $specialAssigned | Group-Object | Where-Object Count -gt 1
$blockMissing = $allBlocks | Where-Object { $blockAssigned -notcontains $_ }
$blockDupes = $blockAssigned | Group-Object | Where-Object Count -gt 1
Write-Output "specials=$($allSpecials.Count) assigned=$($specialAssigned.Count) dupes=$($specialDupes.Count)"
Write-Output "blocks=$($allBlocks.Count) assigned=$($blockAssigned.Count) missing=$($blockMissing -join ',') dupes=$($blockDupes | ForEach-Object { $_.Name })"

$catalog = [ordered]@{
    generatedFrom = 'https://uiarc.dev/llms-small.txt + https://uiarc.dev/docs/introduction (public docs, 2026-10-24)'
    componentCategories = @()
    specialGroups = @()
    blockGroups = @()
}

$componentItems = @()
foreach ($name in $categories.Keys) {
    $slugs = $categories[$name]
    foreach ($slug in $slugs) {
        $entry = $bySlug[$slug]
        if ($null -eq $entry) { Write-Output "MISSING component metadata: $slug"; continue }
        $componentItems += [ordered]@{
            slug = $slug
            title = $entry.title
            kind = 'component'
            category = $name
            tier = $entry.tier
            description = $entry.description
            docs = "https://uiarc.dev/components/$slug"
        }
    }
    $catalog.componentCategories += [ordered]@{ name = $name; items = @($slugs) }
}

$catalog['items'] = $componentItems

foreach ($name in $specialGroups.Keys) {
    $slugs = $specialGroups[$name]
    $catalog.specialGroups += [ordered]@{ name = $name; items = @($slugs) }
}
foreach ($name in $blockGroups.Keys) {
    $slugs = $blockGroups[$name]
    $catalog.blockGroups += [ordered]@{ name = $name; items = @($slugs) }
}

$specialItems = @()
foreach ($slug in $specialAssigned) {
    $entry = $bySlug[$slug]
    $groupName = $specialGroups.Keys | Where-Object { $specialGroups[$_] -contains $slug } | Select-Object -First 1
    $specialItems += [ordered]@{
        slug = $slug
        title = $entry.title
        kind = 'special'
        category = $groupName
        tier = $entry.tier
        description = $entry.description
        docs = "https://uiarc.dev/components/$slug"
    }
}
$blockItems = @()
foreach ($slug in $blockAssigned) {
    $entry = $bySlug[$slug]
    $groupName = $blockGroups.Keys | Where-Object { $blockGroups[$_] -contains $slug } | Select-Object -First 1
    $blockItems += [ordered]@{
        slug = $slug
        title = $entry.title
        kind = 'block'
        category = $groupName
        tier = $entry.tier
        description = $entry.description
        docs = "https://uiarc.dev/components/blocks/$slug"
    }
}
$catalog['specialItems'] = $specialItems
$catalog['blockItems'] = $blockItems

$dir = Split-Path -Parent $Out
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$catalog | ConvertTo-Json -Depth 8 | Set-Content $Out -Encoding utf8
Write-Output "wrote $Out items=$($componentItems.Count)+$($specialItems.Count)+$($blockItems.Count)"



