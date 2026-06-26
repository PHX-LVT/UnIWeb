const database = db.getSiblingDB("FullProjectDbVersion2");

const now = new Date();
const page = database.pages_draft.findOne({ Slug: "about-us" }) || database.pages_published.findOne({ Slug: "about-us" });

if (!page) {
  throw new Error("Page with slug 'about-us' was not found.");
}

const pageStableId = page.StableId;
const asset = name => `/_content/SharedComponents/demo-assets/${name}`;
const text = value => ({ en: value, vi: value, cn: value });

function style({
  backgroundType = "color",
  backgroundColor = "#ffffff",
  backgroundImageUrl = null,
  overlayColor = null,
  overlayOpacity = 0,
  height = "auto",
  customMinHeightPx = null,
  padding = "large",
  contentWidth = "normal",
  textColor = "dark",
  blockLayoutMode = "stack"
} = {}) {
  return {
    BackgroundType: backgroundType,
    BackgroundColor: backgroundColor,
    BackgroundImageUrl: backgroundImageUrl,
    BackgroundVideoUrl: null,
    GradientFrom: null,
    GradientTo: null,
    GradientDirection: "top",
    OverlayColor: overlayColor,
    OverlayOpacity: overlayOpacity,
    Height: height,
    CustomMinHeightPx: customMinHeightPx,
    Padding: padding,
    ContentWidth: contentWidth,
    TextColor: textColor,
    MobileLayout: "stack",
    BlockLayoutMode: blockLayoutMode,
    BlockGridColumns: 12,
    BlockGap: "medium"
  };
}

function sectionBase(type, stableId, order, sectionStyle) {
  return {
    _t: ["Section", type],
    StableId: stableId,
    SourceId: null,
    Version: 1,
    PublishedAt: now,
    PageStableId: pageStableId,
    Visible: true,
    Order: order,
    Style: sectionStyle,
    CreatedAt: now,
    UpdatedAt: now
  };
}

const identityHtml = `
<div id="aboutIntro" class="sc-about-identity">
  <div>
    <span class="sc-about-identity__eyebrow">Identity</span>
    <h2>A Logistics Operator<br>Built Like an Ecosystem</h2>
    <p>U&I Logistics is not just a service provider. We are an operator designing synchronized logistics ecosystems.</p>
    <p>With nationwide infrastructure and trusted global partnerships, we ensure every shipment flows through a system engineered for scale, resilience, and transparency.</p>
    <div class="sc-about-identity__metrics">
      <div class="sc-about-identity__metric"><strong>50+</strong><span>Offices Nationwide</span></div>
      <div class="sc-about-identity__metric"><strong>10,000+</strong><span>Shipments / Month</span></div>
      <div class="sc-about-identity__metric"><strong>30+</strong><span>Global Partners</span></div>
    </div>
  </div>
  <div class="sc-about-identity__frame">
    <img src="${asset("hero-bg1.jpg")}" alt="Logistics Network">
  </div>
</div>`;

const flagsHtml = `
<div class="sc-about-flags">
  <div class="sc-about-flags__inner">
    <h2>Trusted Across Global Trade Corridors</h2>
    <p>Long-term partnerships with logistics leaders worldwide</p>
    <div class="sc-about-flags__grid" aria-label="Global corridor flags">
      <span><img src="${asset("China.png")}" alt="China"></span>
      <span><img src="${asset("Korea.png")}" alt="Korea"></span>
      <span><img src="${asset("Japan.png")}" alt="Japan"></span>
      <span><img src="${asset("Thailand.png")}" alt="Thailand"></span>
      <span><img src="${asset("Germany.png")}" alt="Germany"></span>
      <span><img src="${asset("Netherlands.png")}" alt="Netherlands"></span>
      <span><img src="${asset("USA.png")}" alt="USA"></span>
      <span><img src="${asset("Australia.png")}" alt="Australia"></span>
    </div>
  </div>
</div>`;

