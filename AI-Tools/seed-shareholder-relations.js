const now = new Date();
const pageStableId = "page-shareholder-relations";
const pageSlug = "share-holder-relations";

const dict = (en, vi = en, cn = en) => ({ en, vi, cn });

const style = ({
  backgroundColor = "#ffffff",
  textColor = "dark",
  padding = "large",
  contentWidth = "normal",
  height = "auto",
  minHeight = null
} = {}) => ({
  BackgroundType: "color",
  BackgroundColor: backgroundColor,
  BackgroundImageUrl: null,
  BackgroundVideoUrl: null,
  GradientFrom: null,
  GradientTo: null,
  GradientDirection: "top",
  OverlayColor: null,
  OverlayOpacity: 0,
  Height: height,
  CustomMinHeightPx: minHeight,
  Padding: padding,
  ContentWidth: contentWidth,
  TextColor: textColor,
  MobileLayout: "stack",
  BlockLayoutMode: "stack",
  BlockGridColumns: 12,
  BlockGap: "medium"
});

const makePage = (existing, published = false) => ({
  _id: existing?._id ?? new ObjectId(),
  StableId: pageStableId,
  SourceId: null,
  Version: (existing?.Version ?? 0) + 1,
  PublishedAt: published ? now : null,
  Name: dict("Shareholder Relations", "Quan hệ cổ đông", "Shareholder Relations"),
  Slug: pageSlug,
  Access: true,
  Visible: true,
  Order: existing?.Order ?? 99,
  Status: 1,
  Seo: {
    MetaTitle: dict("Shareholder Relations | U&I Logistics"),
    MetaDescription: dict("Reports, governance documents, company charters, regulations, and public disclosures for shareholders.")
  },
  CreatedAt: existing?.CreatedAt ?? now,
  UpdatedAt: now,
  ParentPageId: null,
  ParentSlug: null,
  FullSlug: pageSlug,
  Card: {
    CardTitle: dict("Shareholder Relations"),
    CardContent: dict("Governance, reports, regulations, and disclosures for investors and shareholders."),
    CardBackgroundType: "color",
    CardBackgroundColor: "#ffffff",
    CardImageUrl: null,
    IsCustomized: true
  }
});

const sectionBase = (id, type, order, published = false, sectionStyle = style()) => ({
  _id: ObjectId.createFromHexString(id),
  _t: ["Section", type],
  StableId: `${pageStableId}-${type}-${order}`,
  SourceId: null,
  Version: 1,
  PublishedAt: published ? now : null,
  PageStableId: pageStableId,
  Visible: true,
  Order: order,
  Style: sectionStyle,
  CreatedAt: now,
  UpdatedAt: now
});

const heroSection = (published = false) => ({
  ...sectionBase("6a3000010000000000000001", "hero", 0, published, style({
    backgroundColor: "#07162f",
    textColor: "light",
    padding: "xl",
    minHeight: 420
  })),
  Layout: "centered",
  Heading: dict("Shareholder Relations", "Quan hệ cổ đông", "Shareholder Relations"),
  Subheading: dict(
    "Transparent access to financial reports, governance documents, company regulations, and official disclosures.",
    "Truy cập minh bạch vào báo cáo tài chính, tài liệu quản trị, quy định công ty và công bố chính thức.",
    "Transparent access to financial reports, governance documents, company regulations, and official disclosures."
  ),
  HeadingSize: "large",
  ContentAlignment: "center",
  Buttons: []
});

const librarySection = ({
  id,
  order,
  contentTypes,
  layout,
  columns,
  rows,
  limit,
  enableTabs,
  enablePagination,
  eyebrow,
  title,
  subheading,
  showImage,
  showSummary,
  showButton,
  showSearchBar,
  showFilters,
  searchPlaceholder,
  sortMode = "newest",
  published = false
}) => ({
  ...sectionBase(id, "library", order, published, style({
    backgroundColor: order % 2 === 0 ? "#ffffff" : "#f7f8fb",
    textColor: "dark",
    padding: "xl",
    contentWidth: "full"
  })),
  ContentTypes: contentTypes,
  Layout: layout,
  Columns: columns,
  Rows: rows,
  Limit: limit,
  EnableTabs: enableTabs,
  EnablePagination: enablePagination,
  Eyebrow: dict(eyebrow),
  SectionTitle: dict(title),
  Subheading: dict(subheading),
  ShowImage: showImage,
  ShowSummary: showSummary,
  ShowButton: showButton,
  ButtonLabel: dict("Download"),
  ButtonStyle: "filled",
  ShowSearchBar: showSearchBar,
  ShowFilters: showFilters,
  SearchPlaceholder: dict(searchPlaceholder),
  SortMode: sortMode
});