const sections = [
  {
    ...sectionBase("hero", "about-demo-hero", 0, style({
      backgroundType: "image",
      backgroundColor: "#001a33",
      backgroundImageUrl: asset("hero-bg1.jpg"),
      overlayColor: "#001a33",
      overlayOpacity: 0.48,
      height: "custom",
      customMinHeightPx: 420,
      padding: "large",
      textColor: "light"
    })),
    Layout: "centered",
    Heading: text("About U&I Logistics"),
    Subheading: text("Engineering Logistics Ecosystems\nWe design, integrate, and operate logistics systems that connect infrastructure, technology, and people into a single synchronized flow."),
    HeadingSize: "large",
    ContentAlignment: "left",
    Buttons: [
      { _id: "about-demo-hero-btn-journey", Label: text("Our Journey"), Action: "externalUrl", Href: "#aboutIntro", Style: "filled", Visible: true, Order: 0 },
      { _id: "about-demo-hero-btn-network", Label: text("Global Network"), Action: "externalUrl", Href: "#globalNetwork", Style: "outline", Visible: true, Order: 1 }
    ]
  },
  {
    ...sectionBase("html", "about-demo-identity", 1, style({ backgroundColor: "#ffffff", padding: "large" })),
    Content: text(identityHtml)
  },
  {
    ...sectionBase("testimonial", "about-demo-values", 2, style({ backgroundColor: "#f9fafc", padding: "large" })),
    Eyebrow: text("Principles"),
    SectionTitle: text("Mission & Core Values"),
    Subheading: {},
    Layout: "cards",
    HeaderAlignment: "left",
    Columns: 4,
    Items: [
      { _id: "about-value-mission", Icon: "fa-bullseye", Title: text("Mission"), Description: text("Deliver logistics solutions engineered for long-term global growth."), ImageUrl: null, Visible: true, Order: 0 },
      { _id: "about-value-integrity", Icon: "fa-handshake-angle", Title: text("Integrity"), Description: text("Operate transparently with absolute accountability."), ImageUrl: null, Visible: true, Order: 1 },
      { _id: "about-value-innovation", Icon: "fa-lightbulb", Title: text("Innovation"), Description: text("Continuously evolve systems through technology and data."), ImageUrl: null, Visible: true, Order: 2 },
      { _id: "about-value-sustainability", Icon: "fa-leaf", Title: text("Sustainability"), Description: text("Build logistics responsibly for future generations."), ImageUrl: null, Visible: true, Order: 3 }
    ]
  },
  {
    ...sectionBase("list", "about-demo-capabilities", 3, style({ backgroundColor: "#ffffff", padding: "large" })),
    Layout: "cards",
    Columns: 3,
    SectionTitle: text("Capabilities\nWhat We Engineer"),
    ShowIcon: true,
    Items: [
      { _id: "about-cap-freight", Icon: "fa-ship", Title: text("Sea & Air Freight"), Description: text("Global transportation designed for reliability and scale."), ImageUrl: null, LinkHref: null, Visible: true, Order: 0 },
      { _id: "about-cap-warehouse", Icon: "fa-warehouse", Title: text("Warehousing"), Description: text("Integrated storage, inventory, and fulfillment operations."), ImageUrl: null, LinkHref: null, Visible: true, Order: 1 },
      { _id: "about-cap-last-mile", Icon: "fa-truck-fast", Title: text("Last Mile Delivery"), Description: text("Precision delivery powered by optimized routing systems."), ImageUrl: null, LinkHref: null, Visible: true, Order: 2 },
      { _id: "about-cap-consulting", Icon: "fa-chart-line", Title: text("Supply Chain Consulting"), Description: text("Data-driven optimization for efficiency and resilience."), ImageUrl: null, LinkHref: null, Visible: true, Order: 3 },
      { _id: "about-cap-packaging", Icon: "fa-box-open", Title: text("Packaging Solutions"), Description: text("Customized packaging engineered for protection and speed."), ImageUrl: null, LinkHref: null, Visible: true, Order: 4 },
      { _id: "about-cap-cross-border", Icon: "fa-globe", Title: text("Cross-Border Logistics"), Description: text("Seamless customs and international operations."), ImageUrl: null, LinkHref: null, Visible: true, Order: 5 }
    ]
  },
  {
    ...sectionBase("network-map", "about-demo-network", 4, style({ backgroundColor: "#f5f7fa", padding: "large" })),
    SectionTitle: text("Infrastructure\nA National Network. Connected to the World."),
    CenterLat: 16,
    CenterLng: 108,
    DefaultZoom: 6,
    Pins: [
      { _id: "about-pin-hcm", Label: "HCM Office", Lat: 10.7769, Lng: 106.7009, Href: null, Visible: true, Order: 0 },
      { _id: "about-pin-binh-duong", Label: "Binh Duong Head Office", Lat: 11.0606, Lng: 106.6509, Href: null, Visible: true, Order: 1 },
      { _id: "about-pin-song-than", Label: "Song Than ICD", Lat: 10.9076, Lng: 106.7693, Href: null, Visible: true, Order: 2 },
      { _id: "about-pin-uniwarehouse", Label: "UNIWAREHOUSE", Lat: 10.82, Lng: 106.63, Href: null, Visible: true, Order: 3 },
      { _id: "about-pin-da-nang", Label: "Da Nang Office", Lat: 16.0471, Lng: 108.2068, Href: null, Visible: true, Order: 4 },
      { _id: "about-pin-ha-noi", Label: "Ha Noi Office", Lat: 21.0278, Lng: 105.8342, Href: null, Visible: true, Order: 5 },
      { _id: "about-pin-can-tho", Label: "Can Tho Office", Lat: 10.0452, Lng: 105.7469, Href: null, Visible: true, Order: 6 }
    ]
  },
  {
    ...sectionBase("html", "about-demo-flags", 5, style({ backgroundColor: "#ffffff", padding: "large" })),
    Content: text(flagsHtml)
  },
  {
    ...sectionBase("cta", "about-demo-final-cta", 6, style({ backgroundColor: "#ffffff", padding: "large", textColor: "light" })),
    Layout: "about-final",
    Heading: text("Building the Future of Connected Logistics Ecosystems"),
    Subtext: {},
    Button: null,
    Buttons: [
      { _id: "about-cta-quote", Label: text("Get A Quote"), Action: "openModal", Href: "#quote", Style: "filled", Visible: true, Order: 0 },
      { _id: "about-cta-expert", Label: text("Talk to an Expert"), Action: "openModal", Href: "#modal", Style: "outline", Visible: true, Order: 1 }
    ]
  }
];

for (const collectionName of ["sections_draft", "sections_published"]) {
  database[collectionName].deleteMany({ PageStableId: pageStableId });
  database[collectionName].insertMany(sections.map(section => ({ ...section, _id: new ObjectId() })));
}

for (const collectionName of ["blocks_draft", "blocks_published"]) {
  database[collectionName].deleteMany({ PageStableId: pageStableId });
}

for (const collectionName of ["pages_draft", "pages_published"]) {
  database[collectionName].updateOne(
    { StableId: pageStableId },
    {
      $set: {
        Name: text("About Us"),
        Slug: "about-us",
        FullSlug: "about-us",
        Access: true,
        Visible: page.Visible === true,
        Status: 1,
        UpdatedAt: now,
        PublishedAt: now
      },
      $inc: { Version: 1 }
    }
  );
}

printjson({
  pageStableId,
  slug: "about-us",
  sectionsSeeded: sections.length,
  collections: ["sections_draft", "sections_published"]
});