const ctaSection = (published = false) => ({
  ...sectionBase("6a3000010000000000000005", "cta", 4, published, style({
    backgroundColor: "#07162f",
    textColor: "light",
    padding: "xl"
  })),
  Heading: dict("Need investor information?"),
  Subtext: dict("Contact U&I Logistics for shareholder relations support and official documentation requests."),
  Layout: "withSubtext",
  Buttons: [
    {
      _id: "shareholder-cta-contact",
      Label: dict("Contact Us"),
      Action: "linkToPage",
      Href: "/contact-network",
      Style: "filled",
      Visible: true,
      Order: 0
    }
  ]
});

const sections = (published = false) => [
  heroSection(published),
  librarySection({
    id: "6a3000010000000000000002",
    order: 1,
    contentTypes: ["financial-report", "governance-report"],
    layout: "grid",
    columns: 3,
    rows: 2,
    limit: 6,
    enableTabs: true,
    enablePagination: true,
    eyebrow: "Reports",
    title: "Governance Reports",
    subheading: "Browse financial performance and governance reports by category.",
    showImage: false,
    showSummary: true,
    showButton: true,
    showSearchBar: false,
    showFilters: false,
    searchPlaceholder: "Search reports",
    published
  }),
  librarySection({
    id: "6a3000010000000000000003",
    order: 2,
    contentTypes: ["company-charter-regulation"],
    layout: "rows",
    columns: 2,
    rows: 2,
    limit: 4,
    enableTabs: false,
    enablePagination: false,
    eyebrow: "Corporate Documents",
    title: "Company Charter & Regulations",
    subheading: "Core corporate governance documents and operating regulations.",
    showImage: false,
    showSummary: true,
    showButton: true,
    showSearchBar: false,
    showFilters: false,
    searchPlaceholder: "Search documents",
    published
  }),
  librarySection({
    id: "6a3000010000000000000004",
    order: 3,
    contentTypes: ["disclosure"],
    layout: "lists",
    columns: 1,
    rows: 10,
    limit: 10,
    enableTabs: false,
    enablePagination: true,
    eyebrow: "",
    title: "Disclosures",
    subheading: "",
    showImage: false,
    showSummary: false,
    showButton: true,
    showSearchBar: true,
    showFilters: true,
    searchPlaceholder: "Search disclosures",
    published
  }),
  ctaSection(published)
];

const contentTypes = [
  {
    Key: "financial-report",
    Name: dict("Financial Report"),
    Description: dict("Financial statements, annual reports, and operating performance reports."),
    RequiresBody: false,
    RequiresHeroImage: false,
    RequiresFile: true,
    RequiresVideoUrl: false,
    AllowsAttachments: true,
    ClickBehavior: "download",
    Visible: true,
    Order: 20
  },
  {
    Key: "governance-report",
    Name: dict("Governance Report"),
    Description: dict("Corporate governance reports and shareholder governance updates."),
    RequiresBody: false,
    RequiresHeroImage: false,
    RequiresFile: true,
    RequiresVideoUrl: false,
    AllowsAttachments: true,
    ClickBehavior: "download",
    Visible: true,
    Order: 21
  },
  {
    Key: "company-charter-regulation",
    Name: dict("Company Charter & Regulations"),
    Description: dict("Company charter, board regulations, and internal governance policies."),
    RequiresBody: false,
    RequiresHeroImage: false,
    RequiresFile: true,
    RequiresVideoUrl: false,
    AllowsAttachments: true,
    ClickBehavior: "download",
    Visible: true,
    Order: 22
  },
  {
    Key: "disclosure",
    Name: dict("Disclosure"),
    Description: dict("Official company announcements and disclosure documents."),
    RequiresBody: false,
    RequiresHeroImage: false,
    RequiresFile: true,
    RequiresVideoUrl: false,
    AllowsAttachments: true,
    ClickBehavior: "download",
    Visible: true,
    Order: 23
  }
];

const pdfUrl = slug => `/downloads/shareholder-relations/${slug}.pdf`;

const contentItem = ({ stableId, type, slug, title, summary, tags, daysAgo, fileName }) => {
  const created = new Date(now.getTime() - daysAgo * 24 * 60 * 60 * 1000);
  return {
    StableId: stableId,
    ContentTypeKey: type,
    Slug: slug,
    Title: dict(title),
    Summary: dict(summary),
    BodyHtml: dict(`<p>${summary}</p>`),
    HeroImageUrl: null,
    HeroImageAlt: null,
    ThumbnailUrl: null,
    VideoUrl: null,
    ExternalUrl: null,
    TemplateKey: null,
    Tags: tags,
    Attachments: [
      {
        Id: `${stableId}-file`,
        FileName: fileName,
        Url: pdfUrl(slug),
        ContentType: "application/pdf",
        SizeBytes: 1250000
      }
    ],
    Status: 3,
    Visible: true,
    AuthorId: "admin",
    UpdatedById: "admin",
    PublishedById: "admin",
    CreatedAt: created,
    UpdatedAt: created,
    SubmittedAt: created,
    PublishedAt: created
  };
};

const contentItems = [
  contentItem({
    stableId: "shareholder-financial-annual-2025",
    type: "financial-report",
    slug: "annual-report-2025",
    title: "Annual Report 2025",
    summary: "Comprehensive annual business, financial, and operational performance report for fiscal year 2025.",
    tags: ["Annual Report", "2025"],
    daysAgo: 6,
    fileName: "annual-report-2025.pdf"
  }),
  contentItem({
    stableId: "shareholder-financial-q4-2025",
    type: "financial-report",
    slug: "q4-2025-financial-statements",
    title: "Q4 2025 Financial Statements",
    summary: "Quarterly financial statements and key operating indicators for Q4 2025.",
    tags: ["Financial Statement", "2025"],
    daysAgo: 18,
    fileName: "q4-2025-financial-statements.pdf"
  }),
  contentItem({
    stableId: "shareholder-financial-q3-2025",
    type: "financial-report",
    slug: "q3-2025-financial-statements",
    title: "Q3 2025 Financial Statements",
    summary: "Quarterly financial statements and key operating indicators for Q3 2025.",
    tags: ["Financial Statement", "2025"],
    daysAgo: 44,
    fileName: "q3-2025-financial-statements.pdf"
  }),
  contentItem({
    stableId: "shareholder-governance-2025",
    type: "governance-report",
    slug: "corporate-governance-report-2025",
    title: "Corporate Governance Report 2025",
    summary: "Annual governance report covering board structure, shareholder rights, and compliance practices.",
    tags: ["Governance", "2025"],
    daysAgo: 9,
    fileName: "corporate-governance-report-2025.pdf"
  }),
  contentItem({
    stableId: "shareholder-governance-board-2025",
    type: "governance-report",
    slug: "board-activity-report-2025",
    title: "Board Activity Report 2025",
    summary: "Summary of board activities, committee oversight, and governance initiatives.",
    tags: ["Governance", "Board"],
    daysAgo: 32,
    fileName: "board-activity-report-2025.pdf"
  }),
  contentItem({
    stableId: "shareholder-governance-compliance-2025",
    type: "governance-report",
    slug: "compliance-and-risk-governance-report",
    title: "Compliance & Risk Governance Report",
    summary: "Governance disclosure covering compliance controls, risk oversight, and internal policies.",
    tags: ["Governance", "Compliance"],
    daysAgo: 70,
    fileName: "compliance-and-risk-governance-report.pdf"
  }),
  contentItem({
    stableId: "shareholder-charter-company",
    type: "company-charter-regulation",
    slug: "company-charter",
    title: "Company Charter",
    summary: "The official company charter defining governance principles, shareholder rights, and operating rules.",
    tags: ["Charter"],
    daysAgo: 12,
    fileName: "company-charter.pdf"
  }),
  contentItem({
    stableId: "shareholder-charter-board",
    type: "company-charter-regulation",
    slug: "board-of-directors-regulation",
    title: "Board of Directors Regulation",
    summary: "Rules and operating framework for the Board of Directors.",
    tags: ["Regulation"],
    daysAgo: 19,
    fileName: "board-of-directors-regulation.pdf"
  }),
  contentItem({
    stableId: "shareholder-charter-disclosure-policy",
    type: "company-charter-regulation",
    slug: "information-disclosure-policy",
    title: "Information Disclosure Policy",
    summary: "Policy governing shareholder communications and public information disclosure.",
    tags: ["Policy"],
    daysAgo: 26,
    fileName: "information-disclosure-policy.pdf"
  }),
  contentItem({
    stableId: "shareholder-charter-ir-policy",
    type: "company-charter-regulation",
    slug: "investor-relations-policy",
    title: "Investor Relations Policy",
    summary: "Investor relations rules for shareholder communication and document access.",
    tags: ["Policy"],
    daysAgo: 38,
    fileName: "investor-relations-policy.pdf"
  }),
  contentItem({
    stableId: "shareholder-disclosure-agm-2026",
    type: "disclosure",
    slug: "notice-of-annual-general-meeting-2026",
    title: "Notice of Annual General Meeting 2026",
    summary: "Official notice of the 2026 Annual General Meeting.",
    tags: ["AGM", "2026"],
    daysAgo: 4,
    fileName: "notice-of-annual-general-meeting-2026.pdf"
  }),
  contentItem({
    stableId: "shareholder-disclosure-dividend-2025",
    type: "disclosure",
    slug: "dividend-payment-announcement-2025",
    title: "Dividend Payment Announcement 2025",
    summary: "Disclosure announcement on dividend payment schedule and shareholder eligibility.",
    tags: ["Dividend", "2025"],
    daysAgo: 15,
    fileName: "dividend-payment-announcement-2025.pdf"
  }),
  contentItem({
    stableId: "shareholder-disclosure-board-resolution",
    type: "disclosure",
    slug: "board-resolution-on-strategic-investment",
    title: "Board Resolution on Strategic Investment",
    summary: "Public disclosure of board resolution regarding strategic investment planning.",
    tags: ["Resolution", "2025"],
    daysAgo: 28,
    fileName: "board-resolution-on-strategic-investment.pdf"
  }),
  contentItem({
    stableId: "shareholder-disclosure-change-personnel",
    type: "disclosure",
    slug: "announcement-on-senior-personnel-change",
    title: "Announcement on Senior Personnel Change",
    summary: "Official disclosure of senior personnel changes.",
    tags: ["Personnel", "2025"],
    daysAgo: 42,
    fileName: "announcement-on-senior-personnel-change.pdf"
  })
];

function upsertContentType(contentType) {
  const existing = db.content_types.findOne({ Key: contentType.Key });
  db.content_types.replaceOne(
    { Key: contentType.Key },
    {
      _id: existing?._id ?? new ObjectId(),
      ...contentType,
      CreatedAt: existing?.CreatedAt ?? now,
      UpdatedAt: now
    },
    { upsert: true }
  );
}

function upsertContentItem(collectionName, item) {
  const collection = db.getCollection(collectionName);
  const existing = collection.findOne({ StableId: item.StableId });
  collection.replaceOne(
    { StableId: item.StableId },
    {
      _id: existing?._id ?? new ObjectId(),
      ...item,
      CreatedAt: existing?.CreatedAt ?? item.CreatedAt,
      UpdatedAt: now,
      PublishedAt: item.PublishedAt ?? now
    },
    { upsert: true }
  );
}

function upsertPage(collectionName, published = false) {
  const collection = db.getCollection(collectionName);
  const existing = collection.findOne({ StableId: pageStableId }) ?? collection.findOne({ Slug: pageSlug });
  collection.replaceOne(
    existing ? { _id: existing._id } : { StableId: pageStableId },
    makePage(existing, published),
    { upsert: true }
  );
}

function replaceSections(collectionName, published = false) {
  const collection = db.getCollection(collectionName);
  collection.deleteMany({ PageStableId: pageStableId });
  collection.insertMany(sections(published));
}

contentTypes.forEach(upsertContentType);
contentItems.forEach(item => {
  upsertContentItem("content_draft", item);
  upsertContentItem("content_published", item);
});

upsertPage("pages_draft", false);
upsertPage("pages_published", true);
replaceSections("sections_draft", false);
replaceSections("sections_published", true);

printjson({
  page: pageSlug,
  pageStableId,
  contentTypes: contentTypes.map(t => t.Key),
  contentItems: contentItems.length,
  sections: sections(false).length
});
